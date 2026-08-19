namespace Stadium3D.Operators
{
    /// <summary>
    /// One physical input device driving one operator. The CAVE now has two: the primary operator's
    /// tracked MiddleVR wand and a secondary operator's gamepad, live <b>at the same time</b> — so
    /// "the input" can no longer be a single static read (<c>MiddleVRWand</c>), it has to be a thing
    /// you can have two of.
    ///
    /// Deliberately a <em>raw device</em> abstraction — indexed buttons and axes, no semantics. Button
    /// indices are per-device and per-configuration (the wand's come from the <c>.vrx</c>; a pad's come
    /// from whichever backend enumerated it), exactly like the inspector-configurable indices the
    /// existing wand consumers already carry. Semantics are layered on top by <see cref="GamepadMap"/>
    /// + <see cref="PadOperator"/>, which is what turns "button 3" into "Y".
    /// </summary>
    /// <remarks>
    /// Implementations must be safe to read before their device exists (MiddleVR kernel not up, no pad
    /// plugged in) — every read returns <c>false</c>/<c>0</c> rather than throwing, matching the
    /// null-guard contract of <see cref="Stadium3D.Vr.MiddleVRWand"/>.
    /// </remarks>
    public interface IOperatorInput
    {
        /// <summary>Human-readable device name for logs and the probe ("Wand0", "Xbox One Controller").</summary>
        string DeviceName { get; }

        /// <summary>False while the device is absent — no pad plugged in, or the MiddleVR kernel isn't up yet.</summary>
        bool IsConnected { get; }

        /// <summary>Number of buttons this device reports; reads outside [0, ButtonCount) are false, not errors.</summary>
        int ButtonCount { get; }

        /// <summary>Number of axes this device reports; reads outside [0, AxisCount) are 0, not errors.</summary>
        int AxisCount { get; }

        /// <summary>True while the button is down.</summary>
        bool Held(int button);

        /// <summary>True only on the frame the button transitions to down (rising edge).</summary>
        bool PressedThisFrame(int button);

        /// <summary>True only on the frame the button transitions to up (falling edge).</summary>
        bool ReleasedThisFrame(int button);

        /// <summary>Axis value, normally in [-1, 1]. Raw — dead-zone and inversion are the map's job.</summary>
        float Axis(int axis);
    }
}
