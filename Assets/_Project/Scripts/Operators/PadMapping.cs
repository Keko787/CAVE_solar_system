using System;
using UnityEngine;

namespace Stadium3D.Operators
{
    /// <summary>Where a pad's D-pad actually comes from on the wire.</summary>
    public enum PadDpadSource
    {
        /// <summary>Four ordinary buttons, bound like any other control.</summary>
        Buttons = 0,
        /// <summary>A single POV-hat axis encoding a direction angle — the usual DirectInput shape.</summary>
        HatAxis = 1,
    }

    /// <summary>How the two analog triggers are reported.</summary>
    public enum PadTriggerSource
    {
        /// <summary>One axis each, both readable at once. What XInput (and so Unity's Input System) gives.</summary>
        SeparateAxes = 0,
        /// <summary>
        /// Both triggers summed onto ONE axis — LT drives it one way, RT the other, and pressing both
        /// cancels to zero. This is the classic DirectInput-over-XInput shape, and it is why any control
        /// scheme that wants LT and RT independently must not rely on the DirectInput path.
        /// </summary>
        CombinedAxis = 1,
    }

    /// <summary>
    /// Translation from one device's raw button/axis indices into the project's canonical
    /// <see cref="PadControl"/> / <see cref="PadAxis"/> space. Serializable plain C# held inside a
    /// config asset — the same shape as <c>CaveRegion</c> inside <c>CaveRegionMap</c> — so the mapping
    /// maths is unit-testable with no asset, no device, and no scene.
    ///
    /// Indices are <b>measured, not assumed</b>: an Xbox pad enumerated through MiddleVR's DirectInput
    /// driver does not use the same numbering as the same pad through XInput, and the numbering differs
    /// again between pad models. Run the <see cref="GamepadProbe"/> in the target environment and fill
    /// these in from what it logs.
    /// </summary>
    [Serializable]
    public class PadMapping
    {
        /// <summary>One canonical axis's binding onto a raw device axis.</summary>
        [Serializable]
        public struct AxisBinding
        {
            [Tooltip("Raw device axis index. -1 = this canonical axis is unbound and reads 0.")]
            public int index;

            [Tooltip("Multiplier applied after the dead zone. Use -1 to flip an axis whose device reports " +
                     "down-positive (Y axes are up-positive in canonical space).")]
            public float scale;

            [Tooltip("Magnitude below which the axis reads 0. Values above it are rescaled so travel " +
                     "still ramps from 0, avoiding a jump at the dead-zone edge.")]
            public float deadZone;

            /// <summary>An unbound binding — reads 0 whatever the device does.</summary>
            public static AxisBinding None => new() { index = -1, scale = 1f, deadZone = 0f };

            /// <summary>A binding with the project's default stick dead zone.</summary>
            public static AxisBinding At(int index, float scale = 1f, float deadZone = DefaultStickDeadZone) =>
                new() { index = index, scale = scale, deadZone = deadZone };
        }

        /// <summary>
        /// Default stick dead zone. Matches the 0.3 the <c>.vrx</c> applies to the Flystick axis, so a
        /// stick push means the same thing to <c>FrontWallPortal</c> whichever operator does it.
        /// </summary>
        public const float DefaultStickDeadZone = 0.3f;

        /// <summary>Travel at which an analog trigger counts as a digital press.</summary>
        public const float DefaultTriggerPressPoint = 0.5f;

        [SerializeField, Tooltip("Raw button index per canonical PadControl, in PadControl order. " +
            "-1 = unbound. Filled in from what the GamepadProbe logs on the target machine.")]
        int[] m_Buttons = new int[PadControls.ButtonCount];

        [SerializeField, Tooltip("Raw axis binding per canonical PadAxis, in PadAxis order.")]
        AxisBinding[] m_Axes = new AxisBinding[PadControls.AxisCount];

        [SerializeField, Tooltip("Whether the D-pad arrives as four buttons or as one POV-hat axis.")]
        PadDpadSource m_DpadSource = PadDpadSource.Buttons;

        [SerializeField, Tooltip("Raw axis index of the POV hat, when the D-pad is a hat.")]
        int m_HatAxis = -1;

        [SerializeField, Tooltip("Hat raw values BELOW this mean 'centred, nothing pressed'. DirectInput " +
            "hats park at a negative sentinel outside their angular range, so this sits below that range " +
            "and must not overlap it — otherwise 'up' and 'centred' become the same reading.")]
        float m_HatCentredBelow;

        [SerializeField, Tooltip("Hat raw value that means 0 degrees (up).")]
        float m_HatMin;

        [SerializeField, Tooltip("Hat raw value that means 360 degrees (back to up). Angles run clockwise: " +
            "90 = right, 180 = down, 270 = left.")]
        float m_HatMax = 1f;

        [SerializeField, Tooltip("Whether the triggers are separate axes or summed onto one shared axis.")]
        PadTriggerSource m_TriggerSource = PadTriggerSource.SeparateAxes;

        [SerializeField, Tooltip("The shared trigger axis, when both triggers are summed onto one. " +
            "Positive travel is read as LEFT trigger unless the sign is flipped below.")]
        int m_CombinedTriggerAxis = -1;

        [SerializeField, Tooltip("Flip if the shared trigger axis reads positive for the RIGHT trigger.")]
        bool m_CombinedTriggerInverted;

        [SerializeField, Range(0f, 1f), Tooltip("Travel at which an analog trigger counts as pressed.")]
        float m_TriggerPressPoint = DefaultTriggerPressPoint;

        /// <summary>How the D-pad is reported by this device.</summary>
        public PadDpadSource DpadSource => m_DpadSource;

        /// <summary>How the triggers are reported by this device.</summary>
        public PadTriggerSource TriggerSource => m_TriggerSource;

        /// <summary>Travel at which an analog trigger reads as a digital press.</summary>
        public float TriggerPressPoint => m_TriggerPressPoint;

        /// <summary>
        /// True when this device cannot report both triggers at once — callers that want LT and RT
        /// independently (a held modifier plus a click, say) must not use such a device for that gesture.
        /// </summary>
        public bool TriggersAreExclusive => m_TriggerSource == PadTriggerSource.CombinedAxis;

        /// <summary>Raw button index bound to a canonical control, or -1 if unbound.</summary>
        public int Button(PadControl control)
        {
            int i = (int)control;
            return m_Buttons != null && i >= 0 && i < m_Buttons.Length ? m_Buttons[i] : -1;
        }

        /// <summary>Raw axis binding for a canonical axis (an unbound binding if none is set).</summary>
        public AxisBinding Axis(PadAxis axis)
        {
            int i = (int)axis;
            return m_Axes != null && i >= 0 && i < m_Axes.Length ? m_Axes[i] : AxisBinding.None;
        }

        /// <summary>Bind a canonical control to a raw button index (-1 unbinds). For probes and tests.</summary>
        public void SetButton(PadControl control, int rawIndex)
        {
            EnsureArrays();
            m_Buttons[(int)control] = rawIndex;
        }

        /// <summary>Bind a canonical axis to a raw axis. For probes and tests.</summary>
        public void SetAxis(PadAxis axis, AxisBinding binding)
        {
            EnsureArrays();
            m_Axes[(int)axis] = binding;
        }

        /// <summary>
        /// Point the D-pad at a POV-hat axis instead of four buttons. Defaults assume the common
        /// normalised shape — a negative sentinel when centred, and 0..1 running clockwise from up —
        /// which the probe should confirm before it is trusted.
        /// </summary>
        public void SetHat(int rawAxis, float centredBelow = 0f, float min = 0f, float max = 1f)
        {
            m_DpadSource = PadDpadSource.HatAxis;
            m_HatAxis = rawAxis;
            m_HatCentredBelow = centredBelow;
            m_HatMin = min;
            m_HatMax = max;
        }

        /// <summary>Declare that both triggers share one axis (the DirectInput-over-XInput case).</summary>
        public void SetCombinedTriggerAxis(int rawAxis, bool inverted = false)
        {
            m_TriggerSource = PadTriggerSource.CombinedAxis;
            m_CombinedTriggerAxis = rawAxis;
            m_CombinedTriggerInverted = inverted;
        }

        /// <summary>Read a canonical axis from a device, applying dead zone and scale.</summary>
        public float ReadAxis(IOperatorInput device, PadAxis axis)
        {
            if (device == null)
                return 0f;

            if (m_TriggerSource == PadTriggerSource.CombinedAxis &&
                (axis == PadAxis.LeftTrigger || axis == PadAxis.RightTrigger))
                return ReadCombinedTrigger(device, axis);

            var binding = Axis(axis);
            return ApplyDeadZone(device.Axis(binding.index), binding);
        }

        /// <summary>
        /// Read a canonical control's level state from a device. Handles the two shapes a control can
        /// arrive in that are not simply "a button": hat-driven D-pad directions and axis-driven triggers.
        /// </summary>
        public bool ReadHeld(IOperatorInput device, PadControl control)
        {
            if (device == null)
                return false;

            if (m_DpadSource == PadDpadSource.HatAxis && TryHatDirection(control, out float centreDegrees))
                return HatMatches(device, centreDegrees);

            int button = Button(control);
            if (button >= 0)
                return device.Held(button);

            // No button bound: an analog trigger can still act as one.
            if (control == PadControl.LeftTrigger)
                return ReadAxis(device, PadAxis.LeftTrigger) >= m_TriggerPressPoint;
            if (control == PadControl.RightTrigger)
                return ReadAxis(device, PadAxis.RightTrigger) >= m_TriggerPressPoint;

            return false;
        }

        /// <summary>True if this control reads from a plain device button (so edges come straight from it).</summary>
        public bool IsButtonBacked(PadControl control)
        {
            if (m_DpadSource == PadDpadSource.HatAxis && TryHatDirection(control, out _))
                return false;
            return Button(control) >= 0;
        }

        // --- Defaults -----------------------------------------------------------------------

        /// <summary>
        /// The identity mapping: canonical index N is raw index N. This is what
        /// <see cref="UnityGamepadOperatorInput"/> needs, because it deliberately reports its controls in
        /// canonical order — Unity's Input System already names every Xbox control, so there is nothing
        /// to discover and nothing to configure.
        /// </summary>
        public static PadMapping UnityDefault()
        {
            var map = new PadMapping();
            map.EnsureArrays();
            for (int i = 0; i < PadControls.ButtonCount; i++)
                map.m_Buttons[i] = i;

            map.SetAxis(PadAxis.LeftStickX, AxisBinding.At((int)PadAxis.LeftStickX));
            map.SetAxis(PadAxis.LeftStickY, AxisBinding.At((int)PadAxis.LeftStickY));
            map.SetAxis(PadAxis.RightStickX, AxisBinding.At((int)PadAxis.RightStickX));
            map.SetAxis(PadAxis.RightStickY, AxisBinding.At((int)PadAxis.RightStickY));
            map.SetAxis(PadAxis.LeftTrigger, AxisBinding.At((int)PadAxis.LeftTrigger, 1f, 0f));
            map.SetAxis(PadAxis.RightTrigger, AxisBinding.At((int)PadAxis.RightTrigger, 1f, 0f));

            map.m_DpadSource = PadDpadSource.Buttons;
            map.m_TriggerSource = PadTriggerSource.SeparateAxes;
            return map;
        }

        /// <summary>
        /// A <b>starting guess</b> for an Xbox-layout pad seen through DirectInput — the layout Windows
        /// most commonly presents for an XInput device, which is what MiddleVR's <c>vrDriverDirectInput</c>
        /// will enumerate. Two things are notoriously wrong on some pads and MUST be confirmed with the
        /// probe before trusting them: the D-pad arrives as a POV hat rather than buttons, and both
        /// triggers are summed onto a single axis (see <see cref="PadTriggerSource.CombinedAxis"/>).
        /// </summary>
        public static PadMapping DirectInputXboxDefault()
        {
            var map = new PadMapping();
            map.EnsureArrays();

            map.SetButton(PadControl.A, 0);
            map.SetButton(PadControl.B, 1);
            map.SetButton(PadControl.X, 2);
            map.SetButton(PadControl.Y, 3);
            map.SetButton(PadControl.LeftBumper, 4);
            map.SetButton(PadControl.RightBumper, 5);
            map.SetButton(PadControl.View, 6);
            map.SetButton(PadControl.Menu, 7);
            map.SetButton(PadControl.LeftStickPress, 8);
            map.SetButton(PadControl.RightStickPress, 9);
            // Triggers are not buttons here — they come off the shared axis below.
            map.SetButton(PadControl.LeftTrigger, -1);
            map.SetButton(PadControl.RightTrigger, -1);
            map.SetButton(PadControl.DpadUp, -1);
            map.SetButton(PadControl.DpadDown, -1);
            map.SetButton(PadControl.DpadLeft, -1);
            map.SetButton(PadControl.DpadRight, -1);

            map.SetAxis(PadAxis.LeftStickX, AxisBinding.At(0));
            map.SetAxis(PadAxis.LeftStickY, AxisBinding.At(1, -1f));   // DirectInput Y is down-positive
            map.SetAxis(PadAxis.RightStickX, AxisBinding.At(3));
            map.SetAxis(PadAxis.RightStickY, AxisBinding.At(4, -1f));

            map.SetCombinedTriggerAxis(2);
            map.SetHat(5);
            return map;
        }

        // --- Internals ----------------------------------------------------------------------

        void EnsureArrays()
        {
            if (m_Buttons == null || m_Buttons.Length != PadControls.ButtonCount)
            {
                var buttons = new int[PadControls.ButtonCount];
                for (int i = 0; i < buttons.Length; i++)
                    buttons[i] = m_Buttons != null && i < m_Buttons.Length ? m_Buttons[i] : -1;
                m_Buttons = buttons;
            }

            if (m_Axes == null || m_Axes.Length != PadControls.AxisCount)
            {
                var axes = new AxisBinding[PadControls.AxisCount];
                for (int i = 0; i < axes.Length; i++)
                    axes[i] = m_Axes != null && i < m_Axes.Length ? m_Axes[i] : AxisBinding.None;
                m_Axes = axes;
            }
        }

        float ReadCombinedTrigger(IOperatorInput device, PadAxis axis)
        {
            float raw = device.Axis(m_CombinedTriggerAxis);
            bool wantPositive = (axis == PadAxis.LeftTrigger) != m_CombinedTriggerInverted;
            float travel = wantPositive ? raw : -raw;
            return travel > 0f ? Mathf.Min(travel, 1f) : 0f;
        }

        static float ApplyDeadZone(float raw, AxisBinding binding)
        {
            if (binding.index < 0)
                return 0f;

            float scaled = raw * (Mathf.Approximately(binding.scale, 0f) ? 1f : binding.scale);
            float dead = Mathf.Clamp01(Mathf.Abs(binding.deadZone));
            if (dead <= 0f)
                return Mathf.Clamp(scaled, -1f, 1f);

            float magnitude = Mathf.Abs(scaled);
            if (magnitude <= dead)
                return 0f;

            // Rescale so travel ramps from 0 at the dead-zone edge instead of jumping to `dead`.
            float ramped = (magnitude - dead) / (1f - dead);
            return Mathf.Clamp01(ramped) * Mathf.Sign(scaled);
        }

        // Canonical hat angles, clockwise from up. Diagonals are covered by the +/-67.5 degree window
        // in HatMatches, so a north-east hat press lights both Up and Right, like a real D-pad.
        static bool TryHatDirection(PadControl control, out float centreDegrees)
        {
            switch (control)
            {
                case PadControl.DpadUp: centreDegrees = 0f; return true;
                case PadControl.DpadRight: centreDegrees = 90f; return true;
                case PadControl.DpadDown: centreDegrees = 180f; return true;
                case PadControl.DpadLeft: centreDegrees = 270f; return true;
                default: centreDegrees = 0f; return false;
            }
        }

        bool HatMatches(IOperatorInput device, float centreDegrees)
        {
            if (m_HatAxis < 0)
                return false;

            float raw = device.Axis(m_HatAxis);
            if (raw < m_HatCentredBelow)
                return false;

            float span = m_HatMax - m_HatMin;
            if (Mathf.Approximately(span, 0f))
                return false;

            float degrees = Mathf.Repeat((raw - m_HatMin) / span * 360f, 360f);
            float delta = Mathf.Abs(Mathf.DeltaAngle(degrees, centreDegrees));
            return delta < 67.5f;   // half of a 135-degree window: cardinals plus both adjacent diagonals
        }
    }
}
