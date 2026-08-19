#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using MiddleVR;
#endif
using UnityEngine;

namespace Stadium3D.Operators
{
    /// <summary>
    /// A gamepad read <b>through MiddleVR</b>. <c>MVR.vrx</c> already loads
    /// <c>&lt;Driver Type="vrDriverDirectInput" /&gt;</c>, so a pad plugged into the CAVE host is
    /// enumerated as a <c>vrJoystick</c> with no <c>.vrx</c> edit at all — the same kernel that owns the
    /// wand also owns the pad, which is what keeps input coherent if the CAVE is ever driven from more
    /// than the single cluster node <c>MVR.vrx</c> declares today.
    ///
    /// The cost of that coherence is DirectInput's shape: on an XInput-class pad the D-pad usually arrives
    /// as a POV-hat axis and <b>both triggers are summed onto one axis</b>, so LT and RT cancel each other
    /// out. Give this source a <see cref="GamepadMap"/> filled in from <see cref="GamepadProbe"/> output,
    /// and prefer <see cref="UnityGamepadOperatorInput"/> where exact trigger semantics matter.
    /// </summary>
    [DefaultExecutionOrder(-1000)]   // sample before any consumer's Update reads an edge
    public class MvrJoystickOperatorInput : MonoBehaviour, IOperatorInput
    {
        [SerializeField, Tooltip("Which operator this device drives. The pad is the secondary operator.")]
        OperatorRole m_Role = OperatorRole.Secondary;

        [SerializeField, Tooltip("MiddleVR device name to bind to (e.g. 'DirectInputJoystick0'). Leave " +
            "empty to bind by index instead — run the GamepadProbe to see the names the kernel reports.")]
        string m_JoystickName = "";

        [SerializeField, Min(0), Tooltip("Joystick index to bind to when no name is given.")]
        int m_JoystickIndex;

        [SerializeField, Tooltip("Log once when the joystick is found, with its name and button/axis " +
            "counts. Worth leaving on — 'the pad does nothing' is otherwise silent.")]
        bool m_LogWhenFound = true;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        vrJoystick m_Joystick;
#endif
        readonly ButtonEdgeSampler m_Edges = new();
        System.Func<int, bool> m_HeldProbe;
        string m_DeviceName = "(no joystick)";
        int m_ButtonCount;
        int m_AxisCount;
        bool m_WasConnected;

        /// <summary>Which operator this device drives.</summary>
        public OperatorRole Role => m_Role;

        /// <inheritdoc/>
        public string DeviceName => m_DeviceName;

        /// <inheritdoc/>
        public int ButtonCount => m_ButtonCount;

        /// <inheritdoc/>
        public int AxisCount => m_AxisCount;

        /// <inheritdoc/>
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        public bool IsConnected => m_Joystick != null;
#else
        public bool IsConnected => false;
#endif

        void Awake() => m_HeldProbe = Held;

        void OnEnable() => OperatorInputRegistry.Register(m_Role, this);

        void OnDisable() => OperatorInputRegistry.Unregister(m_Role, this);

        void Update()
        {
            Resolve();
            m_Edges.Sample(m_HeldProbe, m_ButtonCount);
        }

        /// <inheritdoc/>
        public bool Held(int button)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
            return m_Joystick != null && button >= 0 && button < m_ButtonCount &&
                   m_Joystick.IsButtonPressed((uint)button);
#else
            return false;
#endif
        }

        /// <inheritdoc/>
        public bool PressedThisFrame(int button)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
            if (m_Joystick == null || button < 0 || button >= m_ButtonCount)
                return false;

            // Same idiom MiddleVRWand uses for the wand: the toggle flag marks a state change this frame,
            // and AND-ing with the pressed state isolates the rising edge from the falling one. Taken from
            // the kernel rather than sampled, so a press that starts and is still held between two Unity
            // frames is not missed.
            uint b = (uint)button;
            return m_Joystick.IsButtonToggled(b) && m_Joystick.IsButtonPressed(b);
#else
            return false;
#endif
        }

        /// <summary>
        /// Release edges are sampled rather than read from the kernel: the single-argument toggle flag is
        /// documented two different ways by MiddleVR (state-changed vs. toggled-to-pressed), and sampling
        /// is correct under either reading. Presses keep the kernel path, where the idiom is proven.
        /// </summary>
        public bool ReleasedThisFrame(int button) => m_Edges.ReleasedThisFrame(button);

        /// <inheritdoc/>
        public float Axis(int axis)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
            return m_Joystick != null && axis >= 0 && axis < m_AxisCount
                ? m_Joystick.GetAxisValue((uint)axis)
                : 0f;
#else
            return 0f;
#endif
        }

        /// <summary>
        /// Re-asks the kernel for the joystick <b>every frame</b> rather than caching the first one it
        /// hands out, so the source survives a late kernel start, a pad unplugged mid-session, and a
        /// replug.
        /// </summary>
        /// <remarks>
        /// Holding the first proxy would be worse than a stale connected flag: MiddleVR's DirectInput
        /// driver genuinely drops an unreadable device (<c>vrDriverDirectInput</c> exports
        /// <c>_RemoveJoystick</c> and calls <c>vrDeviceManager::DestroyDevice</c>), so a cached wrapper
        /// outlives the native object it points at and every <see cref="Held"/> call that frame reads
        /// through it. The re-ask is one lookup per frame, which is nothing next to that.
        ///
        /// Connectivity, not proxy identity, is what drives the reset: MiddleVR's bindings hand back a
        /// fresh managed wrapper per call, so comparing references would report a change every frame.
        /// A swap straight from one pad to another with no disconnected frame in between would therefore
        /// not clear stale edges — a case the CAVE's single wired pad cannot produce.
        /// </remarks>
        void Resolve()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
            m_Joystick = Find();

            bool nowConnected = m_Joystick != null;
            if (nowConnected == m_WasConnected)
                return;

            m_WasConnected = nowConnected;
            m_Edges.Reset();   // don't carry edges across a connect or a disconnect

            if (!nowConnected)
            {
                m_DeviceName = "(no joystick)";
                m_ButtonCount = 0;
                m_AxisCount = 0;
                if (m_LogWhenFound)
                    Debug.LogWarning($"[Operators] {m_Role} lost its MiddleVR joystick.", this);
                return;
            }

            m_DeviceName = m_Joystick.GetName();
            m_ButtonCount = (int)m_Joystick.GetButtonsNb();
            m_AxisCount = (int)m_Joystick.GetAxisNb();

            if (m_LogWhenFound)
                Debug.Log($"[Operators] {m_Role} bound to MiddleVR joystick '{m_DeviceName}' " +
                          $"({m_ButtonCount} buttons, {m_AxisCount} axes).", this);
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        vrJoystick Find()
        {
            var devices = MVR.DeviceMgr;
            if (devices == null)
                return null;

            if (!string.IsNullOrEmpty(m_JoystickName))
                return devices.GetJoystick(m_JoystickName);

            return m_JoystickIndex < devices.GetJoysticksNb()
                ? devices.GetJoystickByIndex((uint)m_JoystickIndex)
                : null;
        }
#endif
    }
}
