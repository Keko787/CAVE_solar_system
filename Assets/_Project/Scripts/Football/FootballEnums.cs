namespace Stadium3D.Football
{
    /// <summary>
    /// Which team a body belongs to. This also decides which CAVE wall shows it as "ours": Home is the
    /// right wall, Away the left, matching the confirmed wall assignment.
    /// </summary>
    public enum TeamSide
    {
        Home = 0,
        Away = 1,
    }

    /// <summary>
    /// A player's position, which selects the stat line the focused-player card shows. Grouped rather than
    /// exhaustive — a demo needs QB/RB/WR to read differently, not every NFL depth-chart distinction.
    /// </summary>
    public enum PlayerPosition
    {
        Quarterback = 0,
        RunningBack = 1,
        WideReceiver = 2,
        TightEnd = 3,
        OffensiveLine = 4,
        DefensiveLine = 5,
        Linebacker = 6,
        Cornerback = 7,
        Safety = 8,
    }

    /// <summary>
    /// What a waypoint means to <see cref="RouteBaker"/>. The kind changes the speed solve, not the path.
    /// </summary>
    public enum WaypointKind
    {
        /// <summary>An ordinary point on the path. Speed is limited only by the curvature there.</summary>
        Through = 0,

        /// <summary>
        /// A deliberate break. The baker holds the body to <see cref="RouteWaypoint.SpeedFraction"/> through
        /// this point, so a designer can make a cut read sharp rather than letting the curvature solve alone
        /// decide how hard it is.
        /// </summary>
        Cut = 1,

        /// <summary>Stand here for <see cref="RouteWaypoint.DwellSeconds"/> at zero speed, then continue.</summary>
        Dwell = 2,

        /// <summary>The end of the route. Speed is solved to zero on arrival.</summary>
        Stop = 3,
    }

    /// <summary>Where the ball is and who, if anyone, has it.</summary>
    public enum BallCarriage
    {
        /// <summary>Held by a player; the ball rides their pose.</summary>
        Held = 0,

        /// <summary>In the air between a release and its resolution, on a solved arc.</summary>
        InFlight = 1,

        /// <summary>On the ground and live — an incompletion settling, or a fumble before recovery.</summary>
        Loose = 2,

        /// <summary>Whistled dead and frozen at the spot.</summary>
        Dead = 3,
    }

    /// <summary>
    /// A scripted change of ball custody. The ordered list of these <em>is</em> the ball's behaviour — it is
    /// never simulated, so a pass provably arrives in the receiver's hands at the authored instant instead of
    /// depending on a gravity constant that has to be tuned until it happens to land.
    /// </summary>
    public enum BallEventKind
    {
        /// <summary>The snap. Every play has exactly one, at t = 0.</summary>
        Snap = 0,

        /// <summary>Ball passed from one carrier to another on the ground.</summary>
        Handoff = 1,

        /// <summary>Released toward a target. Must be closed by a <see cref="Catch"/> or <see cref="Incomplete"/>.</summary>
        Throw = 2,

        /// <summary>A throw arrives and is secured.</summary>
        Catch = 3,

        /// <summary>A throw is not secured. The ball goes <see cref="BallCarriage.Loose"/>, then dead.</summary>
        Incomplete = 4,

        /// <summary>The carrier is down. The ball is dead at the spot.</summary>
        Down = 5,

        /// <summary>The ball crosses the goal line in possession.</summary>
        CrossGoalLine = 6,
    }

    /// <summary>Where the transport is in the beat cycle for the current play.</summary>
    public enum PlayPhase
    {
        /// <summary>Between plays: bodies walk from the last play's terminal pose to this play's alignment.</summary>
        Huddle = 0,

        /// <summary>The play is running.</summary>
        Live = 1,

        /// <summary>Whistle to the next huddle: the result is on the walls and the field is still.</summary>
        PostPlay = 2,

        /// <summary>The drive is over and not looping.</summary>
        DriveComplete = 3,
    }

    /// <summary>How a play ended, for the stats ledger and the down-and-distance fold.</summary>
    public enum PlayResultKind
    {
        Rush = 0,
        PassComplete = 1,
        PassIncomplete = 2,
        Sack = 3,
        Touchdown = 4,
        Turnover = 5,

        /// <summary>The snap was nullified by a flag; the down is replayed from the enforcement spot.</summary>
        PenaltyNullified = 6,
    }
}
