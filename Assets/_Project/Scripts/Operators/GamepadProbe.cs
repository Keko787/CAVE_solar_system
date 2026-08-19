#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using MiddleVR;
#endif
using System;
using System.Text;
using UnityEngine;
using Stadium3D.Vr;   // MiddleVRWand — the wand-side view of the same physical pad
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace Stadium3D.Operators
{
    /// <summary>
    /// TEMP diagnostic — the pad counterpart to <c>MiddleVRWandButtonProbe</c>. Drop it on any GameObject
    /// alongside a pad source, press each control, and read the Console to discover which raw index the
    /// device reports for it. Those indices are what a <see cref="GamepadMap"/> is filled in with.
    ///
    /// It reads through <see cref="OperatorInputRegistry"/> rather than a specific backend, so the same
    /// probe measures the Unity Input System pad at a desk and the MiddleVR/DirectInput pad in the CAVE —
    /// and the difference between the two readings is exactly what has to be measured, because an
    /// Xbox-class pad does not number its buttons the same way through both.
    /// </summary>
    /// <remarks>
    /// Two things to look for specifically, both of which change the control scheme if true:
    /// whether the D-pad appears as four buttons or as one axis whose value jumps as you rotate it (a POV
    /// hat), and whether the two triggers move one axis each or fight over a single shared axis.
    /// Remove or disable this component when the mapping is settled.
    /// </remarks>
    public class GamepadProbe : MonoBehaviour
    {
        [SerializeField, Tooltip("Which operator's device to probe.")]
        OperatorRole m_Role = OperatorRole.Secondary;

        [SerializeField, Tooltip("Log axis movement as well as button presses. Needed to find the stick, " +
            "trigger and POV-hat axis indices.")]
        bool m_LogAxes = true;

        [SerializeField, Min(0.001f), Tooltip("Log an axis only once it has moved this far since its last " +
            "logged value, so a resting stick doesn't flood the Console.")]
        float m_AxisChangeThreshold = 0.15f;

        [SerializeField, Tooltip("Optional: a map under test. When set, the probe ALSO reports which " +
            "canonical control each press resolves to, which is how you verify a finished map instead of " +
            "just discovering raw indices.")]
        GamepadMap m_VerifyMap;

        [Header("Diagnostics")]
        [SerializeField, Tooltip("Periodically log what the Input System itself can see, read straight off " +
            "the device rather than through the operator layer. This is what tells you whether 'nothing " +
            "happens' means the pad is not reaching Unity at all, or is reaching it and being mapped wrong.")]
        bool m_LogHeartbeat = true;

        [SerializeField, Min(0.5f)] float m_HeartbeatSeconds = 2f;

        [SerializeField, Tooltip("Print every button press read STRAIGHT off the device, naming the control " +
            "rather than an index. This bypasses the whole operator layer — the map, the canonical control " +
            "space, the registry — so if these lines appear and the mapped ones do not, the fault is mine; " +
            "if neither appears, the pad is not reaching Unity.")]
        bool m_LogRawControls = true;

        IOperatorInput m_Source;
        PadOperator m_Pad;
        float[] m_LastAxis;
        bool m_AnnouncedDevice;
        bool m_AnnouncedMissing;
        float m_NextHeartbeat;
        readonly StringBuilder m_Heartbeat = new();

        Action<InputEventPtr, InputDevice> m_OnEvent;
        Action<InputDevice, InputDeviceChange> m_OnDeviceChange;
        int m_PadEventsSinceHeartbeat;

        // Hooked at the lowest level the Input System exposes. A device can be present, bound and still
        // silent — the count below is the only thing that distinguishes "Unity is receiving input from this
        // pad" from "Unity knows the pad exists". Device-change lines catch it being disabled underneath us.
        void OnEnable()
        {
            m_OnEvent ??= (_, device) =>
            {
                if (device is Gamepad)
                    m_PadEventsSinceHeartbeat++;
            };
            m_OnDeviceChange ??= (device, change) =>
            {
                if (device is Gamepad)
                    Debug.Log($"[GamepadProbe] device '{device.displayName}' → {change}", this);
            };

            InputSystem.onEvent += m_OnEvent;
            InputSystem.onDeviceChange += m_OnDeviceChange;
        }

        void OnDisable()
        {
            InputSystem.onEvent -= m_OnEvent;
            InputSystem.onDeviceChange -= m_OnDeviceChange;
        }

        void Update()
        {
            // Both run before the early-outs: they are most useful exactly when nothing else fires.
            LogHeartbeat();
            LogRawControls();

            var source = OperatorInputRegistry.Get(m_Role);
            if (source == null || !source.IsConnected)
            {
                if (!m_AnnouncedMissing)
                {
                    m_AnnouncedMissing = true;
                    Debug.LogWarning($"[GamepadProbe] No connected input source for {m_Role}. Add a " +
                                     "UnityGamepadOperatorInput (desk) or MvrJoystickOperatorInput (CAVE) " +
                                     "to the scene and set its role.", this);
                }
                m_AnnouncedDevice = false;
                return;
            }

            m_AnnouncedMissing = false;
            if (!ReferenceEquals(source, m_Source))
            {
                m_Source = source;
                m_Pad = null;
                m_LastAxis = null;
                m_AnnouncedDevice = false;
            }

            if (!m_AnnouncedDevice)
            {
                m_AnnouncedDevice = true;
                LogSummary();
            }

            LogButtons();
            if (m_LogAxes)
                LogAxes();
            if (m_VerifyMap != null)
                LogCanonical();
        }

        /// <summary>Log what the bound device says about itself. Also available from the inspector.</summary>
        [ContextMenu("Log Device Summary")]
        public void LogSummary()
        {
            var source = m_Source ?? OperatorInputRegistry.Get(m_Role);
            if (source == null)
            {
                Debug.LogWarning($"[GamepadProbe] No input source registered for {m_Role}.", this);
                return;
            }

            Debug.Log($"[GamepadProbe] {m_Role}: '{source.DeviceName}' — {source.ButtonCount} buttons, " +
                      $"{source.AxisCount} axes. Press each control and read the indices below.", this);
        }

        /// <summary>
        /// What the Input System can see, read directly off the device — deliberately bypassing
        /// <see cref="IOperatorInput"/>, <see cref="PadMapping"/> and everything else this feature added, so
        /// it can prove or clear the whole operator layer at once.
        /// </summary>
        /// <remarks>
        /// Three things it answers. Whether the application has focus, because the Editor stops delivering
        /// input to a Game view that does not. How many gamepads exist and which one is <c>current</c> —
        /// virtual-pad drivers (an Oculus emulation bus, a streaming client) enumerate as a second Xbox pad,
        /// and if a silent phantom is <c>current</c> then everything binds happily and nothing ever moves.
        /// And whether <c>lastUpdateTime</c> advances between heartbeats, which is the difference between a
        /// device that is sending events and one that is merely present.
        /// </remarks>
        void LogHeartbeat()
        {
            if (!m_LogHeartbeat || Time.unscaledTime < m_NextHeartbeat)
                return;
            m_NextHeartbeat = Time.unscaledTime + Mathf.Max(0.5f, m_HeartbeatSeconds);

            var pads = Gamepad.all;
            m_Heartbeat.Clear();
            m_Heartbeat.Append("[GamepadProbe] heartbeat — focused: ").Append(Application.isFocused)
                       .Append("; padEvents since last: ").Append(m_PadEventsSinceHeartbeat)
                       .Append("; background: ").Append(InputSystem.settings.backgroundBehavior)
#if UNITY_EDITOR
                       .Append("; editorPlayMode: ").Append(InputSystem.settings.editorInputBehaviorInPlayMode)
#endif
                       .Append("; gamepads: ").Append(pads.Count);
            m_PadEventsSinceHeartbeat = 0;

            for (int i = 0; i < pads.Count; i++)
            {
                var pad = pads[i];
                m_Heartbeat.Append("\n  [").Append(i).Append("] '").Append(pad.displayName).Append('\'')
                           .Append(pad == Gamepad.current ? " (current)" : "")
                           .Append(" added=").Append(pad.added)
                           .Append(" enabled=").Append(pad.enabled)
                           .Append(" lastUpdate=").Append(pad.lastUpdateTime.ToString("F2"))
                           .Append(" A=").Append(pad.buttonSouth.isPressed)
                           .Append(" leftStick=").Append(pad.leftStick.ReadValue().ToString("F2"));
            }

            if (pads.Count == 0)
                m_Heartbeat.Append("\n  Unity sees no gamepad at all — this is not a mapping problem.");

            AppendMiddleVrView();
            Debug.Log(m_Heartbeat.ToString(), this);
        }

        /// <summary>
        /// Every button press read straight off <c>Gamepad.current</c>, named rather than numbered. This
        /// deliberately shares nothing with the mapped path below — not the registry, not the source, not
        /// the map — so the two together localise a fault: raw lines but no mapped lines means the operator
        /// layer is wrong, neither means the pad is not reaching Unity at all.
        /// </summary>
        /// <summary>
        /// The same physical pad as MiddleVR sees it, read straight off <c>MVR.DeviceMgr</c>.
        /// </summary>
        /// <remarks>
        /// This is the decisive comparison. MiddleVR's DirectInput driver uses the same API Windows'
        /// own joystick control panel does — so if the pad works there and Unity's XInput-backed
        /// <c>Gamepad</c> reports a frozen <c>lastUpdate</c>, this line says whether the input is reaching
        /// the process at all on the other path. Joysticks visible with a button pressed means switch the
        /// backend and the problem is solved; nothing here either means no API in this process can see the
        /// pad, and the fault is above Unity entirely.
        /// </remarks>
        void AppendMiddleVrView()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
            var devices = MVR.DeviceMgr;
            if (devices == null)
            {
                m_Heartbeat.Append("\n  MiddleVR: kernel not up.");
                return;
            }

            uint count = devices.GetJoysticksNb();
            m_Heartbeat.Append("\n  MiddleVR: joysticks=").Append(count);

            for (uint i = 0; i < count && i < 4; i++)
            {
                var joy = devices.GetJoystickByIndex(i);
                if (joy == null)
                    continue;

                uint buttons = joy.GetButtonsNb();
                int pressed = -1;
                for (uint b = 0; b < buttons; b++)
                {
                    if (!joy.IsButtonPressed(b))
                        continue;
                    pressed = (int)b;
                    break;
                }

                m_Heartbeat.Append("\n    [").Append(i).Append("] '").Append(joy.GetName()).Append('\'')
                           .Append(" buttons=").Append(buttons)
                           .Append(" axes=").Append(joy.GetAxisNb())
                           .Append(" pressedNow=").Append(pressed >= 0 ? pressed.ToString() : "none");
            }

            // The wand view of the same device: on a desk with no ART hardware, MiddleVR routinely presents
            // the pad as Wand0, so a button showing here is the pad arriving on the wand path.
            int wandPressed = -1;
            for (int b = 0; b < WandInput.DefaultButtonCount; b++)
            {
                if (!MiddleVRWand.Held(b))
                    continue;
                wandPressed = b;
                break;
            }
            m_Heartbeat.Append("\n  Wand: available=").Append(MiddleVRWand.IsAvailable)
                       .Append(" pressedNow=").Append(wandPressed >= 0 ? wandPressed.ToString() : "none")
                       .Append(" axes=(").Append(MiddleVRWand.HorizontalAxis().ToString("F2")).Append(", ")
                       .Append(MiddleVRWand.VerticalAxis().ToString("F2")).Append(')');
#endif
        }

        void LogRawControls()
        {
            if (!m_LogRawControls)
                return;

            var pad = Gamepad.current;
            if (pad == null)
                return;

            var controls = pad.allControls;
            for (int i = 0; i < controls.Count; i++)
            {
                if (!(controls[i] is ButtonControl button))
                    continue;
                if (button.wasPressedThisFrame)
                    Debug.Log($"[GamepadProbe] RAW '{button.name}' PRESSED", this);
                else if (button.wasReleasedThisFrame)
                    Debug.Log($"[GamepadProbe] RAW '{button.name}' released", this);
            }
        }

        void LogButtons()
        {
            for (int i = 0; i < m_Source.ButtonCount; i++)
            {
                if (m_Source.PressedThisFrame(i))
                    Debug.Log($"[GamepadProbe] button {i} PRESSED", this);
                else if (m_Source.ReleasedThisFrame(i))
                    Debug.Log($"[GamepadProbe] button {i} released", this);
            }
        }

        void LogAxes()
        {
            int count = m_Source.AxisCount;
            if (m_LastAxis == null || m_LastAxis.Length != count)
            {
                m_LastAxis = new float[count];
                for (int i = 0; i < count; i++)
                    m_LastAxis[i] = m_Source.Axis(i);
                return;   // first frame establishes the resting values rather than logging them all
            }

            for (int i = 0; i < count; i++)
            {
                float value = m_Source.Axis(i);
                if (Mathf.Abs(value - m_LastAxis[i]) < m_AxisChangeThreshold)
                    continue;
                m_LastAxis[i] = value;
                Debug.Log($"[GamepadProbe] axis {i} = {value:F3}", this);
            }
        }

        void LogCanonical()
        {
            m_Pad ??= new PadOperator(m_Source, m_VerifyMap.Mapping);
            for (int i = 0; i < PadControls.ButtonCount; i++)
            {
                var control = (PadControl)i;
                if (m_Pad.PressedThisFrame(control))
                    Debug.Log($"[GamepadProbe] map says: {control} PRESSED", this);
            }
        }
    }
}
