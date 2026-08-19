using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Stadium3D.Operators
{
    /// <summary>
    /// A gamepad read through Unity's Input System — the project already runs it exclusively
    /// (<c>activeInputHandler: 1</c>), and <c>ControllerTriggerInputSource</c> established the idiom of
    /// binding devices directly rather than through the rig's actions asset.
    ///
    /// This backend reports its controls in <see cref="PadControl"/> order <b>by construction</b>, so its
    /// mapping is the identity and there is nothing to probe or configure: Input System already knows
    /// which physical button is A on any pad it recognises. It also reports the two triggers separately,
    /// which the MiddleVR/DirectInput path may not (see <see cref="MvrJoystickOperatorInput"/>) — so this
    /// is the backend to prefer unless the CAVE grows past its single cluster node.
    /// </summary>
    /// <remarks>
    /// Works in the Editor with no MiddleVR kernel running, which is what makes the secondary-operator
    /// work testable at a desk instead of only in booked CAVE time.
    /// </remarks>
    [DefaultExecutionOrder(-1000)]   // resolve before any consumer's Update reads an edge
    public class UnityGamepadOperatorInput : MonoBehaviour, IOperatorInput
    {
        [SerializeField, Tooltip("Which operator this device drives. The pad is the secondary operator.")]
        OperatorRole m_Role = OperatorRole.Secondary;

        [SerializeField, Tooltip("Index into Gamepad.all to bind to. -1 binds to whichever pad was used " +
            "most recently (Gamepad.current), which is what you want with a single pad.")]
        int m_DeviceIndex = -1;

        [SerializeField, Tooltip("Log once when a pad is bound. Worth leaving on — 'the pad does " +
            "nothing' is otherwise silent.")]
        bool m_LogWhenFound = true;

        [SerializeField, Tooltip("Keep reading the pad while the application does not have focus. Required " +
            "here: MiddleVR opens its own always-on-top display window, so Unity is usually NOT the focused " +
            "application, and the Input System's default is to reset every control to zero and disable the " +
            "device the moment focus is lost. Turn off only if some other part of the project wants " +
            "focus-gated input.")]
        bool m_ReadWhileUnfocused = true;

        readonly ButtonControl[] m_Controls = new ButtonControl[PadControls.ButtonCount];
        Gamepad m_Pad;

        /// <summary>Which operator this device drives.</summary>
        public OperatorRole Role => m_Role;

        /// <inheritdoc/>
        public string DeviceName => m_Pad != null ? m_Pad.displayName : "(no gamepad)";

        /// <inheritdoc/>
        public bool IsConnected => m_Pad != null && m_Pad.added;

        /// <inheritdoc/>
        public int ButtonCount => PadControls.ButtonCount;

        /// <inheritdoc/>
        public int AxisCount => PadControls.AxisCount;

        void OnEnable()
        {
            if (m_ReadWhileUnfocused)
                KeepReadingWhileUnfocused();
            Resolve();
            OperatorInputRegistry.Register(m_Role, this);
        }

        /// <summary>
        /// Stop the Input System from silencing the pad when the application loses focus.
        /// </summary>
        /// <remarks>
        /// The default <see cref="InputSettings.BackgroundBehavior.ResetAndDisableNonBackgroundDevices"/>
        /// resets every control to its default and disables the device on focus loss. That is sensible for a
        /// windowed game and wrong for this one: MiddleVR runs its display in its own always-on-top window,
        /// so Unity is routinely not the focused application and the secondary operator's pad would go
        /// permanently dead — present and connected, reporting nothing. Player settings already have
        /// <c>Run In Background</c> on, which is the other half of the same requirement.
        /// </remarks>
        void KeepReadingWhileUnfocused()
        {
            var settings = InputSystem.settings;
            if (settings == null || settings.backgroundBehavior == InputSettings.BackgroundBehavior.IgnoreFocus)
                return;

            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            if (m_LogWhenFound)
                Debug.Log("[Operators] Input System background behaviour set to IgnoreFocus — the pad keeps " +
                          "reporting while MiddleVR's window holds focus.", this);
        }

        void OnDisable() => OperatorInputRegistry.Unregister(m_Role, this);

        void Update() => Resolve();

        /// <inheritdoc/>
        public bool Held(int button)
        {
            var control = Control(button);
            return control != null && control.isPressed;
        }

        /// <inheritdoc/>
        public bool PressedThisFrame(int button)
        {
            var control = Control(button);
            return control != null && control.wasPressedThisFrame;
        }

        /// <inheritdoc/>
        public bool ReleasedThisFrame(int button)
        {
            var control = Control(button);
            return control != null && control.wasReleasedThisFrame;
        }

        /// <inheritdoc/>
        public float Axis(int axis)
        {
            if (m_Pad == null)
                return 0f;

            // Canonical PadAxis order. Input System sticks are already up-positive and dead-zoned by the
            // device layout, so the mapping applies no correction on this backend.
            return (PadAxis)axis switch
            {
                PadAxis.LeftStickX => m_Pad.leftStick.x.ReadValue(),
                PadAxis.LeftStickY => m_Pad.leftStick.y.ReadValue(),
                PadAxis.RightStickX => m_Pad.rightStick.x.ReadValue(),
                PadAxis.RightStickY => m_Pad.rightStick.y.ReadValue(),
                PadAxis.LeftTrigger => m_Pad.leftTrigger.ReadValue(),
                PadAxis.RightTrigger => m_Pad.rightTrigger.ReadValue(),
                _ => 0f,
            };
        }

        ButtonControl Control(int button) =>
            m_Pad != null && button >= 0 && button < m_Controls.Length ? m_Controls[button] : null;

        // Rebinds whenever the pad changes, so unplugging and replugging (or swapping pads mid-session)
        // recovers on its own rather than leaving the secondary operator silently dead.
        void Resolve()
        {
            var pad = m_DeviceIndex < 0
                ? Gamepad.current
                : (m_DeviceIndex < Gamepad.all.Count ? Gamepad.all[m_DeviceIndex] : null);

            if (ReferenceEquals(pad, m_Pad))
                return;

            m_Pad = pad;
            CacheControls();

            if (!m_LogWhenFound)
                return;

            // Logged on every change, not just the first. Gamepad.current follows whichever pad most
            // recently sent input, so a silent re-bind is exactly what happens when a virtual pad (an
            // Oculus emulation bus, a streaming client) was current until the real one was first touched —
            // and that switch is the thing worth seeing when "nothing happens".
            if (m_Pad == null)
            {
                Debug.LogWarning($"[Operators] {m_Role} lost its gamepad.", this);
                return;
            }

            int count = Gamepad.all.Count;
            Debug.Log($"[Operators] {m_Role} bound to gamepad '{m_Pad.displayName}'" +
                      (count > 1 ? $" ({count} gamepads present — Gamepad.current follows the last one used)." : "."),
                      this);
        }

        // The canonical order is the contract this backend exists to satisfy — index i here IS
        // (PadControl)i, which is why this source needs no GamepadMap.
        void CacheControls()
        {
            if (m_Pad == null)
            {
                System.Array.Clear(m_Controls, 0, m_Controls.Length);
                return;
            }

            m_Controls[(int)PadControl.A] = m_Pad.buttonSouth;
            m_Controls[(int)PadControl.B] = m_Pad.buttonEast;
            m_Controls[(int)PadControl.X] = m_Pad.buttonWest;
            m_Controls[(int)PadControl.Y] = m_Pad.buttonNorth;
            m_Controls[(int)PadControl.LeftBumper] = m_Pad.leftShoulder;
            m_Controls[(int)PadControl.RightBumper] = m_Pad.rightShoulder;
            m_Controls[(int)PadControl.LeftTrigger] = m_Pad.leftTrigger;
            m_Controls[(int)PadControl.RightTrigger] = m_Pad.rightTrigger;
            m_Controls[(int)PadControl.LeftStickPress] = m_Pad.leftStickButton;
            m_Controls[(int)PadControl.RightStickPress] = m_Pad.rightStickButton;
            m_Controls[(int)PadControl.View] = m_Pad.selectButton;
            m_Controls[(int)PadControl.Menu] = m_Pad.startButton;
            m_Controls[(int)PadControl.DpadUp] = m_Pad.dpad.up;
            m_Controls[(int)PadControl.DpadDown] = m_Pad.dpad.down;
            m_Controls[(int)PadControl.DpadLeft] = m_Pad.dpad.left;
            m_Controls[(int)PadControl.DpadRight] = m_Pad.dpad.right;
        }
    }
}
