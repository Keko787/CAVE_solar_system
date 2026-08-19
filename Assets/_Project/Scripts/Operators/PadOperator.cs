using System;
using UnityEngine;

namespace Stadium3D.Operators
{
    /// <summary>
    /// Reads a pad in <em>meaning</em> rather than in indices: <c>Pressed(PadControl.A)</c> instead of
    /// <c>PressedThisFrame(0)</c>. Wraps an <see cref="IOperatorInput"/> device with the
    /// <see cref="PadMapping"/> that translates it, and is what every gamepad consumer from G3 onward
    /// talks to.
    ///
    /// Edges are derived here, for all controls, from one per-frame sample — not taken from the device —
    /// because two of the sixteen canonical controls may not be device buttons at all (a POV-hat D-pad
    /// direction, an analog trigger acting as a press). Sampling them all in one place means a hat
    /// direction and the A button behave identically to a consumer, which is the whole promise of the
    /// canonical space.
    /// </summary>
    /// <remarks>
    /// Plain C# with an injectable frame counter, so the edge and mapping behaviour is unit-testable
    /// without a device, a scene, or play mode (EditMode tests never advance <c>Time.frameCount</c>).
    /// </remarks>
    public sealed class PadOperator
    {
        readonly ButtonEdgeSampler m_Edges = new();
        readonly Func<int, bool> m_HeldProbe;
        readonly Func<int> m_FrameCounter;

        IOperatorInput m_Device;
        PadMapping m_Mapping;
        int m_SampledFrame = int.MinValue;

        /// <summary>
        /// Wrap a device. A null <paramref name="mapping"/> means the identity mapping, which is correct
        /// for <see cref="UnityGamepadOperatorInput"/> and wrong for anything enumerated by MiddleVR —
        /// give that one a <see cref="GamepadMap"/> asset. A null <paramref name="device"/> is legal and
        /// reads as "nothing pressed", so a consumer can be built before its pad is registered.
        /// </summary>
        /// <param name="frameCounter">
        /// Source of the current frame number; defaults to <c>Time.frameCount</c>. Tests inject their own
        /// so they can step frames deterministically.
        /// </param>
        public PadOperator(IOperatorInput device, PadMapping mapping = null, Func<int> frameCounter = null)
        {
            m_Device = device;
            m_Mapping = mapping ?? PadMapping.UnityDefault();
            m_FrameCounter = frameCounter ?? (() => Time.frameCount);
            m_HeldProbe = ReadHeldRaw;   // cached once; re-creating it per frame would allocate every frame
        }

        /// <summary>The device being read, or null.</summary>
        public IOperatorInput Device => m_Device;

        /// <summary>The translation in force.</summary>
        public PadMapping Mapping => m_Mapping;

        /// <summary>False when no pad is present — consumers should show their cursor only when true.</summary>
        public bool IsConnected => m_Device != null && m_Device.IsConnected;

        /// <summary>
        /// True when this pad cannot report both triggers at once (see
        /// <see cref="PadTriggerSource.CombinedAxis"/>). A gesture that needs LT held while RT clicks must
        /// check this and fall back to another binding.
        /// </summary>
        public bool TriggersAreExclusive => m_Mapping.TriggersAreExclusive;

        /// <summary>Swap the device (hot-plug, or a role handed to a different pad). Clears stale edges.</summary>
        public void SetDevice(IOperatorInput device)
        {
            if (ReferenceEquals(m_Device, device))
                return;
            m_Device = device;
            ResetEdges();
        }

        /// <summary>Swap the translation. Clears stale edges, since control identities just changed.</summary>
        public void SetMapping(PadMapping mapping)
        {
            var next = mapping ?? PadMapping.UnityDefault();
            if (ReferenceEquals(m_Mapping, next))
                return;
            m_Mapping = next;
            ResetEdges();
        }

        /// <summary>True while the control is down.</summary>
        public bool Held(PadControl control)
        {
            EnsureSampled();
            return m_Edges.Held((int)control);
        }

        /// <summary>True only on the frame the control goes down.</summary>
        public bool PressedThisFrame(PadControl control)
        {
            EnsureSampled();
            return m_Edges.PressedThisFrame((int)control);
        }

        /// <summary>True only on the frame the control comes up.</summary>
        public bool ReleasedThisFrame(PadControl control)
        {
            EnsureSampled();
            return m_Edges.ReleasedThisFrame((int)control);
        }

        /// <summary>Mapped axis value — dead zone applied, sign corrected, normally in [-1, 1].</summary>
        public float Axis(PadAxis axis) => m_Mapping.ReadAxis(m_Device, axis);

        /// <summary>Left stick as a vector, Y up-positive.</summary>
        public Vector2 LeftStick => new(Axis(PadAxis.LeftStickX), Axis(PadAxis.LeftStickY));

        /// <summary>Right stick as a vector, Y up-positive.</summary>
        public Vector2 RightStick => new(Axis(PadAxis.RightStickX), Axis(PadAxis.RightStickY));

        /// <summary>Forget latched state — after a device swap, or when a consumer is re-enabled.</summary>
        public void ResetEdges()
        {
            m_Edges.Reset();
            m_SampledFrame = int.MinValue;
        }

        // One sample per frame, taken lazily on first read rather than pushed by an Update, so no consumer
        // can read edges before the sample has happened and no execution order has to be maintained.
        void EnsureSampled()
        {
            int frame = m_FrameCounter();
            if (frame == m_SampledFrame)
                return;
            m_SampledFrame = frame;
            m_Edges.Sample(m_HeldProbe, PadControls.ButtonCount);
        }

        bool ReadHeldRaw(int canonicalIndex) => m_Mapping.ReadHeld(m_Device, (PadControl)canonicalIndex);
    }
}
