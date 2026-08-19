namespace Stadium3D.Operators
{
    /// <summary>
    /// Which operator a device (or a cursor, or a log entry) belongs to. Both roles are live
    /// simultaneously — this is an identity, not a priority: nothing in the input layer gates
    /// <see cref="Secondary"/> behind <see cref="Primary"/>.
    /// </summary>
    public enum OperatorRole
    {
        /// <summary>The tracked operator inside the CAVE holding the MiddleVR wand (Flystick).</summary>
        Primary = 0,

        /// <summary>The co-pilot / instructor on the gamepad.</summary>
        Secondary = 1,
    }
}
