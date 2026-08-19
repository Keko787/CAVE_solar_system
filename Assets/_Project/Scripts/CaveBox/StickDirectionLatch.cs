using UnityEngine;

namespace Stadium3D.CaveBox
{
    /// <summary>
    /// Turns an analog stick into discrete focus steps: one step when it is pushed, then auto-repeat while
    /// it is held, and nothing at all until it comes back near centre. Without this a held stick would move
    /// focus once per frame and shoot across a panel in a fraction of a second.
    ///
    /// The centre band is the same "re-arm near centre" shape <c>FrontWallPortal</c> and
    /// <c>RescanController</c> already use for the wand thumbstick, so a stick gesture means the same thing
    /// whichever operator makes it. Pure C# with an injected delta time, so the timing — the part that is
    /// pure feel and cannot be eyeballed from a test log — is pinned deterministically.
    /// </summary>
    public sealed class StickDirectionLatch
    {
        readonly float m_Threshold;
        readonly float m_Rearm;
        readonly float m_FirstRepeatDelay;
        readonly float m_RepeatInterval;

        FocusDirection? m_Current;
        float m_Timer;

        /// <param name="threshold">Push past this to register a step. Sits above the .vrx 0.3 dead zone.</param>
        /// <param name="rearm">Fall below this to clear, so the next push is a fresh step.</param>
        /// <param name="firstRepeatDelay">Hold time before auto-repeat starts.</param>
        /// <param name="repeatInterval">Time between repeats once it has started.</param>
        public StickDirectionLatch(float threshold = 0.6f, float rearm = 0.3f,
            float firstRepeatDelay = 0.45f, float repeatInterval = 0.14f)
        {
            m_Threshold = Mathf.Max(0.01f, threshold);
            m_Rearm = Mathf.Clamp(rearm, 0f, m_Threshold);
            m_FirstRepeatDelay = Mathf.Max(0f, firstRepeatDelay);
            m_RepeatInterval = Mathf.Max(0.01f, repeatInterval);
        }

        /// <summary>The direction currently being held, if any.</summary>
        public FocusDirection? Current => m_Current;

        /// <summary>
        /// Advance by one frame. Returns true on the frames a step should be taken — once when the stick is
        /// pushed, then again each time auto-repeat comes round.
        /// </summary>
        public bool Tick(Vector2 stick, float deltaTime, out FocusDirection direction)
        {
            direction = FocusDirection.Up;

            var next = Classify(stick);
            if (next == null)
            {
                m_Current = null;
                m_Timer = 0f;
                return false;
            }

            if (m_Current != next)
            {
                // A new push, or a change of direction: step immediately and start the hold clock.
                m_Current = next;
                m_Timer = m_FirstRepeatDelay;
                direction = next.Value;
                return true;
            }

            m_Timer -= Mathf.Max(0f, deltaTime);
            if (m_Timer > 0f)
                return false;

            m_Timer = m_RepeatInterval;
            direction = next.Value;
            return true;
        }

        /// <summary>Forget the held direction — after a focus change driven from elsewhere, or on enable.</summary>
        public void Reset()
        {
            m_Current = null;
            m_Timer = 0f;
        }

        // Null means centred. Between the re-arm band and the threshold the stick holds whatever it already
        // had, so a stick resting just off-centre cannot chatter between a direction and nothing.
        FocusDirection? Classify(Vector2 stick)
        {
            float ax = Mathf.Abs(stick.x);
            float ay = Mathf.Abs(stick.y);
            float max = Mathf.Max(ax, ay);

            if (max < m_Rearm)
                return null;
            if (max < m_Threshold)
                return m_Current;

            // The dominant axis wins outright, so a diagonal push steps one way rather than both.
            if (ay >= ax)
                return stick.y > 0f ? FocusDirection.Up : FocusDirection.Down;
            return stick.x > 0f ? FocusDirection.Right : FocusDirection.Left;
        }
    }
}
