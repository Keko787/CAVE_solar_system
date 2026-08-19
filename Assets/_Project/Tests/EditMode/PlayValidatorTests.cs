using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Stadium3D.Football;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// The designer guard, held to both halves of its contract: a sound play must produce nothing, and every
    /// mistake this content pipeline can actually make must produce something. Both halves matter equally. A
    /// validator that cries on clean content teaches everyone to ignore it, and one that stays quiet on a
    /// broken play is worse than no validator at all — the faults it exists to catch (a throw nothing ever
    /// closes, a body aligned three yards into the stands, an event scheduled after the whistle) are invisible
    /// at the desk and unmissable once eleven markers are moving on the CAVE walls, by which point the fix
    /// costs a rebuild rather than a keystroke.
    /// </summary>
    /// <remarks>
    /// Each broken variant is the valid play with exactly <em>one</em> thing wrong, so a failure here names the
    /// check that regressed rather than a pile of them at once. The assertions match a fragment of the message
    /// rather than the whole string: the wording is a readout for a human and will be reworded, but which check
    /// fires, and whether it says which player or which event it is about, must not drift.
    /// </remarks>
    public class PlayValidatorTests
    {
        // The smallest roster that can still be wrong in interesting ways: a passer, a target, and a defender
        // so the "everyone on the roster is assigned" check has both sides to walk.
        const int QuarterbackNumber = 12;
        const int ReceiverNumber = 80;
        const int CornerbackNumber = 24;

        /// <summary>A jersey deliberately absent from the roster, for the unknown-id cases.</summary>
        const int StrangerNumber = 99;

        const float LineOfScrimmage = 35f;

        // ── fixtures ───────────────────────────────────────────────────────────────────────────────

        static Roster BuildRoster() => new Roster(new[]
        {
            new PlayerIdentity
            {
                Number = QuarterbackNumber, Side = TeamSide.Home,
                Position = PlayerPosition.Quarterback, Name = "Passer",
            },
            new PlayerIdentity
            {
                Number = ReceiverNumber, Side = TeamSide.Home,
                Position = PlayerPosition.WideReceiver, Name = "Target",
            },
            new PlayerIdentity
            {
                Number = CornerbackNumber, Side = TeamSide.Away,
                Position = PlayerPosition.Cornerback, Name = "Cover",
            },
        });

        static RouteWaypoint At(float depthYards, float lateralYards,
                                WaypointKind kind = WaypointKind.Through) => new RouteWaypoint
        {
            DepthYards = depthYards,
            LateralYards = lateralYards,
            Kind = kind,
        };

        /// <summary>
        /// A play with nothing wrong with it: every roster member assigned, every alignment on the field, a
        /// snap at t = 0, a throw closed by a catch, and an outcome that agrees with the ball events. Every
        /// other test in this fixture starts here and breaks one thing.
        /// </summary>
        static PlayScript BuildValidPlay() => new PlayScript
        {
            Name = "Minimal Sound Play",
            Formation = "Gun Trips Right",
            DefensiveCall = "Cover 3",
            Offense = TeamSide.Home,
            LineOfScrimmageYard = LineOfScrimmage,
            BallLateralYards = FieldGeometry.HashLateralYards,
            MinDurationSeconds = 3f,

            Players = new List<PlayerScript>
            {
                new PlayerScript
                {
                    Number = QuarterbackNumber, Side = TeamSide.Home, RouteName = "five step drop",
                    Waypoints = new List<RouteWaypoint>
                    {
                        At(-1f, 0f),
                        At(-6f, 0f, WaypointKind.Stop),
                    },
                },
                new PlayerScript
                {
                    Number = ReceiverNumber, Side = TeamSide.Home, RouteName = "dig",
                    Waypoints = new List<RouteWaypoint>
                    {
                        At(0f, 12f),
                        At(10f, 12f, WaypointKind.Cut),
                        At(10f, 3f, WaypointKind.Stop),
                    },
                },
                new PlayerScript
                {
                    Number = CornerbackNumber, Side = TeamSide.Away, RouteName = "cover 3 deep third",
                    Waypoints = new List<RouteWaypoint>
                    {
                        At(7f, 12f),
                        At(18f, 11f, WaypointKind.Stop),
                    },
                },
            },

            BallEvents = new List<BallEvent>
            {
                new BallEvent
                {
                    Time = 0f, Kind = BallEventKind.Snap,
                    Side = TeamSide.Home, ToNumber = QuarterbackNumber,
                },
                new BallEvent
                {
                    Time = 2f, Kind = BallEventKind.Throw, Side = TeamSide.Home,
                    FromNumber = QuarterbackNumber, ToNumber = ReceiverNumber, ApexMeters = 3.5f,
                },
                new BallEvent
                {
                    Time = 3.1f, Kind = BallEventKind.Catch,
                    Side = TeamSide.Home, ToNumber = ReceiverNumber,
                },
                new BallEvent
                {
                    Time = 4.2f, Kind = BallEventKind.Down,
                    Side = TeamSide.Home, FromNumber = ReceiverNumber,
                },
            },

            Outcome = new PlayOutcome
            {
                Kind = PlayResultKind.PassComplete,
                YardsGained = 12f,
                FirstDown = true,
                ElapsedSeconds = 6f,
                ClockRunsAfter = true,
                PasserNumber = QuarterbackNumber,
                BallCarrierNumber = ReceiverNumber,
                HasPenalty = false,
                PenaltyName = "",
            },
        };

        static PlayerScript AssignmentFor(PlayScript play, TeamSide side, int number) =>
            play.ScriptFor(PlayerIdentity.PlayerKey(side, number));

        /// <summary>The first event of a kind, so a test never depends on an index into the builder.</summary>
        static BallEvent EventOfKind(PlayScript play, BallEventKind kind)
        {
            for (int i = 0; i < play.BallEvents.Count; i++)
                if (play.BallEvents[i] != null && play.BallEvents[i].Kind == kind)
                    return play.BallEvents[i];
            return null;
        }

        static bool Mentions(List<string> errors, string fragment)
        {
            for (int i = 0; i < errors.Count; i++)
                if (errors[i] != null && errors[i].Contains(fragment))
                    return true;
            return false;
        }

        /// <summary>Every message on one line, so a failed assertion says what the validator actually said.</summary>
        static string Joined(List<string> errors) =>
            errors == null || errors.Count == 0 ? "<no errors reported>" : string.Join(" | ", errors);

        // ── the clean case ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void SoundPlay_ReportsNothing()
        {
            var errors = PlayValidator.Validate(BuildValidPlay(), BuildRoster());

            // This is the assertion the rest of the fixture leans on: if the baseline ever starts producing
            // messages, every "broken variant" test below stops proving anything about its own break.
            Assert.That(errors, Is.Empty, Joined(errors));
        }

        [Test]
        public void NullRoster_SkipsRosterChecksRatherThanThrowing()
        {
            // The roster parameter is optional in the signature, and a bake-time caller can legitimately have
            // no roster yet. Everything that does not need one must still be checked, and nothing must throw.
            var errors = PlayValidator.Validate(BuildValidPlay(), null);

            Assert.That(errors, Is.Empty, Joined(errors));
        }

        // ── ball events ────────────────────────────────────────────────────────────────────────────

        [Test]
        public void ThrowWithNoCatchOrIncompletion_IsReported()
        {
            var play = BuildValidPlay();
            play.BallEvents.Remove(EventOfKind(play, BallEventKind.Catch));

            var errors = PlayValidator.Validate(play, BuildRoster());

            Assert.That(errors.Count, Is.GreaterThan(0), Joined(errors));
            Assert.IsTrue(Mentions(errors, "never caught or ruled incomplete"), Joined(errors));
        }

        [Test]
        public void BallEventBeforeTheSnap_IsReported()
        {
            var play = BuildValidPlay();
            EventOfKind(play, BallEventKind.Throw).Time = -0.5f;

            var errors = PlayValidator.Validate(play, BuildRoster());

            // A negative time is necessarily also out of order behind the snap, so this variant trips two
            // checks. Only the one it is named for is asserted; the other is somebody else's test.
            Assert.That(errors.Count, Is.GreaterThan(0), Joined(errors));
            Assert.IsTrue(Mentions(errors, "before the snap"), Joined(errors));
        }

        [Test]
        public void FirstEventThatIsNotASnap_IsReported()
        {
            var play = BuildValidPlay();
            var first = play.BallEvents[0];
            first.Kind = BallEventKind.Handoff;
            first.FromNumber = QuarterbackNumber;   // keep the ids sound, so the missing snap is the only fault

            var errors = PlayValidator.Validate(play, BuildRoster());

            Assert.That(errors.Count, Is.GreaterThan(0), Joined(errors));
            Assert.IsTrue(Mentions(errors, "first ball event must be a Snap"), Joined(errors));
        }

        [Test]
        public void SnapAwayFromZero_IsReported()
        {
            var play = BuildValidPlay();
            play.BallEvents[0].Time = 0.4f;

            var errors = PlayValidator.Validate(play, BuildRoster());

            // Every other time on the play is measured from the snap, so a snap that is not the origin quietly
            // shifts the whole play's timeline rather than failing anywhere visible.
            Assert.That(errors.Count, Is.GreaterThan(0), Joined(errors));
            Assert.IsTrue(Mentions(errors, "snap must be at t = 0"), Joined(errors));
        }

        [Test]
        public void EventsOutOfTimeOrder_AreReported()
        {
            var play = BuildValidPlay();
            EventOfKind(play, BallEventKind.Catch).Time = 1f;   // the throw is still authored at 2 s

            var errors = PlayValidator.Validate(play, BuildRoster());

            Assert.That(errors.Count, Is.GreaterThan(0), Joined(errors));
            Assert.IsTrue(Mentions(errors, "out of time order"), Joined(errors));
        }

        [Test]
        public void EventNamingAPlayerNotOnTheRoster_IsReported()
        {
            var play = BuildValidPlay();
            EventOfKind(play, BallEventKind.Catch).ToNumber = StrangerNumber;

            var errors = PlayValidator.Validate(play, BuildRoster());

            // The jersey has to survive into the message: "someone is not on the roster" sends a designer
            // hunting through twenty-two assignments.
            Assert.That(errors.Count, Is.GreaterThan(0), Joined(errors));
            Assert.IsTrue(Mentions(errors, $"goes to #{StrangerNumber}"), Joined(errors));
        }

        [Test]
        public void PlayWithNoBallEvents_IsReported()
        {
            var play = BuildValidPlay();
            play.BallEvents.Clear();

            var errors = PlayValidator.Validate(play, BuildRoster());

            Assert.That(errors.Count, Is.GreaterThan(0), Joined(errors));
            Assert.IsTrue(Mentions(errors, "no ball events"), Joined(errors));
        }

        // ── roster coverage and alignment ──────────────────────────────────────────────────────────

        [Test]
        public void RosterPlayerWithNoAssignment_IsReported()
        {
            var play = BuildValidPlay();
            play.Players.Remove(AssignmentFor(play, TeamSide.Away, CornerbackNumber));

            var errors = PlayValidator.Validate(play, BuildRoster());

            // An unassigned body does not vanish — it stands on the ball spot, which reads as a player
            // planted on the line doing nothing rather than as missing content.
            Assert.That(errors.Count, Is.GreaterThan(0), Joined(errors));
            Assert.IsTrue(Mentions(errors, "is on the roster but has no assignment"), Joined(errors));
        }

        [Test]
        public void AssignmentForSomeoneNotOnTheRoster_IsReported()
        {
            var play = BuildValidPlay();
            AssignmentFor(play, TeamSide.Away, CornerbackNumber).Number = StrangerNumber;

            var errors = PlayValidator.Validate(play, BuildRoster());

            Assert.That(errors.Count, Is.GreaterThan(0), Joined(errors));
            Assert.IsTrue(Mentions(errors, "has an assignment but is not on the roster"), Joined(errors));
        }

        [Test]
        public void DuplicateAssignmentForOnePlayer_IsReported()
        {
            var play = BuildValidPlay();
            play.Players.Add(new PlayerScript
            {
                Number = ReceiverNumber, Side = TeamSide.Home, RouteName = "pasted twice",
                Waypoints = new List<RouteWaypoint> { At(0f, 12f), At(6f, 12f, WaypointKind.Stop) },
            });

            var errors = PlayValidator.Validate(play, BuildRoster());

            // The bake keeps whichever copy it saw first and silently drops the rest, so the second route
            // simply never runs. Nothing downstream can tell that apart from a route authored badly.
            Assert.That(errors.Count, Is.GreaterThan(0), Joined(errors));
            Assert.IsTrue(Mentions(errors, "more than one assignment"), Joined(errors));
        }

        [Test]
        public void AssignmentWithNoWaypoints_IsReported()
        {
            var play = BuildValidPlay();
            AssignmentFor(play, TeamSide.Home, ReceiverNumber).Waypoints.Clear();

            var errors = PlayValidator.Validate(play, BuildRoster());

            Assert.That(errors.Count, Is.GreaterThan(0), Joined(errors));
            Assert.IsTrue(Mentions(errors, "no waypoints"), Joined(errors));
        }

        [Test]
        public void AlignmentOutOfBounds_IsReported()
        {
            var play = BuildValidPlay();
            var target = AssignmentFor(play, TeamSide.Home, ReceiverNumber);

            // Derived from the field's own half-width rather than a typed literal, so this stays out of bounds
            // if the sideline ever moves.
            target.Waypoints[0].LateralYards = FieldGeometry.HalfWidthYards + 5f;

            Vector3 spot = target.Waypoints[0]
                .ToPitchLocal(play.LineOfScrimmageYard, play.BallLateralYards, play.Offense);
            Assert.IsFalse(FieldGeometry.InBounds(spot),
                "the fixture has to actually put him off the field for this test to mean anything");

            var errors = PlayValidator.Validate(play, BuildRoster());

            Assert.That(errors.Count, Is.GreaterThan(0), Joined(errors));
            Assert.IsTrue(Mentions(errors, "aligns out of bounds"), Joined(errors));
        }

        // ── the outcome ────────────────────────────────────────────────────────────────────────────

        [Test]
        public void NullOutcome_IsReported()
        {
            var play = BuildValidPlay();
            play.Outcome = null;

            var errors = PlayValidator.Validate(play, BuildRoster());

            // Without an outcome the drive fold has nothing to advance on, so the scoreboard sits on the same
            // down for the whole drive while the field keeps running plays.
            Assert.That(errors.Count, Is.GreaterThan(0), Joined(errors));
            Assert.IsTrue(Mentions(errors, "no outcome"), Joined(errors));
        }

        [Test]
        public void PenaltyWithNoName_IsReported()
        {
            var play = BuildValidPlay();
            play.Outcome.HasPenalty = true;
            play.Outcome.PenaltyName = "";

            var errors = PlayValidator.Validate(play, BuildRoster());

            Assert.That(errors.Count, Is.GreaterThan(0), Joined(errors));
            Assert.IsTrue(Mentions(errors, "penalty is flagged but unnamed"), Joined(errors));
        }

        [Test]
        public void IncompleteOutcomeWithNoIncompleteEvent_IsReported()
        {
            var play = BuildValidPlay();
            play.Outcome.Kind = PlayResultKind.PassIncomplete;   // the ball events still show a catch

            var errors = PlayValidator.Validate(play, BuildRoster());

            // The two disagree about the same instant: the wall would post an incompletion while the ball is
            // visibly in a receiver's hands on the field view.
            Assert.That(errors.Count, Is.GreaterThan(0), Joined(errors));
            Assert.IsTrue(Mentions(errors, "no Incomplete event exists"), Joined(errors));
        }

        [Test]
        public void IncompleteEventUnderACompleteOutcome_IsReported()
        {
            var play = BuildValidPlay();
            EventOfKind(play, BallEventKind.Catch).Kind = BallEventKind.Incomplete;
            play.BallEvents.Remove(EventOfKind(play, BallEventKind.Down));

            var errors = PlayValidator.Validate(play, BuildRoster());

            Assert.That(errors.Count, Is.GreaterThan(0), Joined(errors));
            Assert.IsTrue(Mentions(errors, "ball events say otherwise"), Joined(errors));
        }

        // ── nulls, which must be reported rather than thrown ───────────────────────────────────────

        [Test]
        public void NullPlay_IsReportedRatherThanThrowing()
        {
            var errors = PlayValidator.Validate(null, BuildRoster());

            // The whole point of returning messages is that a build step can print everything wrong at once.
            // An exception on the first bad entry gives one fault per run instead.
            Assert.That(errors.Count, Is.GreaterThan(0), Joined(errors));
            Assert.IsTrue(Mentions(errors, "null"), Joined(errors));
        }

        [Test]
        public void NullEntries_AreReportedRatherThanThrowing()
        {
            var roster = BuildRoster();

            var missingAssignment = BuildValidPlay();
            missingAssignment.Players.Add(null);
            var assignmentErrors = PlayValidator.Validate(missingAssignment, roster);

            Assert.That(assignmentErrors.Count, Is.GreaterThan(0), Joined(assignmentErrors));
            Assert.IsTrue(Mentions(assignmentErrors, "player assignment"), Joined(assignmentErrors));

            var missingEvent = BuildValidPlay();
            missingEvent.BallEvents.Add(null);
            var eventErrors = PlayValidator.Validate(missingEvent, roster);

            Assert.That(eventErrors.Count, Is.GreaterThan(0), Joined(eventErrors));
            Assert.IsTrue(Mentions(eventErrors, "is null"), Joined(eventErrors));
        }

        // ── against a bake, where the play has a real length ───────────────────────────────────────

        [Test]
        public void SoundPlay_StaysCleanAgainstItsOwnBake()
        {
            var roster = BuildRoster();
            var play = BuildValidPlay();
            var baked = BakedPlay.Bake(play, roster, 60f);

            // Structural, not a hand-computed number: the derived duration is floored by the last ball event
            // plus its tail, so a play can never bake shorter than its own event list.
            Assert.That(baked.Duration,
                Is.GreaterThanOrEqualTo(play.BallEvents[play.BallEvents.Count - 1].Time - 1e-3f));

            var errors = PlayValidator.Validate(play, roster, baked);
            Assert.That(errors, Is.Empty, Joined(errors));
        }

        [Test]
        public void EventAfterThePlayEnds_IsReportedOnceThereIsABake()
        {
            var roster = BuildRoster();
            var play = BuildValidPlay();
            var baked = BakedPlay.Bake(play, roster, 60f);

            // Pushed past the whistle after the bake, so the recorded duration is the one the validator sees.
            play.BallEvents[play.BallEvents.Count - 1].Time = baked.Duration + 2f;

            var errors = PlayValidator.Validate(play, roster, baked);

            Assert.That(errors.Count, Is.GreaterThan(0), Joined(errors));
            Assert.IsTrue(Mentions(errors, "after the play ends"), Joined(errors));
        }

        [Test]
        public void EventPastTheEnd_IsInvisibleWithoutABake()
        {
            var play = BuildValidPlay();
            play.BallEvents[play.BallEvents.Count - 1].Time = 900f;

            var errors = PlayValidator.Validate(play, BuildRoster());

            // With no bake there is no duration to compare against, so this check cannot fire. Pinned because
            // it is a real limit of the desk-time pass, not an oversight: only a baked drive can catch it.
            Assert.That(errors, Is.Empty, Joined(errors));
        }
    }
}
