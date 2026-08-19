using System;
using System.Collections.Generic;
using UnityEngine;

namespace Stadium3D.Football
{
    /// <summary>One player's assignment on one play: who, what they run, and how it should read.</summary>
    [Serializable]
    public class PlayerScript
    {
        [Tooltip("Jersey number of the player this assignment belongs to.")]
        public int Number;

        [Tooltip("Which team, so the number resolves to a roster id.")]
        public TeamSide Side = TeamSide.Home;

        [Tooltip("Short label shown in the live telemetry — \"dig\", \"go\", \"inside zone\", \"cover 3 flat\".")]
        public string RouteName = "";

        [Tooltip("Pre-snap facing in degrees about Y, pitch-local. Seeds the heading solve, so a lineman in " +
                 "his stance faces the defense rather than the direction of his first step.")]
        public float InitialFacingDegrees;

        [Tooltip("The route, in yards relative to the ball. The first waypoint IS the pre-snap alignment.")]
        public List<RouteWaypoint> Waypoints = new List<RouteWaypoint>();

        [Tooltip("Override this player's motion envelope for this play only. Leave clear to use the roster's.")]
        public bool OverrideLimits;

        [Tooltip("The override envelope, used only when OverrideLimits is set.")]
        public MotionLimits Limits;

        /// <summary>Roster id this assignment resolves to.</summary>
        public int PlayerId => PlayerIdentity.PlayerKey(Side, Number);
    }

    /// <summary>
    /// A timestamped change of ball custody. The ordered list of these <b>is</b> the ball's behaviour — it is
    /// never integrated, so a pass provably arrives in the receiver's hands at the authored instant instead of
    /// depending on a gravity constant tuned until it happens to land.
    /// </summary>
    [Serializable]
    public class BallEvent
    {
        [Tooltip("Seconds from the snap.")]
        public float Time;

        [Tooltip("What happens to the ball here.")]
        public BallEventKind Kind = BallEventKind.Snap;

        [Tooltip("Jersey number the ball comes from, where that applies (a handoff, a throw).")]
        public int FromNumber;

        [Tooltip("Jersey number the ball goes to, where that applies (a snap, a handoff, a catch).")]
        public int ToNumber;

        [Tooltip("Which team the From/To numbers belong to. A turnover is the one case this flips.")]
        public TeamSide Side = TeamSide.Home;

        [Min(0f)]
        [Tooltip("Peak height of the throw above the straight line between release and arrival, metres. " +
                 "Only meaningful on a Throw.")]
        public float ApexMeters = 3.5f;

        [Tooltip("Where an incompletion lands, as an absolute yard line. Only meaningful on Incomplete.")]
        public float TargetYardLine;

        [Tooltip("Where an incompletion lands, lateral yards from the centre of the field.")]
        public float TargetLateralYards;

        [Min(0f)]
        [Tooltip("How long the ball stays live after this event before it is dead. Used to let an " +
                 "incompletion settle rather than vanishing at the whistle.")]
        public float DeadAfterSeconds;

        /// <summary>Roster id the ball comes from.</summary>
        public int FromPlayerId => PlayerIdentity.PlayerKey(Side, FromNumber);

        /// <summary>Roster id the ball goes to.</summary>
        public int ToPlayerId => PlayerIdentity.PlayerKey(Side, ToNumber);
    }

    /// <summary>
    /// What a play did to the game — <b>authored, not inferred from geometry</b>. A scripted drive has to land
    /// on an exact down-and-distance sequence, and deriving that from where a marker happened to stop would
    /// make the scoreboard hostage to a bake tolerance.
    /// </summary>
    [Serializable]
    public class PlayOutcome
    {
        [Tooltip("How the play ended, which selects how it is counted in the box score.")]
        public PlayResultKind Kind = PlayResultKind.Rush;

        [Tooltip("Net yards, signed in the offense's direction. A sack is negative.")]
        public float YardsGained;

        [Tooltip("Set when the play produced a first down. Authored rather than derived so a scripted drive " +
                 "cannot drift out of sync with its own chains.")]
        public bool FirstDown;

        [Tooltip("Seconds of game clock the play and its aftermath consume.")]
        public float ElapsedSeconds = 6f;

        [Tooltip("Does the game clock keep running after the whistle? False on an incompletion or a score.")]
        public bool ClockRunsAfter = true;

        [Tooltip("Jersey number credited with the carry or reception, if any.")]
        public int BallCarrierNumber;

        [Tooltip("Jersey number credited with the pass attempt, if any.")]
        public int PasserNumber;

        [Header("Penalty")]
        [Tooltip("Set when a flag nullified or modified the play.")]
        public bool HasPenalty;

        [Tooltip("Penalty name for the readout, e.g. \"Offensive Holding\".")]
        public string PenaltyName = "";

        [Tooltip("Penalty yardage, always positive; the sign is decided by which team was flagged.")]
        public float PenaltyYards;

        [Tooltip("Which team was flagged.")]
        public TeamSide PenalizedSide = TeamSide.Home;

        [Tooltip("Does the flag give the offense an automatic first down? Defensive fouls often do.")]
        public bool AutomaticFirstDown;

        [Tooltip("Is the down replayed? True for most offensive fouls.")]
        public bool ReplayDown = true;

        [Header("Escape hatch")]
        [Tooltip("Take the authored situation below verbatim instead of folding this outcome. The rules model " +
                 "here is deliberately shallow — this is how a scripted drive stays on its intended sequence " +
                 "when a real rulebook case does not fit. Scripted demo only.")]
        public bool SituationOverride;

        [Tooltip("The situation to force when SituationOverride is set.")]
        public GameSituation ForcedSituation;
    }

    /// <summary>
    /// One complete play as pure data: where the ball starts, who does what, what the ball does, and what it
    /// all meant. Nothing here is a Unity object, so a play is testable off the run loop.
    /// </summary>
    [Serializable]
    public class PlayScript
    {
        [Tooltip("Display name, e.g. \"Gun Trips Right — Four Verticals\".")]
        public string Name = "Play";

        [Tooltip("Offensive formation, for the huddle readout.")]
        public string Formation = "";

        [Tooltip("Defensive front and coverage, for the huddle readout.")]
        public string DefensiveCall = "";

        [Tooltip("Which team has the ball.")]
        public TeamSide Offense = TeamSide.Home;

        [Tooltip("Absolute yard of the line of scrimmage: 0 = home goal, 50 = midfield, 100 = away goal.")]
        public float LineOfScrimmageYard = 35f;

        [Tooltip("Lateral yards from the centre of the field to the ball — normally a hash mark.")]
        public float BallLateralYards;

        [Min(0f)]
        [Tooltip("Floor on the play's length. The real duration is DERIVED from the bake and the ball " +
                 "events, so a designer cannot author a play shorter than it is physically runnable — this " +
                 "only extends it.")]
        public float MinDurationSeconds = 3f;

        [Tooltip("Every player's assignment. All 22 should appear.")]
        public List<PlayerScript> Players = new List<PlayerScript>();

        [Tooltip("Ball custody in time order. Must start with a Snap at t = 0.")]
        public List<BallEvent> BallEvents = new List<BallEvent>();

        [Tooltip("What the play did to the game.")]
        public PlayOutcome Outcome = new PlayOutcome();

        /// <summary>The assignment for a roster id on this play, or null.</summary>
        public PlayerScript ScriptFor(int playerId)
        {
            for (int i = 0; i < Players.Count; i++)
                if (Players[i] != null && Players[i].PlayerId == playerId)
                    return Players[i];
            return null;
        }

        /// <summary>The team without the ball.</summary>
        public TeamSide Defense => Offense == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
    }
}
