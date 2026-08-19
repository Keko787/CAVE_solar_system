using Stadium3D.Operators;

namespace Stadium3D.Operators.Tests
{
    /// <summary>
    /// A hand-driven <see cref="IOperatorInput"/> for the operator-input tests: no device, no kernel, no
    /// play mode. Shared by the mapping, pad-operator and registry fixtures, which is why it lives in its
    /// own file rather than nested in one of them.
    /// </summary>
    /// <remarks>
    /// Reports no edges of its own — <see cref="PadOperator"/> derives every edge from level state, so a
    /// fake that faked edges too would test the fake instead of the code.
    /// </remarks>
    public sealed class OperatorTestInput : IOperatorInput
    {
        readonly bool[] m_Buttons;
        readonly float[] m_Axes;

        public OperatorTestInput(int buttons = 16, int axes = 8)
        {
            m_Buttons = new bool[buttons];
            m_Axes = new float[axes];
        }

        public string DeviceName { get; set; } = "TestPad";
        public bool IsConnected { get; set; } = true;
        public int ButtonCount => m_Buttons.Length;
        public int AxisCount => m_Axes.Length;

        public bool Held(int button) => button >= 0 && button < m_Buttons.Length && m_Buttons[button];
        public bool PressedThisFrame(int button) => false;
        public bool ReleasedThisFrame(int button) => false;
        public float Axis(int axis) => axis >= 0 && axis < m_Axes.Length ? m_Axes[axis] : 0f;

        /// <summary>Hold a raw button down.</summary>
        public OperatorTestInput Press(int button)
        {
            if (button >= 0 && button < m_Buttons.Length)
                m_Buttons[button] = true;
            return this;
        }

        /// <summary>Let a raw button up.</summary>
        public OperatorTestInput Release(int button)
        {
            if (button >= 0 && button < m_Buttons.Length)
                m_Buttons[button] = false;
            return this;
        }

        /// <summary>Park a raw axis at a value.</summary>
        public OperatorTestInput SetAxis(int axis, float value)
        {
            if (axis >= 0 && axis < m_Axes.Length)
                m_Axes[axis] = value;
            return this;
        }
    }
}
