namespace Stadium3D.Operators
{
    /// <summary>
    /// The semantic controls of an Xbox-layout pad, in the project's <b>canonical order</b>. This order is
    /// the contract: <see cref="UnityGamepadOperatorInput"/> reports its buttons in exactly this sequence
    /// (so its <see cref="GamepadMap"/> is the identity), and a <see cref="GamepadMap"/> asset translates
    /// some other device's indices into it. Consumers name controls, never numbers.
    /// </summary>
    public enum PadControl
    {
        /// <summary>A — click / confirm.</summary>
        A = 0,
        /// <summary>B — back / cancel / disarm.</summary>
        B = 1,
        X = 2,
        Y = 3,
        LeftBumper = 4,
        RightBumper = 5,
        /// <summary>LT as a digital press. Analog value is <see cref="PadAxis.LeftTrigger"/>.</summary>
        LeftTrigger = 6,
        /// <summary>RT as a digital press. Analog value is <see cref="PadAxis.RightTrigger"/>.</summary>
        RightTrigger = 7,
        LeftStickPress = 8,
        RightStickPress = 9,
        /// <summary>The "View" button — the one historically labelled Back.</summary>
        View = 10,
        /// <summary>The "Menu" button — the one historically labelled Start.</summary>
        Menu = 11,
        DpadUp = 12,
        DpadDown = 13,
        DpadLeft = 14,
        DpadRight = 15,
    }

    /// <summary>
    /// The analog axes of an Xbox-layout pad, in canonical order (see <see cref="PadControl"/>).
    /// Y axes are <b>up-positive</b> after mapping, matching the wand's vertical axis convention that
    /// <c>FrontWallPortal</c> already reads (push up = open).
    /// </summary>
    public enum PadAxis
    {
        LeftStickX = 0,
        LeftStickY = 1,
        RightStickX = 2,
        RightStickY = 3,
        /// <summary>LT travel in [0, 1].</summary>
        LeftTrigger = 4,
        /// <summary>RT travel in [0, 1].</summary>
        RightTrigger = 5,
    }

    /// <summary>Sizes for the canonical control/axis spaces, so maps can be allocated without magic numbers.</summary>
    public static class PadControls
    {
        /// <summary>Number of entries in <see cref="PadControl"/>.</summary>
        public const int ButtonCount = 16;

        /// <summary>Number of entries in <see cref="PadAxis"/>.</summary>
        public const int AxisCount = 6;
    }
}
