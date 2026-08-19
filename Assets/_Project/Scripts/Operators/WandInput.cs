using System;
using UnityEngine;
using Stadium3D.Vr;   // MiddleVRWand — the existing wand read, unchanged

namespace Stadium3D.Operators
{
    /// <summary>
    /// The primary operator's MiddleVR wand as an <see cref="IOperatorInput"/>. A thin adapter over the
    /// existing <see cref="MiddleVRWand"/> helper that deliberately adds no behaviour, so the wand keeps
    /// reading exactly as it always has while becoming addressable as "operator Primary".
    ///
    /// Plain C# with a process-wide <see cref="Shared"/> instance rather than a <c>MonoBehaviour</c>, for
    /// two reasons. A cursor can fall back to it when no source component has been wired into the scene
    /// yet, so adding the operator layer changes nothing about how existing scenes behave. And the
    /// registry then holds a plain object instead of a <c>UnityEngine.Object</c> that could be destroyed
    /// out from under it — a destroyed component is not <c>null</c> through an interface reference,
    /// because the interface call bypasses Unity's lifetime-checking <c>==</c> overload.
    /// </summary>
    public sealed class WandInput : IOperatorInput
    {
        /// <summary>The wand. There is only ever one, so consumers share this instance.</summary>
        public static WandInput Shared { get; private set; } = new();

        readonly ButtonEdgeSampler m_Edges = new();
        readonly Func<int, bool> m_HeldProbe;
        readonly Func<int> m_FrameCounter;

        int m_ButtonCount = DefaultButtonCount;
        int m_SampledFrame = int.MinValue;

        /// <summary>
        /// Buttons the VisBox Flystick exposes, which is what the <c>.vrx</c> <c>Wand0</c> entry declares
        /// (<c>Button0</c>..<c>Button5</c>).
        /// </summary>
        public const int DefaultButtonCount = 6;

        /// <param name="frameCounter">
        /// Source of the current frame number; defaults to <c>Time.frameCount</c>. Injected by tests so
        /// frames can be stepped deterministically.
        /// </param>
        public WandInput(Func<int> frameCounter = null)
        {
            m_FrameCounter = frameCounter ?? (() => Time.frameCount);
            m_HeldProbe = Held;   // cached once; a fresh delegate per frame would allocate every frame
        }

        // Enter-play-mode-without-domain-reload would otherwise carry the previous session's latched
        // button states into the next one.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Shared = new WandInput();

        /// <inheritdoc/>
        public string DeviceName => "MiddleVR Wand";

        /// <inheritdoc/>
        public bool IsConnected => MiddleVRWand.IsAvailable;

        /// <inheritdoc/>
        public int ButtonCount
        {
            get => m_ButtonCount;
            set => m_ButtonCount = Mathf.Max(0, value);
        }

        /// <summary>Horizontal (0) and vertical (1) thumbstick axes, as the .vrx declares them.</summary>
        public int AxisCount => 2;

        /// <inheritdoc/>
        public bool Held(int button) => button >= 0 && button < m_ButtonCount && MiddleVRWand.Held(button);

        /// <summary>
        /// Press edges come straight from MiddleVR's own toggle flag rather than from sampling, because
        /// the kernel sees transitions between Unity frames. This keeps the wand's feel bit-identical to
        /// what every existing consumer already gets from <see cref="MiddleVRWand"/>.
        /// </summary>
        public bool PressedThisFrame(int button) =>
            button >= 0 && button < m_ButtonCount && MiddleVRWand.PressedThisFrame(button);

        /// <summary>
        /// Release edges are derived by sampling — <see cref="MiddleVRWand"/> never exposed them, and the
        /// existing consumers never needed them.
        /// </summary>
        public bool ReleasedThisFrame(int button)
        {
            EnsureSampled();
            return m_Edges.ReleasedThisFrame(button);
        }

        /// <inheritdoc/>
        public float Axis(int axis) => axis switch
        {
            0 => MiddleVRWand.HorizontalAxis(),
            1 => MiddleVRWand.VerticalAxis(),
            _ => 0f,
        };

        /// <summary>
        /// Latch this frame's button states. <see cref="WandOperatorInput"/> calls this every frame, but
        /// reads self-sample too, so the wand still behaves correctly in a scene that has no source
        /// component in it.
        /// </summary>
        public void Sample() => EnsureSampled();

        void EnsureSampled()
        {
            int frame = m_FrameCounter();
            if (frame == m_SampledFrame)
                return;
            m_SampledFrame = frame;
            m_Edges.Sample(m_HeldProbe, m_ButtonCount);
        }
    }
}
