using System;
using System.Collections.Generic;
using Stadium3D.Football;
using NUnit.Framework;
using UnityEngine;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// The transport, pinned without a bake behind it. Everything a person does to a scripted drive in the
    /// CAVE — freezing on a frame, replaying the snap they just missed, dropping the ball flight to half
    /// rate so there is time to turn towards another wall, leaving the drive looping unattended — is this
    /// one class moving five scalars, and each of those actions has to leave the drive standing on a beat
    /// rather than a little way off one. The failures worth guarding against here are the quiet ones: a
    /// play that ends a frame late because the frame was heavy, a remainder that carries across a phase
    /// change until the loop is a play out of step with the commentary, a seam that announces itself twice.
    /// None of that is visible in a single session; it shows up an hour into an unattended demo.
    /// </summary>
    /// <remarks>
    /// The duration comes from a lambda rather than the baker on purpose. A play's real length is derived
    /// from its bake, but that is a different question from whether the clock walks correctly, and mixing
    /// the two would make a transport regression look like a route-solve regression.
    /// </remarks>
    public class DriveDirectorTests
    {
        const float HuddleBeat = 1.5f;
        const float PostPlayBeat = 1f;
        const float PlayDuration = 2f;
        const int PlayCount = 3;

        // A deliberately irregular schedule: two render frames, a long frame, a stall, a step longer than
        // a whole phase. Nothing in the transport is allowed to depend on the shape of it.
        static readonly double[] Schedule = { 0.016, 0.016, 0.25, 0.7, 0.016, 1.3, 0.05 };

        // Two players is enough to make the drive well formed; the transport never looks at a roster.
        static Roster TwoPlayerRoster() => new Roster(
            new[]
            {
                new PlayerIdentity { Number = 12, Name = "Reyes",  Position = PlayerPosition.Quarterback, Side = TeamSide.Home },
                new PlayerIdentity { Number = 24, Name = "Okafor", Position = PlayerPosition.Cornerback,  Side = TeamSide.Away },
            },
            "Bay City", "Northside", "BAY", "NOR");

        static PlayScript TrivialPlay(int index) => new PlayScript
        {
            Name = $"Play {index}",
            Offense = TeamSide.Home,
            LineOfScrimmageYard = FieldGeometry.MidfieldYard,
            Players = new List<PlayerScript>
            {
                new PlayerScript { Number = 12, Side = TeamSide.Home, RouteName = "drop" },
                new PlayerScript { Number = 24, Side = TeamSide.Away, RouteName = "press" },
            },
        };

        static DriveScript Drive(bool loop = true, int playCount = PlayCount)
        {
            var plays = new PlayScript[playCount];
            for (int i = 0; i < playCount; i++) plays[i] = TrivialPlay(i);

            return new DriveScript(
                TwoPlayerRoster(),
                plays,
                GameSituation.Kickoff(TeamSide.Home, FieldGeometry.MidfieldYard),
                HuddleBeat, PostPlayBeat, loop);
        }

        static DriveDirector Director(DriveScript drive) => new DriveDirector(drive, _ => PlayDuration);

        static void RunSchedule(DriveDirector director)
        {
            for (int i = 0; i < Schedule.Length; i++) director.Tick(Schedule[i]);
        }

        /// <summary>Tick until <paramref name="stop"/> holds, failing rather than spinning if it never does.</summary>
        static void TickUntil(DriveDirector director, Func<bool> stop, double dt, int maxTicks = 10000)
        {
            for (int i = 0; i < maxTicks; i++)
            {
                if (stop()) return;
                director.Tick(dt);
            }

            Assert.Fail($"the transport never reached the expected state in {maxTicks} ticks of {dt}s");
        }

        static void AssertSameSnapshot(DriveSnapshot expected, DriveSnapshot actual, string because = "")
        {
            Assert.AreEqual(expected.PlayIndex, actual.PlayIndex, $"PlayIndex — {because}");
            Assert.AreEqual(expected.PreviousPlayIndex, actual.PreviousPlayIndex, $"PreviousPlayIndex — {because}");
            Assert.AreEqual(expected.Phase, actual.Phase, $"Phase — {because}");

            // Both sides come out of the same double arithmetic, so these are in fact exact; the tolerance
            // is here only because comparing floating point without one is a habit worth not breaking.
            Assert.That(actual.PlayTime, Is.EqualTo(expected.PlayTime).Within(1e-9), $"PlayTime — {because}");
            Assert.That(actual.PhaseTime, Is.EqualTo(expected.PhaseTime).Within(1e-9), $"PhaseTime — {because}");
        }

        static bool Differs(DriveSnapshot a, DriveSnapshot b) =>
            a.PlayIndex != b.PlayIndex
            || a.PreviousPlayIndex != b.PreviousPlayIndex
            || a.Phase != b.Phase
            || Math.Abs(a.PlayTime - b.PlayTime) > 1e-9
            || Math.Abs(a.PhaseTime - b.PhaseTime) > 1e-9;

        [Test]
        public void NewDirector_StartsInTheHuddleBeforePlayZero()
        {
            var director = Director(Drive());

            Assert.AreEqual(0, director.PlayIndex);
            Assert.AreEqual(-1, director.PreviousPlayIndex, "nothing precedes play 0, so there is no pose to walk from");
            Assert.AreEqual(PlayPhase.Huddle, director.Phase);
            Assert.AreEqual(0f, director.PlayTime, 1e-3f);
            Assert.AreEqual(0f, director.PhaseTime, 1e-3f);
            Assert.IsFalse(director.Paused);
            Assert.AreEqual(1f, director.TimeScale, 1e-3f);
        }

        [Test]
        public void PhaseWalk_VisitsHuddleLivePostPlayThenTheNextHuddleOnTheAuthoredBeats()
        {
            var director = Director(Drive());
            int snapped = -1;
            int ended = -1;
            director.PlaySnapped += i => snapped = i;
            director.PlayEnded += i => ended = i;

            // Just short of the huddle beat, the field is still gathering.
            director.Tick(HuddleBeat - 0.1);
            Assert.AreEqual(PlayPhase.Huddle, director.Phase);
            Assert.AreEqual(-1, snapped, "no snap before the huddle beat is spent");

            // Crossing it snaps play 0. The phase clock restarts at zero rather than carrying the overshoot,
            // which is what keeps a heavy frame from shortening the next phase to compensate.
            director.Tick(0.2);
            Assert.AreEqual(PlayPhase.Live, director.Phase);
            Assert.AreEqual(0, snapped, "the snap announces which play went live");
            Assert.AreEqual(0f, director.PlayTime, 1e-3f);
            Assert.AreEqual(0f, director.PhaseTime, 1e-3f);

            // Live runs against the play's own duration, not the beat that started it.
            director.Tick(PlayDuration - 0.1);
            Assert.AreEqual(PlayPhase.Live, director.Phase);
            Assert.AreEqual(-1, ended);

            director.Tick(0.2);
            Assert.AreEqual(PlayPhase.PostPlay, director.Phase);
            Assert.AreEqual(0, ended, "the whistle announces the play that just finished");
            Assert.AreEqual(PlayDuration, director.PlayTime, 1e-3f);

            // The result holds on the walls for the post-play beat before the next huddle starts.
            director.Tick(PostPlayBeat - 0.1);
            Assert.AreEqual(PlayPhase.PostPlay, director.Phase);
            Assert.AreEqual(0, director.PlayIndex, "the index only moves once the result has had its beat");

            director.Tick(0.2);
            Assert.AreEqual(PlayPhase.Huddle, director.Phase);
            Assert.AreEqual(1, director.PlayIndex);
            Assert.AreEqual(0, director.PreviousPlayIndex, "the huddle blend needs the play the bodies are walking from");
            Assert.AreEqual(0f, director.PlayTime, 1e-3f);
        }

        [Test]
        public void PlayEnds_AtExactlyItsDuration_WhateverTheDtSchedule()
        {
            // Three ways the same play can be stepped through: a steady 60 Hz frame, a badly stuttering one,
            // and a single step longer than the whole drive — an editor pause, or a headset waking up.
            var fine = RunToWhistle(0.016);
            var coarse = RunToWhistle(0.25);
            var single = RunToWhistle(PlayDuration * 20.0);

            Assert.AreEqual(PlayPhase.PostPlay, fine.Phase);
            Assert.AreEqual(0, fine.PlayIndex);
            Assert.That((float)fine.PlayTime, Is.EqualTo(PlayDuration).Within(1e-6f),
                "the whistle lands on the duration itself, not a frame past it");

            // Nothing in the state at the whistle records how it was reached: the phase clock is zeroed on
            // the transition and play time is assigned, so all three schedules stand on identical ground.
            AssertSameSnapshot(fine, coarse, "a stuttering frame must land where a smooth one does");
            AssertSameSnapshot(fine, single, "one enormous step must land there too");
        }

        static DriveSnapshot RunToWhistle(double dt)
        {
            var director = Director(Drive());
            TickUntil(director, () => director.Phase == PlayPhase.PostPlay, dt);
            return director.Capture();
        }

        [Test]
        public void PlayTime_ClampsAtDurationRatherThanWrapping()
        {
            var director = Director(Drive());
            director.SeekToPlay(0, 0f);

            // A step a hundred times the play. Wrapping the remainder would restart the play looking like a
            // fresh snap; clamping ends it.
            director.Tick(PlayDuration * 100.0);

            Assert.AreEqual(PlayPhase.PostPlay, director.Phase);
            Assert.AreEqual(PlayDuration, director.PlayTime, 1e-6f);
            Assert.That(director.PlayTime, Is.LessThanOrEqualTo(PlayDuration));

            // And the same holds every tick of a long run, across plays and the loop seam: play time is
            // never observed beyond the duration, because the clamp happens in the tick that crosses it.
            var running = Director(Drive());
            for (int i = 0; i < 400; i++)
            {
                running.Tick(0.05);
                Assert.That(running.PlayTime, Is.LessThanOrEqualTo(PlayDuration + 1e-4f),
                    $"play time ran past the duration on tick {i}");
                Assert.That(running.PlayTime, Is.GreaterThanOrEqualTo(0f));
            }
        }

        [Test]
        public void ReplayCurrentPlay_RewindsToThisPlaysHuddleWithoutMovingTheIndex()
        {
            var director = Director(Drive());
            director.SeekToPlay(1, PlayDuration * 0.5f);
            Assert.AreEqual(PlayPhase.Live, director.Phase, "the fixture wants a play that is genuinely under way");

            director.ReplayCurrentPlay();

            Assert.AreEqual(1, director.PlayIndex, "a replay is this play again, not the one before it");
            Assert.AreEqual(0f, director.PlayTime, 1e-3f);
            Assert.AreEqual(0f, director.PhaseTime, 1e-3f);
            Assert.AreEqual(PlayPhase.Huddle, director.Phase,
                "the replay starts from the huddle, so the bodies walk back to the stance instead of popping into it");

            // The claim the class makes for itself: seeking assigns time rather than winding it, so a replay
            // is the same arithmetic as the first pass. A director set up the long way round agrees exactly.
            var fresh = Director(Drive());
            fresh.SeekToPlay(1);
            AssertSameSnapshot(fresh.Capture(), director.Capture(), "a replay and a fresh seek are the same state");

            RunSchedule(fresh);
            RunSchedule(director);
            AssertSameSnapshot(fresh.Capture(), director.Capture(), "and they stay the same state under the same dt schedule");
        }

        [Test]
        public void Loop_WrapsFromTheLastPlayToZeroAndAnnouncesTheSeamOnce()
        {
            var director = Director(Drive(loop: true));
            int loops = 0;
            int completed = 0;
            director.Looped += () => loops++;
            director.DriveCompleted += () => completed++;

            TickUntil(director, () => loops > 0, 0.25);

            Assert.AreEqual(1, loops);
            Assert.AreEqual(0, director.PlayIndex, "the lap restarts at play 0");
            Assert.AreEqual(PlayCount - 1, director.PreviousPlayIndex,
                "across the seam the bodies still walk in from the last play, rather than teleporting");
            Assert.AreEqual(PlayPhase.Huddle, director.Phase);
            Assert.AreEqual(0f, director.PlayTime, 1e-3f);
            Assert.AreEqual(0, completed, "a looping drive never completes");

            // A little way into the second lap. An attract loop runs this seam for hours, so a seam that
            // re-announces itself would flood whatever is listening.
            for (int i = 0; i < 8; i++) director.Tick(0.25);
            Assert.AreEqual(1, loops);
            Assert.AreEqual(0, director.PlayIndex);
        }

        [Test]
        public void NonLoopingDrive_ParksOnDriveCompleteAndStaysThere()
        {
            var director = Director(Drive(loop: false));
            int completed = 0;
            int loops = 0;
            director.DriveCompleted += () => completed++;
            director.Looped += () => loops++;

            director.SeekToPlay(PlayCount - 1, 0f);
            TickUntil(director, () => director.Phase == PlayPhase.DriveComplete, 0.25);

            Assert.AreEqual(1, completed);
            Assert.AreEqual(0, loops, "a drive that is not looping must not take the seam");
            Assert.AreEqual(PlayCount - 1, director.PlayIndex,
                "the index parks on the last play rather than running off the end of the list");

            // Terminal means terminal: twenty-five seconds of ticks change nothing at all.
            var parked = director.Capture();
            for (int i = 0; i < 100; i++) director.Tick(0.25);

            AssertSameSnapshot(parked, director.Capture(), "DriveComplete absorbs every tick after it");
            Assert.AreEqual(1, completed, "the completion is announced once, not on every tick that follows");
        }

        [Test]
        public void CaptureRestore_RoundTripsTheTransportExactly()
        {
            var director = Director(Drive());
            for (int i = 0; i < 23; i++) director.Tick(0.25);   // somewhere mid-drive, not on a beat
            var saved = director.Capture();

            RunSchedule(director);
            var firstPass = director.Capture();
            Assert.IsTrue(Differs(saved, firstPass),
                "the schedule has to actually move the transport, or the restore below proves nothing");

            director.Restore(saved);
            AssertSameSnapshot(saved, director.Capture(),
                "restore puts every scalar back — and the captured value survived the ticks in between");

            // The point of capturing five scalars rather than a scene: replaying from a restored state is
            // the same arithmetic, so an attract-mode reset lands on the frame it landed on last time.
            RunSchedule(director);
            AssertSameSnapshot(firstPass, director.Capture(), "the second pass over the same schedule is bit-identical");
        }

        [Test]
        public void Paused_FreezesIndexTimeAndPhaseAcrossAHundredTicks()
        {
            var director = Director(Drive());
            for (int i = 0; i < 9; i++) director.Tick(0.25);
            Assert.AreEqual(PlayPhase.Live, director.Phase, "the fixture wants to freeze mid-play, not between plays");

            director.SetPaused(true);
            Assert.IsTrue(director.Paused);
            var frozen = director.Capture();

            // Twenty-five seconds of wall clock — nearly two whole laps of this drive.
            for (int i = 0; i < 100; i++) director.Tick(0.25);

            AssertSameSnapshot(frozen, director.Capture(),
                "a frozen transport holds; there is no swallowed remainder waiting to be released on resume");

            director.SetPaused(false);
            director.Tick(0.25);
            Assert.That(director.PlayTime, Is.GreaterThan((float)frozen.PlayTime),
                "resuming carries on from the frozen instant rather than restarting the phase");
            Assert.AreEqual(frozen.PlayIndex, director.PlayIndex);
        }

        [Test]
        public void TogglePaused_FlipsTheFreezeBothWays()
        {
            var director = Director(Drive());

            director.TogglePaused();
            Assert.IsTrue(director.Paused, "one wand button has to be able to do both halves of this");

            director.TogglePaused();
            Assert.IsFalse(director.Paused);
        }

        [Test]
        public void TimeScale_OfAHalfAdvancesTheTransportHalfAsFast()
        {
            var slow = Director(Drive());
            slow.TimeScale = 0.5f;
            slow.SeekToPlay(0, 0f);

            var full = Director(Drive());
            full.SeekToPlay(0, 0f);

            // One second of wall clock, fed identically to both.
            for (int i = 0; i < 4; i++) { slow.Tick(0.25); full.Tick(0.25); }

            Assert.AreEqual(0.5f, slow.PlayTime, 1e-3f, "half a second of play time per second of wall clock");
            Assert.AreEqual(1f, full.PlayTime, 1e-3f);

            // On to two and a half seconds of wall clock in total. The full-rate transport is past a
            // two-second play; the slow one is still running it, which is the whole point of slow motion.
            for (int i = 0; i < 6; i++) { slow.Tick(0.25); full.Tick(0.25); }

            Assert.AreEqual(PlayPhase.PostPlay, full.Phase);
            Assert.AreEqual(PlayPhase.Live, slow.Phase);
            Assert.AreEqual(2.5f * 0.5f, slow.PlayTime, 1e-3f, "2.5 s of wall clock at half rate");
        }

        [Test]
        public void TimeScale_OfZeroOrBelowStopsTheClockRatherThanRunningItBackwards()
        {
            var director = Director(Drive());
            director.SeekToPlay(1, PlayDuration * 0.5f);
            var held = director.Capture();

            director.TimeScale = 0f;
            for (int i = 0; i < 20; i++) director.Tick(0.25);
            AssertSameSnapshot(held, director.Capture(), "zero rate is a freeze");

            // A negative rate is a bad value, not a rewind request. Winding the phase clock backwards would
            // walk the transport into phases it has already left, which no consumer is written to survive.
            director.TimeScale = -2f;
            for (int i = 0; i < 20; i++) director.Tick(0.25);
            AssertSameSnapshot(held, director.Capture(), "a negative rate clamps to a freeze");

            director.TimeScale = 1f;
            director.Tick(-1.0);
            AssertSameSnapshot(held, director.Capture(), "a negative dt is ignored for the same reason");
        }

        [Test]
        public void SeekToPlay_SetsTheStateDirectly()
        {
            var director = Director(Drive());

            director.SeekToPlay(1);
            Assert.AreEqual(1, director.PlayIndex);
            Assert.AreEqual(0, director.PreviousPlayIndex, "the blend walks in from the play before the one jumped to");
            Assert.AreEqual(PlayPhase.Huddle, director.Phase, "no time given means start it in the huddle");
            Assert.AreEqual(0f, director.PlayTime, 1e-3f);
            Assert.AreEqual(0f, director.PhaseTime, 1e-3f);

            director.SeekToPlay(2, PlayDuration * 0.5f);
            Assert.AreEqual(2, director.PlayIndex);
            Assert.AreEqual(PlayPhase.Live, director.Phase, "a time given means the play is already running at it");
            Assert.AreEqual(PlayDuration * 0.5f, director.PlayTime, 1e-3f);
            Assert.AreEqual(0f, director.PhaseTime, 1e-3f);

            director.SeekToPlay(0, PlayDuration * 10f);
            Assert.AreEqual(PlayDuration, director.PlayTime, 1e-3f, "past the end of a play clamps to the whistle");
            Assert.AreEqual(-1, director.PreviousPlayIndex, "play 0 has nothing before it to walk from");
        }

        [Test]
        public void SeekToPlay_WrapsAnOutOfRangeIndexIntoTheDrive()
        {
            var director = Director(Drive());

            director.SeekToPlay(PlayCount);
            Assert.AreEqual(0, director.PlayIndex, "one past the end is the top of the drive again");

            director.SeekToPlay(PlayCount + 1);
            Assert.AreEqual(1, director.PlayIndex);

            director.SeekToPlay(-1);
            Assert.AreEqual(PlayCount - 1, director.PlayIndex,
                "stepping back off the front lands on the last play — an operator holding Previous must not fall off");
        }

        [Test]
        public void SeekToPlay_LeavesNoTraceOfHowTheDriveGotThere()
        {
            var untouched = Director(Drive());
            var wellUsed = Director(Drive());
            for (int i = 0; i < 37; i++) wellUsed.Tick(0.1);   // a drive that has been running a while

            untouched.SeekToPlay(2, 0.75f);
            wellUsed.SeekToPlay(2, 0.75f);

            AssertSameSnapshot(untouched.Capture(), wellUsed.Capture(),
                "a jump assigns the state outright, so no accumulation order survives it");

            RunSchedule(untouched);
            RunSchedule(wellUsed);
            AssertSameSnapshot(untouched.Capture(), wellUsed.Capture(),
                "and the two run on identically from there, which is what makes a seek reproducible");
        }

        [Test]
        public void Reset_ReturnsToTheTopOfTheDrive()
        {
            var director = Director(Drive());
            for (int i = 0; i < 60; i++) director.Tick(0.25);
            director.TimeScale = 0.25f;

            director.Reset();

            AssertSameSnapshot(Director(Drive()).Capture(), director.Capture(),
                "a reset drive is indistinguishable from a fresh one");
            Assert.AreEqual(0.25f, director.TimeScale, 1e-3f,
                "the rate is an operator preference, not drive state — a reset must not quietly undo slow motion");
        }
    }
}
