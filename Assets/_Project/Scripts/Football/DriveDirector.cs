using System;
using UnityEngine;

namespace Stadium3D.Football
{
    /// <summary>
    /// The entire mutable state of the simulation, in five scalars. Everything else — 22 poses, the ball, the
    /// situation, the box score, the telemetry — is derived from these by pure functions, which is why a
    /// snapshot is all it takes to seek, replay, loop or capture the sim exactly.
    /// </summary>
    [Serializable]
    public struct DriveSnapshot
    {
        public int PlayIndex;
        public int PreviousPlayIndex;
        public double PlayTime;
        public double PhaseTime;
        public PlayPhase Phase;
    }

    /// <summary>
    /// Walks the previous play's terminal poses to the next play's alignment across the huddle beat, so
    /// bodies get to their spots by walking rather than teleporting between plays.
    /// </summary>
    public static class HuddleBlend
    {
        /// <summary>
        /// Blend at <paramref name="u"/> in 0..1. Position uses smoothstep so the walk eases out of the last
        /// play and into the stance; facing takes the short arc. When <c>from</c> and <c>to</c> are the same
        /// pose — the first play, a player who was not on the previous play, the loop seam — nothing moves.
        /// </summary>
        public static MotionSample Sample(in MotionSample from, in MotionSample to, float u)
        {
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u));
            return new MotionSample(
                0f,
                Vector3.Lerp(from.Position, to.Position, e),
                Mathf.LerpAngle(from.HeadingDegrees, to.HeadingDegrees, e),
                0f, 0f, 0f);
        }
    }

    /// <summary>
    /// The transport. Owns <see cref="DriveSnapshot"/> and nothing else — it never touches a pose, a stat or
    /// the ball, which is what keeps "where are we in the drive" separate from "what does that look like".
    /// </summary>
    /// <remarks>
    /// <b>Seeks set time rather than winding it.</b> <see cref="DriveSnapshot.PlayTime"/> is assigned
    /// directly on a replay or a jump, so no accumulation order survives a seek and two different dt
    /// schedules that arrive at the same time produce byte-identical frames.
    /// </remarks>
    public sealed class DriveDirector
    {
        readonly DriveScript m_Drive;
        readonly Func<int, float> m_DurationOf;

        DriveSnapshot m_State;

        /// <summary>Raised when a play's snap fires.</summary>
        public event Action<int> PlaySnapped;

        /// <summary>Raised at the whistle.</summary>
        public event Action<int> PlayEnded;

        /// <summary>Raised when the drive wraps back to play 0.</summary>
        public event Action Looped;

        /// <summary>Raised when a non-looping drive finishes.</summary>
        public event Action DriveCompleted;

        /// <summary>Frozen: ticks do nothing at all.</summary>
        public bool Paused { get; private set; }

        /// <summary>
        /// Time multiplier. The beat schedule stretches the hero play's ball flight to about 0.3× so a person
        /// has time to actually use four surfaces — a 4-second play is otherwise over before anyone can turn
        /// their head. First-class here rather than a post-hoc effect, because slow motion has to survive a
        /// replay and a loop identically.
        /// </summary>
        public float TimeScale { get; set; } = 1f;

        public int PlayIndex => m_State.PlayIndex;
        public int PreviousPlayIndex => m_State.PreviousPlayIndex;
        public float PlayTime => (float)m_State.PlayTime;
        public float PhaseTime => (float)m_State.PhaseTime;
        public PlayPhase Phase => m_State.Phase;

        public DriveDirector(DriveScript drive, Func<int, float> durationOf)
        {
            m_Drive = drive;
            m_DurationOf = durationOf;
            Reset();
        }

        /// <summary>Back to the top of the drive, in the huddle before play 0.</summary>
        public void Reset()
        {
            m_State = new DriveSnapshot
            {
                PlayIndex = 0,
                PreviousPlayIndex = -1,
                PlayTime = 0,
                PhaseTime = 0,
                Phase = PlayPhase.Huddle,
            };
        }

        /// <summary>Advance the transport. The only mutation in the whole system.</summary>
        public void Tick(double dt)
        {
            if (Paused || dt <= 0) return;
            dt *= Mathf.Max(0f, TimeScale);
            if (dt <= 0) return;

            switch (m_State.Phase)
            {
                case PlayPhase.Huddle:
                    m_State.PhaseTime += dt;
                    if (m_State.PhaseTime >= m_Drive.HuddleSeconds)
                    {
                        m_State.Phase = PlayPhase.Live;
                        m_State.PhaseTime = 0;
                        m_State.PlayTime = 0;
                        PlaySnapped?.Invoke(m_State.PlayIndex);
                    }
                    break;

                case PlayPhase.Live:
                {
                    m_State.PlayTime += dt;
                    float duration = m_DurationOf(m_State.PlayIndex);
                    if (m_State.PlayTime >= duration)
                    {
                        m_State.PlayTime = duration;      // clamped, never wrapped
                        m_State.Phase = PlayPhase.PostPlay;
                        m_State.PhaseTime = 0;
                        PlayEnded?.Invoke(m_State.PlayIndex);
                    }
                    break;
                }

                case PlayPhase.PostPlay:
                    m_State.PhaseTime += dt;
                    if (m_State.PhaseTime >= m_Drive.PostPlaySeconds)
                    {
                        m_State.PreviousPlayIndex = m_State.PlayIndex;
                        m_State.PlayIndex++;
                        m_State.PhaseTime = 0;
                        m_State.PlayTime = 0;

                        if (m_State.PlayIndex >= m_Drive.Plays.Count)
                        {
                            if (m_Drive.Loop)
                            {
                                m_State.PlayIndex = 0;
                                m_State.Phase = PlayPhase.Huddle;
                                Looped?.Invoke();
                            }
                            else
                            {
                                m_State.PlayIndex = m_Drive.Plays.Count - 1;
                                m_State.Phase = PlayPhase.DriveComplete;
                                DriveCompleted?.Invoke();
                            }
                        }
                        else
                        {
                            m_State.Phase = PlayPhase.Huddle;
                        }
                    }
                    break;

                case PlayPhase.DriveComplete:
                    break;
            }
        }

        /// <summary>Freeze or resume. A frozen transport holds every scalar exactly.</summary>
        public void SetPaused(bool paused) => Paused = paused;

        /// <summary>Toggle pause, for a single wand button.</summary>
        public void TogglePaused() => Paused = !Paused;

        /// <summary>
        /// Run the current play again from its huddle. Because seeking is an assignment rather than a rewind,
        /// the replay is bit-identical to the first pass.
        /// </summary>
        public void ReplayCurrentPlay()
        {
            m_State.PlayTime = 0;
            m_State.PhaseTime = 0;
            m_State.Phase = PlayPhase.Huddle;
        }

        /// <summary>
        /// Jump to a play. <paramref name="atTime"/> below zero starts it in the huddle; otherwise the play is
        /// live at exactly that time.
        /// </summary>
        public void SeekToPlay(int index, float atTime = -1f)
        {
            int count = Mathf.Max(1, m_Drive.Plays.Count);
            index = ((index % count) + count) % count;

            m_State.PreviousPlayIndex = index == 0 ? -1 : index - 1;
            m_State.PlayIndex = index;
            m_State.PhaseTime = 0;

            if (atTime < 0f)
            {
                m_State.Phase = PlayPhase.Huddle;
                m_State.PlayTime = 0;
            }
            else
            {
                m_State.Phase = PlayPhase.Live;
                m_State.PlayTime = Mathf.Clamp(atTime, 0f, m_DurationOf(index));
            }
        }

        /// <summary>The transport state, for an attract-mode reset or a test digest.</summary>
        public DriveSnapshot Capture() => m_State;

        /// <summary>Restore a captured state exactly.</summary>
        public void Restore(DriveSnapshot snapshot) => m_State = snapshot;
    }
}
