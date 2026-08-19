using System;

namespace Stadium3D.Operators
{
    /// <summary>
    /// Derives press/release edges from a device that only reports level state ("is it down?"). MiddleVR's
    /// wand gives us a real toggle flag, but a raw <c>vrJoystick</c> and a hand-rolled index space do not,
    /// so each <see cref="IOperatorInput"/> that needs edges owns one of these and calls
    /// <see cref="Sample"/> exactly once per frame.
    ///
    /// Kept as plain C# (no <c>MonoBehaviour</c>, no <c>Time</c> read) so the edge semantics — the part
    /// that is easy to get subtly wrong and that every consumer depends on — are unit-testable without a
    /// device, a scene, or a play-mode frame.
    /// </summary>
    public sealed class ButtonEdgeSampler
    {
        bool[] m_Previous;
        bool[] m_Current;

        /// <summary>Number of buttons currently tracked (grows to fit whatever <see cref="Sample"/> is given).</summary>
        public int Count => m_Current != null ? m_Current.Length : 0;

        /// <summary>
        /// Latch this frame's button states. Call once per frame, before anything reads the edges —
        /// sources do it from an early-execution <c>Update</c>.
        /// </summary>
        /// <param name="held">Level state for a button index. Never called with an out-of-range index.</param>
        /// <param name="count">How many buttons to sample.</param>
        public void Sample(Func<int, bool> held, int count)
        {
            count = count < 0 ? 0 : count;
            EnsureSize(count);
            if (held == null)
            {
                // Device vanished mid-session (pad unplugged): roll everything to "up" so the frame it
                // disappears reads as a release rather than latching a button down forever.
                for (int i = 0; i < m_Current.Length; i++)
                {
                    m_Previous[i] = m_Current[i];
                    m_Current[i] = false;
                }
                return;
            }

            for (int i = 0; i < m_Current.Length; i++)
            {
                m_Previous[i] = m_Current[i];
                m_Current[i] = i < count && held(i);
            }
        }

        /// <summary>Level state as of the last <see cref="Sample"/>.</summary>
        public bool Held(int button) => InRange(button) && m_Current[button];

        /// <summary>True if the last <see cref="Sample"/> saw this button go down.</summary>
        public bool PressedThisFrame(int button) => InRange(button) && m_Current[button] && !m_Previous[button];

        /// <summary>True if the last <see cref="Sample"/> saw this button come up.</summary>
        public bool ReleasedThisFrame(int button) => InRange(button) && !m_Current[button] && m_Previous[button];

        /// <summary>Forget all state — used when a device is swapped so stale edges can't fire.</summary>
        public void Reset()
        {
            if (m_Current == null)
                return;
            Array.Clear(m_Current, 0, m_Current.Length);
            Array.Clear(m_Previous, 0, m_Previous.Length);
        }

        bool InRange(int button) => m_Current != null && button >= 0 && button < m_Current.Length;

        // Never shrinks: a device that reports fewer buttons after a reconnect would otherwise leave
        // consumers indexing past the end, and the extra slots simply read as "up".
        void EnsureSize(int count)
        {
            if (m_Current != null && m_Current.Length >= count)
                return;
            int size = count > 0 ? count : 1;
            var prev = new bool[size];
            var curr = new bool[size];
            if (m_Current != null)
            {
                Array.Copy(m_Previous, prev, m_Previous.Length);
                Array.Copy(m_Current, curr, m_Current.Length);
            }
            m_Previous = prev;
            m_Current = curr;
        }
    }
}
