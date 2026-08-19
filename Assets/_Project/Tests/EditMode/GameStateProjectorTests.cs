using NUnit.Framework;
using Stadium3D.Football;
using UnityEngine;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// The down-and-distance fold, pinned. <see cref="GameStateProjector"/> is deliberately not a rulebook —
    /// it owns the handful of ordinary cases a scripted drive needs and yields to
    /// <see cref="PlayOutcome.SituationOverride"/> for everything else — so what is worth protecting here is
    /// not rules coverage but that the cases it <em>does</em> own behave exactly as authored.
    ///
    /// <para>Two things make a wrong branch here expensive. The front wall's scoreboard reads straight out of
    /// this fold with nothing in between that could notice a bad down, and <see cref="DriveLedger"/> folds the
    /// entire drive once at bind time — so a single mistake is not one wrong play, it is every situation from
    /// that play onward, baked in before the demo starts. There is no runtime that would catch it; a person
    /// standing in the CAVE would, which is far too late.</para>
    ///
    /// <para>Most of these assert an invariant rather than a hand-computed number — the down never passes 4,
    /// distance never drops below the floor, the ball never leaves the field, a nullified snap does not
    /// advance the down. Those are the properties the fold structurally guarantees, and they stay true if the
    /// authoring content changes underneath them.</para>
    ///
    /// <para>The display helpers on <see cref="GameSituation"/> are covered too. They look trivial, but they
    /// are the last step before the pixels: an off-by-one in the OWN/OPP split, or a clock that renders
    /// "12:4" instead of "12:04", is precisely the class of bug nothing upstream can catch.</para>
    /// </summary>
    public class GameStateProjectorTests
    {
        // Positions and distances are yards; the fold does no unit work, so 1e-3 is generous.
        const float YardTolerance = 1e-3f;
        const float SecondTolerance = 1e-3f;

        /// <summary>A situation at a snap, on top of the sane opening state so clocks and timeouts are real.</summary>
        static GameSituation At(TeamSide possession, int down, float distanceYards, float lineOfScrimmageYard)
        {
            var s = GameSituation.Kickoff(possession, lineOfScrimmageYard);
            s.Down = down;
            s.DistanceYards = distanceYards;
            s.ClockRunning = true;
            return s;
        }

        /// <summary>An ordinary snap that gained ground.</summary>
        static PlayOutcome Gain(PlayResultKind kind, float yards, bool firstDown) => new PlayOutcome
        {
            Kind = kind,
            YardsGained = yards,
            FirstDown = firstDown,
            ElapsedSeconds = 6f,
            ClockRunsAfter = true,
        };

        // ── the ordinary snap ──────────────────────────────────────────────────────────────────────

        [Test]
        public void Next_ThirdAndTwoConverted_BecomesFirstAndTenAtTheNewSpot()
        {
            var atSnap = At(TeamSide.Home, 3, 2f, 40f);
            var outcome = Gain(PlayResultKind.Rush, 5f, firstDown: true);

            var next = GameStateProjector.Next(atSnap, outcome);

            Assert.AreEqual(1, next.Down);
            Assert.That(next.DistanceYards, Is.EqualTo(GameStateProjector.FirstDownYards).Within(YardTolerance));
            Assert.That(next.LineOfScrimmageYard, Is.EqualTo(45f).Within(YardTolerance),
                "home advances up the absolute yard axis, so 40 + 5 = 45");
            Assert.IsFalse(next.GoalToGo, "the goal line is 55 yards off, further than the yards to gain");
            Assert.AreEqual("1st & 10", next.DownAndDistance());
        }

        [Test]
        public void Next_GainThatDoesNotConvert_AdvancesTheDownAndShortensTheChains()
        {
            var atSnap = At(TeamSide.Home, 3, 8f, 30f);
            var outcome = Gain(PlayResultKind.Rush, 5f, firstDown: false);

            var next = GameStateProjector.Next(atSnap, outcome);

            Assert.AreEqual(4, next.Down);
            Assert.That(next.DistanceYards, Is.EqualTo(3f).Within(YardTolerance), "8 to gain less 5 gained");
            Assert.That(next.LineOfScrimmageYard, Is.EqualTo(35f).Within(YardTolerance));
            Assert.AreEqual("4th & 3", next.DownAndDistance());
        }

        [Test]
        public void Next_AwayOffense_MovesDownTheAbsoluteYardAxis()
        {
            // The one place a sign convention could invert without any other test noticing: the away team
            // gains yards by moving toward yard 0, and every spot in the fold goes through DirectionFor.
            var atSnap = At(TeamSide.Away, 1, GameStateProjector.FirstDownYards, 70f);
            var outcome = Gain(PlayResultKind.PassComplete, 12f, firstDown: true);

            var next = GameStateProjector.Next(atSnap, outcome);

            Assert.That(next.LineOfScrimmageYard, Is.EqualTo(58f).Within(YardTolerance));
            Assert.AreEqual(1, next.Down);
            Assert.AreEqual(TeamSide.Away, next.Possession, "an ordinary gain does not change hands");
        }

        [Test]
        public void Next_DownNeverPassesFour()
        {
            // The projector is not a rulebook: it has no idea a fourth-down failure is a turnover on downs.
            // What it does guarantee is that the readout never says "5th", which would be nonsense on a wall.
            var atSnap = At(TeamSide.Home, 4, 6f, 45f);
            var next = GameStateProjector.Next(atSnap, Gain(PlayResultKind.Rush, 2f, firstDown: false));

            Assert.AreEqual(4, next.Down);
            Assert.AreEqual(TeamSide.Home, next.Possession,
                "handing the ball over on downs is a rulebook case this fold deliberately leaves to authoring");
        }

        [Test]
        public void Next_DistanceNeverFallsBelowOneYard()
        {
            // Authoring says no first down, but the yardage says otherwise. Rather than showing "3rd & -2",
            // the fold floors the chains at a yard and lets the authored FirstDown flag stay authoritative.
            var atSnap = At(TeamSide.Home, 2, 3f, 30f);
            var next = GameStateProjector.Next(atSnap, Gain(PlayResultKind.Rush, 5f, firstDown: false));

            Assert.That(next.DistanceYards, Is.EqualTo(1f).Within(YardTolerance));
            Assert.AreEqual("3rd & 1", next.DownAndDistance());
        }

        [Test]
        public void Next_LargeLoss_StopsAtTheBackOfTheEndZone()
        {
            // Derived from the field constants rather than typed, so moving an end zone moves this with it.
            var atSnap = At(TeamSide.Home, 1, GameStateProjector.FirstDownYards, 5f);
            var outcome = Gain(PlayResultKind.Sack, -30f, firstDown: false);

            var next = GameStateProjector.Next(atSnap, outcome);

            float backOfEndZone = FieldGeometry.HomeGoalLineYard - FieldGeometry.EndZoneDepthYards;
            Assert.That(next.LineOfScrimmageYard, Is.EqualTo(backOfEndZone).Within(YardTolerance),
                "the fold must never spot the ball outside the field it is drawn on");
        }

        // ── the clocks ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Next_BurnsTheElapsedTimeAndResetsThePlayClock()
        {
            var atSnap = At(TeamSide.Home, 1, GameStateProjector.FirstDownYards, 25f);
            var outcome = Gain(PlayResultKind.Rush, 4f, firstDown: false);
            outcome.ElapsedSeconds = 7.5f;

            var next = GameStateProjector.Next(atSnap, outcome);

            Assert.That(next.GameClockSeconds,
                Is.EqualTo(atSnap.GameClockSeconds - 7.5f).Within(SecondTolerance));
            Assert.That(next.PlayClockSeconds, Is.EqualTo(40f).Within(SecondTolerance),
                "every snap gets a fresh play clock");
            Assert.IsTrue(next.ClockRunning, "the outcome said the clock keeps running after the whistle");
        }

        [Test]
        public void Next_GameClockNeverGoesNegative()
        {
            var atSnap = At(TeamSide.Home, 2, 5f, 40f);
            atSnap.GameClockSeconds = 4f;
            var outcome = Gain(PlayResultKind.Rush, 3f, firstDown: false);
            outcome.ElapsedSeconds = 10f;

            var next = GameStateProjector.Next(atSnap, outcome);

            Assert.That(next.GameClockSeconds, Is.EqualTo(0f).Within(SecondTolerance));
            Assert.AreEqual("0:00", next.GameClock());
        }

        // ── the incompletion ───────────────────────────────────────────────────────────────────────

        [Test]
        public void Next_Incompletion_AdvancesTheDownAndLeavesTheSpotAndChainsAlone()
        {
            var atSnap = At(TeamSide.Home, 2, 7f, 28f);
            var outcome = Gain(PlayResultKind.PassIncomplete, 0f, firstDown: false);

            var next = GameStateProjector.Next(atSnap, outcome);

            Assert.AreEqual(3, next.Down);
            Assert.That(next.LineOfScrimmageYard, Is.EqualTo(28f).Within(YardTolerance),
                "the ball comes back to the previous spot");
            Assert.That(next.DistanceYards, Is.EqualTo(7f).Within(YardTolerance));
            Assert.AreEqual("3rd & 7", next.DownAndDistance());
        }

        [Test]
        public void Next_Incompletion_StopsTheClockEvenWhenTheOutcomeSaysOtherwise()
        {
            // ClockRunsAfter is authored per play and it is easy to leave at its true default on an
            // incompletion. The incompletion branch has to win, because a clock that keeps ticking through
            // an incomplete pass is the sort of thing an audience notices immediately.
            var atSnap = At(TeamSide.Home, 1, GameStateProjector.FirstDownYards, 35f);
            var outcome = Gain(PlayResultKind.PassIncomplete, 0f, firstDown: false);
            outcome.ClockRunsAfter = true;

            var next = GameStateProjector.Next(atSnap, outcome);

            Assert.IsFalse(next.ClockRunning);
        }

        [Test]
        public void Next_Incompletion_IgnoresAuthoredYardageEntirely()
        {
            // An incompletion nets nothing, so YardsGained should be zero — but if a play is authored with
            // the air yards left in it by mistake, the ball still must not move. This pins the branch
            // structure, not the data: reordering the fold so the spot is applied first would fail here.
            var atSnap = At(TeamSide.Home, 2, 9f, 41f);
            var outcome = Gain(PlayResultKind.PassIncomplete, 14f, firstDown: false);

            var next = GameStateProjector.Next(atSnap, outcome);

            Assert.That(next.LineOfScrimmageYard, Is.EqualTo(41f).Within(YardTolerance));
            Assert.That(next.DistanceYards, Is.EqualTo(9f).Within(YardTolerance));
        }

        // ── the score ──────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Next_Touchdown_AddsSixToThePossessingTeamOnly()
        {
            var atSnap = At(TeamSide.Home, 2, 5f, 88f);
            atSnap.HomeScore = 7;
            atSnap.AwayScore = 10;

            var next = GameStateProjector.Next(atSnap, Gain(PlayResultKind.Touchdown, 12f, firstDown: true));

            Assert.AreEqual(13, next.HomeScore, "six for the score; the try is a separate authored play");
            Assert.AreEqual(10, next.AwayScore, "the other team's score is untouched");
        }

        [Test]
        public void Next_TouchdownByAway_CreditsAwayAndSpotsTheBallOnTheHomeGoalLine()
        {
            var atSnap = At(TeamSide.Away, 3, 4f, 9f);
            atSnap.AwayScore = 3;

            var next = GameStateProjector.Next(atSnap, Gain(PlayResultKind.Touchdown, 9f, firstDown: true));

            Assert.AreEqual(9, next.AwayScore);
            Assert.AreEqual(0, next.HomeScore);
            Assert.That(next.LineOfScrimmageYard,
                Is.EqualTo(FieldGeometry.GoalLineFor(TeamSide.Away)).Within(YardTolerance),
                "the ball is spotted on the goal line it crossed, not wherever the yardage landed");
        }

        [Test]
        public void Next_Touchdown_ClearsGoalToGoAndStopsTheClock()
        {
            // The score branch is the one place the fold does NOT run ApplyGoalToGo. It must not: on the goal
            // line the yards-to-goal is zero, so the readout would come back as "1st & Goal" immediately
            // after a touchdown.
            var atSnap = At(TeamSide.Home, 1, 6f, 94f);
            atSnap.GoalToGo = true;
            var outcome = Gain(PlayResultKind.Touchdown, 6f, firstDown: true);
            outcome.ClockRunsAfter = true;

            var next = GameStateProjector.Next(atSnap, outcome);

            Assert.IsFalse(next.GoalToGo);
            Assert.IsFalse(next.ClockRunning, "the clock stops on a score regardless of what the play authored");
            Assert.AreEqual(1, next.Down);
            Assert.That(next.DistanceYards, Is.EqualTo(GameStateProjector.FirstDownYards).Within(YardTolerance));
            Assert.AreEqual("1st & 10", next.DownAndDistance());
        }

        // ── the turnover ───────────────────────────────────────────────────────────────────────────

        [Test]
        public void Next_Turnover_FlipsPossessionAndResetsToFirstAndTen()
        {
            var atSnap = At(TeamSide.Home, 3, 7f, 45f);

            var next = GameStateProjector.Next(atSnap, Gain(PlayResultKind.Turnover, -3f, firstDown: false));

            Assert.AreEqual(TeamSide.Away, next.Possession);
            Assert.AreEqual(1, next.Down);
            Assert.That(next.DistanceYards, Is.EqualTo(GameStateProjector.FirstDownYards).Within(YardTolerance));
            Assert.That(next.LineOfScrimmageYard, Is.EqualTo(42f).Within(YardTolerance),
                "the yardage is still signed in the direction of the team that had the ball at the snap");
            Assert.IsFalse(next.GoalToGo);
            Assert.AreEqual("1st & 10", next.DownAndDistance());
        }

        [Test]
        public void Next_Turnover_ReadsAsTheNewOffensesOwnHalf()
        {
            // Possession flips before the readout is derived, so the same absolute yard changes label. This
            // is the fold and the display helper agreeing, which is what the front wall actually shows.
            var atSnap = At(TeamSide.Home, 2, 8f, 30f);

            var next = GameStateProjector.Next(atSnap, Gain(PlayResultKind.Turnover, 0f, firstDown: false));

            Assert.AreEqual("OWN 30", atSnap.BallOn());
            Assert.AreEqual("OPP 30", next.BallOn());
        }

        // ── the flag ───────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Next_OffensiveHolding_MarksOffBackwardsAndReplaysTheDown()
        {
            var atSnap = At(TeamSide.Home, 2, 8f, 35f);
            var outcome = new PlayOutcome
            {
                Kind = PlayResultKind.PenaltyNullified,
                HasPenalty = true,
                PenaltyName = "Offensive Holding",
                PenaltyYards = 10f,
                PenalizedSide = TeamSide.Home,
                AutomaticFirstDown = false,
                ReplayDown = true,
                ElapsedSeconds = 4f,
                ClockRunsAfter = false,
            };

            var next = GameStateProjector.Next(atSnap, outcome);

            Assert.That(next.LineOfScrimmageYard, Is.EqualTo(25f).Within(YardTolerance),
                "an offensive foul moves the offense backwards, against its own direction of travel");
            Assert.AreEqual(2, next.Down, "the snap was wiped out — the down is replayed, not spent");
            Assert.That(next.DistanceYards, Is.EqualTo(18f).Within(YardTolerance),
                "the chains do not move, so the mark-off is added to the yards to gain");
            Assert.AreEqual("2nd & 18", next.DownAndDistance());
        }

        [Test]
        public void Next_DefensivePenaltyWithAutomaticFirstDown_ResetsToFirstAndTen()
        {
            var atSnap = At(TeamSide.Home, 3, 12f, 30f);
            var outcome = new PlayOutcome
            {
                Kind = PlayResultKind.PenaltyNullified,
                HasPenalty = true,
                PenaltyName = "Defensive Holding",
                PenaltyYards = 5f,
                PenalizedSide = TeamSide.Away,
                AutomaticFirstDown = true,
                ReplayDown = true,
                ElapsedSeconds = 4f,
            };

            var next = GameStateProjector.Next(atSnap, outcome);

            Assert.That(next.LineOfScrimmageYard, Is.EqualTo(35f).Within(YardTolerance),
                "a defensive foul marks off toward the defense's own goal");
            Assert.AreEqual(1, next.Down);
            Assert.That(next.DistanceYards, Is.EqualTo(GameStateProjector.FirstDownYards).Within(YardTolerance));
            Assert.AreEqual("1st & 10", next.DownAndDistance());
        }

        [Test]
        public void Next_DefensivePenaltyWithoutAutomaticFirstDown_ShortensTheChainsAndReplaysTheDown()
        {
            var atSnap = At(TeamSide.Home, 2, 10f, 30f);
            var outcome = new PlayOutcome
            {
                Kind = PlayResultKind.PenaltyNullified,
                HasPenalty = true,
                PenaltyName = "Defensive Offside",
                PenaltyYards = 5f,
                PenalizedSide = TeamSide.Away,
                AutomaticFirstDown = false,
                ReplayDown = true,
                ElapsedSeconds = 2f,
            };

            var next = GameStateProjector.Next(atSnap, outcome);

            Assert.AreEqual(2, next.Down);
            Assert.That(next.DistanceYards, Is.EqualTo(5f).Within(YardTolerance));
            Assert.AreEqual("2nd & 5", next.DownAndDistance());
        }

        [Test]
        public void Next_PenaltyWithReplayDownCleared_SpendsTheDown()
        {
            // ReplayDown is authored, not inferred, because the enforcement rule differs by foul. When the
            // authoring clears it the down has to advance, which is the only thing separating this case from
            // the holding case above.
            var atSnap = At(TeamSide.Home, 1, GameStateProjector.FirstDownYards, 40f);
            var outcome = new PlayOutcome
            {
                Kind = PlayResultKind.PenaltyNullified,
                HasPenalty = true,
                PenaltyYards = 5f,
                PenalizedSide = TeamSide.Home,
                AutomaticFirstDown = false,
                ReplayDown = false,
                ElapsedSeconds = 3f,
            };

            var next = GameStateProjector.Next(atSnap, outcome);

            Assert.AreEqual(2, next.Down);
            Assert.That(next.LineOfScrimmageYard, Is.EqualTo(35f).Within(YardTolerance));
            Assert.That(next.DistanceYards, Is.EqualTo(15f).Within(YardTolerance));
        }

        [Test]
        public void Next_PenaltyNearTheGoalLine_StillGoesThroughTheGoalToGoTrim()
        {
            // Every non-scoring branch ends in ApplyGoalToGo. The penalty branch has its own early return, so
            // it is the one most likely to lose that call in a future edit.
            var atSnap = At(TeamSide.Home, 2, GameStateProjector.FirstDownYards, 88f);
            var outcome = new PlayOutcome
            {
                Kind = PlayResultKind.PenaltyNullified,
                HasPenalty = true,
                PenaltyName = "Defensive Pass Interference",
                PenaltyYards = 5f,
                PenalizedSide = TeamSide.Away,
                AutomaticFirstDown = true,
                ReplayDown = true,
                ElapsedSeconds = 5f,
            };

            var next = GameStateProjector.Next(atSnap, outcome);

            Assert.That(next.LineOfScrimmageYard, Is.EqualTo(93f).Within(YardTolerance));
            Assert.IsTrue(next.GoalToGo);
            Assert.That(next.DistanceYards, Is.EqualTo(7f).Within(YardTolerance));
            Assert.AreEqual("1st & Goal", next.DownAndDistance());
        }

        [Test]
        public void Next_PenaltyFlagWithoutTheNullifiedKind_FoldsAsAnOrdinarySnap()
        {
            // HasPenalty alone is not enough to take the penalty branch — a flag can also be attached to a
            // play that stood (declined, or offsetting). The kind is what decides.
            var atSnap = At(TeamSide.Home, 1, GameStateProjector.FirstDownYards, 30f);
            var outcome = Gain(PlayResultKind.Rush, 6f, firstDown: false);
            outcome.HasPenalty = true;
            outcome.PenaltyYards = 10f;
            outcome.PenalizedSide = TeamSide.Home;

            var next = GameStateProjector.Next(atSnap, outcome);

            Assert.That(next.LineOfScrimmageYard, Is.EqualTo(36f).Within(YardTolerance),
                "the yardage stands; the flag only shows up in the box score");
            Assert.AreEqual(2, next.Down);
            Assert.That(next.DistanceYards, Is.EqualTo(4f).Within(YardTolerance));
        }

        // ── goal to go ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void ApplyGoalToGo_InsideTheTen_TrimsTheDistanceToTheGoalLine()
        {
            var s = At(TeamSide.Home, 1, GameStateProjector.FirstDownYards, 94f);

            var trimmed = GameStateProjector.ApplyGoalToGo(s);

            Assert.IsTrue(trimmed.GoalToGo);
            Assert.That(trimmed.DistanceYards, Is.EqualTo(6f).Within(YardTolerance),
                "the line to gain cannot be past the goal line");
            Assert.AreEqual("1st & Goal", trimmed.DownAndDistance());
        }

        [Test]
        public void ApplyGoalToGo_OutsideTheTen_LeavesTheChainsAlone()
        {
            var s = At(TeamSide.Home, 1, GameStateProjector.FirstDownYards, 80f);

            var untouched = GameStateProjector.ApplyGoalToGo(s);

            Assert.IsFalse(untouched.GoalToGo);
            Assert.That(untouched.DistanceYards,
                Is.EqualTo(GameStateProjector.FirstDownYards).Within(YardTolerance));
            Assert.AreEqual("1st & 10", untouched.DownAndDistance());
        }

        [Test]
        public void ApplyGoalToGo_ExactlyOnTheLineToGain_CountsAsGoalToGo()
        {
            // Ball on the ten, ten to gain: the line to gain *is* the goal line, so this is goal to go. The
            // fold uses <= for exactly this boundary.
            var s = At(TeamSide.Home, 1, GameStateProjector.FirstDownYards,
                       FieldGeometry.AwayGoalLineYard - GameStateProjector.FirstDownYards);

            var trimmed = GameStateProjector.ApplyGoalToGo(s);

            Assert.IsTrue(trimmed.GoalToGo);
            Assert.That(trimmed.DistanceYards,
                Is.EqualTo(GameStateProjector.FirstDownYards).Within(YardTolerance));
        }

        [Test]
        public void ApplyGoalToGo_ForAway_MeasuresTowardTheHomeGoalLine()
        {
            var s = At(TeamSide.Away, 2, 8f, 6f);

            var trimmed = GameStateProjector.ApplyGoalToGo(s);

            Assert.IsTrue(trimmed.GoalToGo);
            Assert.That(trimmed.DistanceYards, Is.EqualTo(6f).Within(YardTolerance));
            Assert.AreEqual("2nd & Goal", trimmed.DownAndDistance());
        }

        [Test]
        public void Next_ConversionInsideTheTen_ReadsAsFirstAndGoal()
        {
            // The whole point of the trim, exercised through the fold the way a drive would hit it: a first
            // down that lands on the opponent's 6 must not put "1st & 10" on the wall.
            var atSnap = At(TeamSide.Home, 2, GameStateProjector.FirstDownYards, 80f);

            var next = GameStateProjector.Next(atSnap, Gain(PlayResultKind.PassComplete, 14f, firstDown: true));

            Assert.That(next.LineOfScrimmageYard, Is.EqualTo(94f).Within(YardTolerance));
            Assert.IsTrue(next.GoalToGo);
            Assert.That(next.DistanceYards, Is.EqualTo(6f).Within(YardTolerance));
            Assert.AreEqual("1st & Goal", next.DownAndDistance());
            Assert.AreEqual("OPP 6", next.BallOn());
        }

        [Test]
        public void ApplyGoalToGo_StaleFlagFarFromTheGoalLine_IsCleared()
        {
            // GoalToGo is recomputed from the spot every time rather than latched. A flag that only ever got
            // set would follow the offense back out to midfield and leave "& Goal" on the wall from the 40.
            var s = At(TeamSide.Home, 1, GameStateProjector.FirstDownYards, 40f);
            s.GoalToGo = true;

            var recomputed = GameStateProjector.ApplyGoalToGo(s);

            Assert.IsFalse(recomputed.GoalToGo);
            Assert.AreEqual("1st & 10", recomputed.DownAndDistance());
        }

        [Test]
        public void Next_TurnoverFromGoalToGo_ClearsTheFlagForTheNewOffense()
        {
            // The ball has not moved, but it is now ninety-four yards from the goal line that matters. This
            // is the case a latched flag gets wrong most visibly.
            var atSnap = At(TeamSide.Home, 1, 6f, 94f);
            atSnap.GoalToGo = true;

            var next = GameStateProjector.Next(atSnap, Gain(PlayResultKind.Turnover, 0f, firstDown: false));

            Assert.AreEqual(TeamSide.Away, next.Possession);
            Assert.IsFalse(next.GoalToGo);
            Assert.That(next.DistanceYards,
                Is.EqualTo(GameStateProjector.FirstDownYards).Within(YardTolerance));
            Assert.AreEqual("1st & 10", next.DownAndDistance());
            Assert.AreEqual("OWN 6", next.BallOn());
        }

        [Test]
        public void Next_OffensivePenaltyFromGoalToGo_StaysGoalToGo()
        {
            // Worth pinning because it looks like a bug and is not. Once goal-to-go, the distance IS the
            // distance to the goal line, so a mark-off that pushes the offense back adds the same yards to
            // both — 1st & Goal from the 6 becomes 1st & Goal from the 16, never "1st & 16".
            var atSnap = At(TeamSide.Home, 1, 6f, 94f);
            atSnap.GoalToGo = true;
            var outcome = new PlayOutcome
            {
                Kind = PlayResultKind.PenaltyNullified,
                HasPenalty = true,
                PenaltyName = "Offensive Holding",
                PenaltyYards = 10f,
                PenalizedSide = TeamSide.Home,
                AutomaticFirstDown = false,
                ReplayDown = true,
                ElapsedSeconds = 4f,
            };

            var next = GameStateProjector.Next(atSnap, outcome);

            Assert.That(next.LineOfScrimmageYard, Is.EqualTo(84f).Within(YardTolerance));
            Assert.AreEqual(1, next.Down);
            Assert.IsTrue(next.GoalToGo);
            Assert.That(next.DistanceYards, Is.EqualTo(16f).Within(YardTolerance));
            Assert.AreEqual("1st & Goal", next.DownAndDistance());
        }

        // ── the escape hatch ───────────────────────────────────────────────────────────────────────

        [Test]
        public void Next_SituationOverride_WinsOverEveryDerivedValue()
        {
            // The forced situation is taken verbatim — not folded, not clock-adjusted, not goal-to-go
            // trimmed. Everything else on this outcome is deliberately set to something that would produce a
            // different answer, so the test fails loudly if the override is ever merged rather than obeyed.
            var atSnap = At(TeamSide.Home, 1, GameStateProjector.FirstDownYards, 25f);
            var forced = new GameSituation
            {
                Down = 4,
                DistanceYards = GameStateProjector.FirstDownYards,
                LineOfScrimmageYard = 97f,
                Possession = TeamSide.Away,
                HomeScore = 21,
                AwayScore = 17,
                Quarter = 3,
                GameClockSeconds = 512f,
                PlayClockSeconds = 12f,
                HomeTimeouts = 1,
                AwayTimeouts = 0,
                GoalToGo = false,
                ClockRunning = false,
            };
            var outcome = new PlayOutcome
            {
                Kind = PlayResultKind.Touchdown,
                YardsGained = 40f,
                FirstDown = true,
                ElapsedSeconds = 30f,
                ClockRunsAfter = true,
                HasPenalty = true,
                PenaltyYards = 15f,
                SituationOverride = true,
                ForcedSituation = forced,
            };

            var next = GameStateProjector.Next(atSnap, outcome);

            Assert.AreEqual(4, next.Down);
            Assert.AreEqual(TeamSide.Away, next.Possession);
            Assert.AreEqual(21, next.HomeScore, "the touchdown kind must not add six on top of the override");
            Assert.AreEqual(17, next.AwayScore);
            Assert.AreEqual(3, next.Quarter);
            Assert.That(next.GameClockSeconds, Is.EqualTo(512f).Within(SecondTolerance),
                "the elapsed time is not subtracted from a forced clock");
            Assert.That(next.PlayClockSeconds, Is.EqualTo(12f).Within(SecondTolerance),
                "not even the play-clock reset applies");
            Assert.IsFalse(next.ClockRunning);
            Assert.That(next.LineOfScrimmageYard, Is.EqualTo(97f).Within(YardTolerance));
            Assert.IsFalse(next.GoalToGo,
                "three yards out with ten to gain would normally trim to goal-to-go; the override says otherwise");
            Assert.That(next.DistanceYards,
                Is.EqualTo(GameStateProjector.FirstDownYards).Within(YardTolerance));
        }

        [Test]
        public void Next_NullOutcome_ReturnsTheSnapSituationUnchanged()
        {
            var atSnap = At(TeamSide.Away, 3, 4f, 62f);

            var next = GameStateProjector.Next(atSnap, null);

            Assert.AreEqual(atSnap.Down, next.Down);
            Assert.AreEqual(atSnap.Possession, next.Possession);
            Assert.That(next.LineOfScrimmageYard,
                Is.EqualTo(atSnap.LineOfScrimmageYard).Within(YardTolerance));
            Assert.That(next.GameClockSeconds, Is.EqualTo(atSnap.GameClockSeconds).Within(SecondTolerance));
        }

        [Test]
        public void Next_IsPureAndLeavesTheSnapSituationUntouched()
        {
            // DriveLedger's whole path-independence claim rests on this: folding the same play twice, or
            // looping the drive, must not accumulate. A fold that mutated its input would double-count on
            // the second lap and nothing downstream would notice.
            var atSnap = At(TeamSide.Home, 3, 2f, 40f);
            var outcome = Gain(PlayResultKind.Rush, 5f, firstDown: true);

            var first = GameStateProjector.Next(atSnap, outcome);
            var second = GameStateProjector.Next(atSnap, outcome);

            Assert.AreEqual(3, atSnap.Down, "the snap situation is an input, not a buffer to fold into");
            Assert.That(atSnap.LineOfScrimmageYard, Is.EqualTo(40f).Within(YardTolerance));
            Assert.AreEqual(first.Down, second.Down);
            Assert.That(second.LineOfScrimmageYard,
                Is.EqualTo(first.LineOfScrimmageYard).Within(YardTolerance));
            Assert.That(second.GameClockSeconds, Is.EqualTo(first.GameClockSeconds).Within(SecondTolerance));
        }

        // ── the display helpers ────────────────────────────────────────────────────────────────────

        [Test]
        public void DownAndDistance_UsesTheOrdinalForEveryDown()
        {
            var s = At(TeamSide.Home, 1, GameStateProjector.FirstDownYards, 40f);

            s.Down = 1; Assert.AreEqual("1st & 10", s.DownAndDistance());
            s.Down = 2; Assert.AreEqual("2nd & 10", s.DownAndDistance());
            s.Down = 3; Assert.AreEqual("3rd & 10", s.DownAndDistance());
            s.Down = 4; Assert.AreEqual("4th & 10", s.DownAndDistance());
        }

        [Test]
        public void DownAndDistance_RoundsTheDistanceToAWholeYard()
        {
            // Distance is a float because the mark-offs are, but a scoreboard says "3rd & 7", never "3rd & 7.4".
            var s = At(TeamSide.Home, 3, 7.4f, 40f);
            Assert.AreEqual("3rd & 7", s.DownAndDistance());

            s.DistanceYards = 6.8f;
            Assert.AreEqual("3rd & 7", s.DownAndDistance());
        }

        [Test]
        public void DownAndDistance_GoalToGo_SaysGoalInsteadOfANumber()
        {
            var s = At(TeamSide.Home, 2, 6f, 94f);
            s.GoalToGo = true;

            Assert.AreEqual("2nd & Goal", s.DownAndDistance(),
                "the trimmed distance is still carried, but it is not what a wall shows");
        }

        [Test]
        public void BallOn_ReadsFromThePossessingTeamsPointOfView()
        {
            var home = At(TeamSide.Home, 1, GameStateProjector.FirstDownYards, 35f);
            Assert.AreEqual("OWN 35", home.BallOn());

            home.LineOfScrimmageYard = 65f;
            Assert.AreEqual("OPP 35", home.BallOn(), "the same painted 35, on the far side of midfield");

            // Absolute yards do not flip with possession, so the label has to.
            var away = At(TeamSide.Away, 1, GameStateProjector.FirstDownYards, 65f);
            Assert.AreEqual("OWN 35", away.BallOn());

            away.LineOfScrimmageYard = 35f;
            Assert.AreEqual("OPP 35", away.BallOn());
        }

        [Test]
        public void BallOn_AtMidfield_IsJustFifty()
        {
            // Neither "OWN 50" nor "OPP 50" is how anyone says it, and the two would disagree across a
            // possession change for a ball that never moved.
            var home = At(TeamSide.Home, 1, GameStateProjector.FirstDownYards, FieldGeometry.MidfieldYard);
            var away = At(TeamSide.Away, 1, GameStateProjector.FirstDownYards, FieldGeometry.MidfieldYard);

            Assert.AreEqual("50", home.BallOn());
            Assert.AreEqual("50", away.BallOn());
        }

        [Test]
        public void BallOn_SnapsToTheNearestPaintedLine()
        {
            // The ball is spotted wherever it is spotted; the readout names a yard line that exists.
            var s = At(TeamSide.Home, 1, GameStateProjector.FirstDownYards, 35.4f);
            Assert.AreEqual("OWN 35", s.BallOn());

            s.LineOfScrimmageYard = 3f;
            Assert.AreEqual("OWN 3", s.BallOn());
        }

        [Test]
        public void GameClock_PadsTheSecondsToTwoDigits()
        {
            var s = At(TeamSide.Home, 1, GameStateProjector.FirstDownYards, 40f);

            s.GameClockSeconds = 724f;
            Assert.AreEqual("12:04", s.GameClock(), "twelve minutes and four seconds, not \"12:4\"");

            s.GameClockSeconds = 60f;
            Assert.AreEqual("1:00", s.GameClock());

            s.GameClockSeconds = 9f;
            Assert.AreEqual("0:09", s.GameClock());
        }

        [Test]
        public void GameClock_TruncatesRatherThanRounding()
        {
            // A clock that rounds up shows 15:00 for a fifteen-minute quarter that has already started, and
            // shows 0:00 while there is still most of a second left. Both are wrong on a wall.
            var s = At(TeamSide.Home, 1, GameStateProjector.FirstDownYards, 40f);

            s.GameClockSeconds = 599.9f;
            Assert.AreEqual("9:59", s.GameClock());

            s.GameClockSeconds = 0.9f;
            Assert.AreEqual("0:00", s.GameClock());
        }

        [Test]
        public void GameClock_NegativeSeconds_ReadAsZero()
        {
            var s = At(TeamSide.Home, 1, GameStateProjector.FirstDownYards, 40f);
            s.GameClockSeconds = -5f;

            Assert.AreEqual("0:00", s.GameClock(), "the readout clamps even if something upstream did not");
        }

        [Test]
        public void YardsToGoal_IsNeverNegativeOnEitherSide()
        {
            // Reached from either end zone, since the fold can legitimately spot the ball behind a goal line
            // after a big loss and ApplyGoalToGo compares straight against this value. A negative here would
            // trim the yards to gain to a negative number and put "& Goal" on a play that was never close.
            var home = At(TeamSide.Home, 1, GameStateProjector.FirstDownYards, 105f);
            var away = At(TeamSide.Away, 1, GameStateProjector.FirstDownYards, -5f);

            Assert.That(home.YardsToGoal, Is.EqualTo(0f).Within(YardTolerance));
            Assert.That(away.YardsToGoal, Is.EqualTo(0f).Within(YardTolerance));
        }
    }
}
