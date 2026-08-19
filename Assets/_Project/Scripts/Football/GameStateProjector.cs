using UnityEngine;

namespace Stadium3D.Football
{
    /// <summary>
    /// Pure folds over <see cref="GameSituation"/>: what the state becomes after a play, and what it looks
    /// like at any instant while one is running.
    /// </summary>
    /// <remarks>
    /// <b>This is deliberately not a rules engine.</b> It implements the ordinary cases a scripted drive
    /// needs — downs, distance, spot, first downs, a penalty, a score — and yields entirely to
    /// <see cref="PlayOutcome.SituationOverride"/> when the authoring says so. That is correct for a
    /// demo whose sequence is authored and verified against an expected box score, and it means the front
    /// wall's down-and-distance is only ever as right as the content.
    ///
    /// <para>If this project ever wants free play — AI, or outcomes decided by the sim rather than authored —
    /// this has to become a real rulebook and the override field turns into a liability. Scripted demo only.</para>
    /// </remarks>
    public static class GameStateProjector
    {
        /// <summary>Yards needed for a first down from a fresh set.</summary>
        public const float FirstDownYards = 10f;

        /// <summary>
        /// The situation at the snap of the next play, given the situation at this snap and what this play
        /// did. Pure: same inputs, same output, no matter how many times or in what order it is called.
        /// </summary>
        public static GameSituation Next(in GameSituation atSnap, PlayOutcome outcome)
        {
            if (outcome == null)
                return atSnap;

            if (outcome.SituationOverride)
                return outcome.ForcedSituation;

            var s = atSnap;
            float direction = FieldGeometry.DirectionFor(s.Possession);

            // ── clock ─────────────────────────────────────────────────────────────────────────────
            s.GameClockSeconds = Mathf.Max(0f, s.GameClockSeconds - Mathf.Max(0f, outcome.ElapsedSeconds));
            s.PlayClockSeconds = 40f;
            s.ClockRunning = outcome.ClockRunsAfter;

            // ── a flag that wipes the play ────────────────────────────────────────────────────────
            if (outcome.HasPenalty && outcome.Kind == PlayResultKind.PenaltyNullified)
            {
                // Marked off from the previous spot. An offensive foul moves the offense backwards.
                float sign = outcome.PenalizedSide == s.Possession ? -1f : 1f;
                s.LineOfScrimmageYard = ClampToField(s.LineOfScrimmageYard + sign * outcome.PenaltyYards * direction);

                if (outcome.AutomaticFirstDown && outcome.PenalizedSide != s.Possession)
                {
                    s.Down = 1;
                    s.DistanceYards = FirstDownYards;
                }
                else
                {
                    // The down is replayed, and the distance grows or shrinks by the mark-off.
                    if (!outcome.ReplayDown)
                        s.Down = Mathf.Min(4, s.Down + 1);
                    s.DistanceYards = Mathf.Max(1f, s.DistanceYards - sign * outcome.PenaltyYards);
                }

                return ApplyGoalToGo(s);
            }

            // ── an ordinary snap ──────────────────────────────────────────────────────────────────
            float newSpot = ClampToField(s.LineOfScrimmageYard + outcome.YardsGained * direction);

            if (outcome.Kind == PlayResultKind.Touchdown)
            {
                if (s.Possession == TeamSide.Home) s.HomeScore += 6; else s.AwayScore += 6;
                s.Down = 1;
                s.DistanceYards = FirstDownYards;
                s.LineOfScrimmageYard = FieldGeometry.GoalLineFor(s.Possession);
                s.GoalToGo = false;
                s.ClockRunning = false;
                return s;
            }

            if (outcome.Kind == PlayResultKind.Turnover)
            {
                s.Possession = s.Possession == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
                s.LineOfScrimmageYard = newSpot;
                s.Down = 1;
                s.DistanceYards = FirstDownYards;
                return ApplyGoalToGo(s);
            }

            if (outcome.Kind == PlayResultKind.PassIncomplete)
            {
                // Ball comes back to the previous spot; only the down changes.
                s.Down = Mathf.Min(4, s.Down + 1);
                s.ClockRunning = false;
                return ApplyGoalToGo(s);
            }

            s.LineOfScrimmageYard = newSpot;

            if (outcome.FirstDown)
            {
                s.Down = 1;
                s.DistanceYards = FirstDownYards;
            }
            else
            {
                s.Down = Mathf.Min(4, s.Down + 1);
                s.DistanceYards = Mathf.Max(1f, s.DistanceYards - outcome.YardsGained);
            }

            return ApplyGoalToGo(s);
        }

        /// <summary>
        /// The situation during the huddle before a play. The play clock counts down; the game clock runs
        /// only if the previous outcome left it running.
        /// </summary>
        public static GameSituation Huddle(in GameSituation atSnap, float phaseTime, float huddleSeconds)
        {
            var s = atSnap;
            s.PlayClockSeconds = Mathf.Max(0f, 40f - phaseTime);
            s.LiveBallYard = atSnap.LineOfScrimmageYard;   // the ball is on the spot, waiting
            if (s.ClockRunning)
                s.GameClockSeconds = Mathf.Max(0f, atSnap.GameClockSeconds - phaseTime);
            return s;
        }

        /// <summary>
        /// The situation at <paramref name="playTime"/> seconds into a live play.
        /// </summary>
        /// <remarks>
        /// <b>Nothing about the spot or the chains moves during a play.</b> Down, distance and the line of
        /// scrimmage all stay at their snap values until the whistle — the front wall must not flip to the
        /// next down while the ball is still in the air, and "BALL ON" means the spot this snap was taken
        /// from, not wherever the football currently is.
        ///
        /// <para>An earlier version moved <see cref="GameSituation.LineOfScrimmageYard"/> to the live ball
        /// position, which made the readout say "OWN 30" pre-snap on a play taken from the 35 — because at
        /// t = 0 the ball is in a shotgun quarterback's hands five yards behind the line. The live position
        /// is still wanted, for the tactical overlay and the ball marker, so it now travels separately in
        /// <see cref="GameSituation.LiveBallYard"/> where it cannot be mistaken for the spot.</para>
        /// </remarks>
        public static GameSituation InPlay(in GameSituation atSnap, PlayScript play, float playTime, float ballYard)
        {
            var s = atSnap;
            s.PlayClockSeconds = 0f;
            s.ClockRunning = true;
            s.GameClockSeconds = Mathf.Max(0f, atSnap.GameClockSeconds - Mathf.Max(0f, playTime));
            s.LiveBallYard = ClampToField(ballYard);
            return s;
        }

        /// <summary>
        /// Set <see cref="GameSituation.GoalToGo"/> when the goal line is nearer than the yards-to-gain, and
        /// trim the distance to the goal line so "1st &amp; 10" never appears on the opponent's 6.
        /// </summary>
        public static GameSituation ApplyGoalToGo(GameSituation s)
        {
            float toGoal = FieldGeometry.YardsToGoal(s.Possession, s.LineOfScrimmageYard);
            if (toGoal <= s.DistanceYards)
            {
                s.GoalToGo = true;
                s.DistanceYards = toGoal;
            }
            else
            {
                s.GoalToGo = false;
            }
            return s;
        }

        static float ClampToField(float absoluteYard) => Mathf.Clamp(
            absoluteYard,
            FieldGeometry.HomeGoalLineYard - FieldGeometry.EndZoneDepthYards,
            FieldGeometry.AwayGoalLineYard + FieldGeometry.EndZoneDepthYards);
    }
}
