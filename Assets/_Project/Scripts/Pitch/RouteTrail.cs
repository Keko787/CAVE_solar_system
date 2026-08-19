using UnityEngine;
using Stadium3D.Football;

namespace Stadium3D.Pitch
{
    /// <summary>
    /// The path one wall's focused player has run so far this play, drawn on the turf.
    /// </summary>
    /// <remarks>
    /// <b>Sampled from the bake, never accumulated.</b> The obvious way to draw a trail is to append the
    /// player's position every frame, and it would be wrong here in three ways at once: it would double up
    /// under slow motion, thin out under a frame-rate dip, and survive a seek — so scrubbing back would
    /// leave a trail through places the player has not been yet. M1 bakes every route into a fixed-rate
    /// <see cref="MotionTrack"/> precisely so that position is a pure function of time, and this asks it
    /// forty-eight times between the snap and now. Replay, looping and slow motion then need no special
    /// case at all, which is the same argument the sim itself rests on.
    ///
    /// <para>A fixed sample count rather than a fixed interval, so the work per frame is constant and the
    /// resolution of the line does not depend on how long the play has been running.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public class RouteTrail : MonoBehaviour
    {
        /// <summary>Points along the line. Constant, so neither cost nor detail drifts with play time.</summary>
        public const int Samples = 48;

        /// <summary>Below this the play has barely started and every sample lands on the same spot.</summary>
        const float MinimumSeconds = 0.08f;

        [SerializeField, Tooltip("Whose focused player this trail follows. The floor carries one of each; " +
            "each team wall carries only its own.")]
        TeamSide m_Side = TeamSide.Home;

        [SerializeField, Tooltip("Height above the turf, metres.")]
        float m_Height = FieldMarkings.OverlayY + 0.004f;

        LineRenderer m_Line;

        /// <summary>Which side's focus this trail draws.</summary>
        public TeamSide Side => m_Side;

        void Awake() => m_Line = GetComponent<LineRenderer>();

        void LateUpdate()
        {
            var driver = FootballSimDriver.Instance;
            var sim = driver != null ? driver.Sim : null;

            if (sim == null) Hide();
            else Apply(sim, driver.FocusOf(m_Side));
        }

        /// <summary>
        /// Draw one player's route so far. Public because the editor capture tooling has no run loop.
        /// </summary>
        public void Apply(FootballSim sim, int playerId)
        {
            if (m_Line == null) m_Line = GetComponent<LineRenderer>();
            if (m_Line == null || sim == null) return;

            var play = sim.CurrentBakedPlay;
            if (playerId == 0 || play == null || !play.Has(playerId)) { Hide(); return; }

            float now = Mathf.Clamp(sim.Director.PlayTime, 0f, play.Duration);
            if (now < MinimumSeconds) { Hide(); return; }

            m_Line.enabled = true;
            if (m_Line.positionCount != Samples) m_Line.positionCount = Samples;

            for (int i = 0; i < Samples; i++)
            {
                float t = now * i / (Samples - 1);
                Vector3 p = play.Sample(playerId, t).Position;

                // Local space. The sim works in field coordinates and the pitch is parked 1500 m below the
                // room, so a world-space line would draw itself up in the CAVE.
                m_Line.SetPosition(i, new Vector3(p.x, m_Height, p.z));
            }
        }

        void Hide()
        {
            if (m_Line != null && m_Line.enabled) m_Line.enabled = false;
        }

        /// <summary>Set which side's focus this trail follows, from a builder.</summary>
        public void Configure(TeamSide side) => m_Side = side;
    }
}
