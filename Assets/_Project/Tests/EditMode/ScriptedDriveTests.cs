using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Stadium3D.Football;
using E = Stadium3D.Football.ScriptedDrive.Expected;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// The acceptance test for the whole sim: the <b>shipped</b> drive, not a fixture built to pass.
    /// </summary>
    /// <remarks>
    /// Every other fixture proves a component behaves. This one proves the content and the components agree —
    /// that the six authored plays walk the intended down-and-distance sequence, land on the intended
    /// scoreboard, and produce the box score that was written down <em>before</em> the plays were authored
    /// (<see cref="ScriptedDrive.Expected"/>). That ordering is the point: an expectation derived from
    /// whatever the code currently prints can only ever agree with itself.
    ///
    /// <para>It also carries the rewind-equality checks. Those are the tests that would fail loudly against a
    /// per-frame integrator and pass by construction here, so they are the ones that actually defend the
    /// design decision the whole sim rests on.</para>
    /// </remarks>
    public class ScriptedDriveTests
    {
        DriveScript m_Drive;
        FootballSim m_Sim;

        [SetUp]
        public void SetUp()
        {
            m_Drive = ScriptedDrive.Build();
            m_Sim = new FootballSim(m_Drive);
        }

        // ── the content is sound ───────────────────────────────────────────────────────────────────

        [Test]
        public void ShippedDrive_ValidatesWithNoErrors()
        {
            var baked = new List<BakedPlay>();
            for (int i = 0; i < m_Drive.Plays.Count; i++)
                baked.Add(BakedPlay.Bake(m_Drive.Plays[i], m_Drive.Roster, m_Drive.SampleHz));

            var errors = PlayValidator.ValidateDrive(m_Drive, baked);

            // Printing every error beats "expected 0 but was 7" when a play is edited.
            Assert.That(errors, Is.Empty, "the shipped drive should validate clean:\n  " + string.Join("\n  ", errors));
        }

        [Test]
        public void ShippedDrive_HasTheAuthoredShape()
        {
            Assert.That(m_Drive.Plays.Count, Is.EqualTo(ScriptedDrive.Expected.Plays));
            Assert.That(m_Drive.Roster.All.Count, Is.EqualTo(22), "eleven a side");

            int penalties = 0, incompletions = 0, touchdowns = 0, sacks = 0;
            foreach (var play in m_Drive.Plays)
            {
                if (play.Outcome.HasPenalty) penalties++;
                if (play.Outcome.Kind == PlayResultKind.PassIncomplete) incompletions++;
                if (play.Outcome.Kind == PlayResultKind.Touchdown) touchdowns++;
                if (play.Outcome.Kind == PlayResultKind.Sack) sacks++;
            }

            Assert.That(penalties, Is.EqualTo(1), "exactly one flag");
            Assert.That(incompletions, Is.EqualTo(1), "exactly one incompletion");
            Assert.That(sacks, Is.EqualTo(1), "exactly one sack");
            Assert.That(touchdowns, Is.EqualTo(1), "the drive ends in a touchdown");
            Assert.That(m_Drive.Plays[m_Drive.Plays.Count - 1].Outcome.Kind, Is.EqualTo(PlayResultKind.Touchdown),
                "and the touchdown is the last play");
        }

        [Test]
        public void EveryPlayer_IsOnEveryPlay()
        {
            // A body that vanishes for one snap and reappears at the next is the most obvious possible
            // artefact on the floor view, and the cheapest to catch here.
            foreach (var play in m_Drive.Plays)
                foreach (var identity in m_Drive.Roster.All)
                    Assert.That(play.ScriptFor(identity.Id), Is.Not.Null,
                        $"#{identity.Number} ({identity.Side}) has no assignment on \"{play.Name}\"");
        }

        // ── the fold lands where the football says it should ───────────────────────────────────────

        [Test]
        public void Ledger_WalksTheAuthoredDownAndDistanceSequence()
        {
            var ledger = new DriveLedger(m_Drive);
            var expected = ScriptedDrive.Expected.Sequence;

            for (int i = 0; i < expected.Length; i++)
            {
                var s = ledger.StateAtSnap(i);
                Assert.That(s.Down, Is.EqualTo(expected[i].Down), $"down at snap {i}");
                Assert.That(s.DistanceYards, Is.EqualTo(expected[i].Distance).Within(0.01f), $"distance at snap {i}");
                Assert.That(s.LineOfScrimmageYard, Is.EqualTo(expected[i].Yard).Within(0.01f), $"ball spot at snap {i}");
            }
        }

        [Test]
        public void EveryPlay_IsAuthoredAtTheSpotTheFoldPutsTheBall()
        {
            // The scoreboard and the field have to agree. If they drift, the demo reads as broken rather
            // than as a content typo, which is why PlayValidator checks it too — this pins it directly.
            var ledger = new DriveLedger(m_Drive);
            for (int i = 0; i < m_Drive.Plays.Count; i++)
                Assert.That(m_Drive.Plays[i].LineOfScrimmageYard,
                    Is.EqualTo(ledger.StateAtSnap(i).LineOfScrimmageYard).Within(0.01f),
                    $"play {i} (\"{m_Drive.Plays[i].Name}\")");
        }

        [Test]
        public void Ledger_ProducesTheExpectedBoxScore()
        {
            var ledger = new DriveLedger(m_Drive);
            var box = ledger.ScoreAfter(m_Drive.Plays.Count);

            Assert.That(box.Home.TotalYards, Is.EqualTo(E.TeamTotalYards).Within(0.01f), "team total yards");
            Assert.That(box.Home.PassingYards, Is.EqualTo(E.TeamPassingYards).Within(0.01f), "team passing yards");
            Assert.That(box.Home.RushingYards, Is.EqualTo(E.TeamRushingYards).Within(0.01f), "team rushing yards");
            Assert.That(box.Home.FirstDowns, Is.EqualTo(E.HomeFirstDowns), "first downs");
            Assert.That(box.Home.Penalties, Is.EqualTo(E.HomePenalties), "penalties");
            Assert.That(box.Home.PenaltyYards, Is.EqualTo(E.HomePenaltyYards).Within(0.01f), "penalty yards");
            Assert.That(box.Home.ThirdDownAttempts, Is.EqualTo(E.ThirdDownAttempts), "third-down attempts");
            Assert.That(box.Home.ThirdDownConversions, Is.EqualTo(E.ThirdDownConversions), "third-down conversions");
            Assert.That(box.Home.Points, Is.EqualTo(E.HomeScoreAfter), "home score");
            Assert.That(box.Away.Points, Is.EqualTo(E.AwayScoreAfter), "away score");
        }

        [Test]
        public void Ledger_ProducesTheExpectedPasserLine()
        {
            var ledger = new DriveLedger(m_Drive);
            var box = ledger.ScoreAfter(m_Drive.Plays.Count);
            var vance = box.For(PlayerIdentity.PlayerKey(TeamSide.Home, E.PasserNumber));

            Assert.That(vance.PassAttempts, Is.EqualTo(E.PassAttempts), "a sack is not a pass attempt");
            Assert.That(vance.PassCompletions, Is.EqualTo(E.PassCompletions));
            Assert.That(vance.PassingYards, Is.EqualTo(E.PassingYards).Within(0.01f));
            Assert.That(vance.PassingTouchdowns, Is.EqualTo(E.PassingTouchdowns));
            Assert.That(vance.PasserRating(), Is.GreaterThan(100f), "two completions and a touchdown on three attempts");
        }

        [Test]
        public void FieldPositionGained_IsSixtyFiveYards_WhichIsNotTheSameAsTotalYards()
        {
            // The two numbers legitimately differ: a penalty moves the ball without being charged as
            // offensive yardage. Pinning both stops someone "fixing" the gap.
            var ledger = new DriveLedger(m_Drive);
            float start = ledger.StateAtSnap(0).LineOfScrimmageYard;
            var box = ledger.ScoreAfter(m_Drive.Plays.Count);

            Assert.That(FieldGeometry.AwayGoalLineYard - start,
                Is.EqualTo(ScriptedDrive.Expected.FieldPositionYards).Within(0.01f));
            Assert.That(box.Home.TotalYards,
                Is.Not.EqualTo(ScriptedDrive.Expected.FieldPositionYards).Within(0.01f));
        }

        // ── the sim runs the content ───────────────────────────────────────────────────────────────

        [Test]
        public void Sim_TicksTheWholeDriveWithoutProducingNaN()
        {
            for (int i = 0; i < 4000; i++)
            {
                m_Sim.Tick(1.0 / 60.0);

                foreach (var f in m_Sim.Frames)
                {
                    Assert.That(float.IsNaN(f.Position.x) || float.IsNaN(f.Position.y) || float.IsNaN(f.Position.z),
                        Is.False, $"NaN position on #{f.PlayerId} at tick {i}");
                    Assert.That(float.IsNaN(f.SpeedMps), Is.False, $"NaN speed on #{f.PlayerId} at tick {i}");
                }

                Assert.That(float.IsNaN(m_Sim.Ball.Position.x), Is.False, $"NaN ball at tick {i}");
                Assert.That(m_Sim.Situation.Down, Is.InRange(1, 4), $"illegal down at tick {i}");
            }
        }

        [Test]
        public void Sim_KeepsEveryBodyInBoundsForTheWholeDrive()
        {
            // Generous: a body may run through the back of the end zone on a touchdown, so this only catches
            // an alignment or a route that leaves the world entirely.
            for (int i = 0; i < 4000; i++)
            {
                m_Sim.Tick(1.0 / 60.0);
                foreach (var f in m_Sim.Frames)
                {
                    float lateral = Mathf.Abs(FieldGeometry.LateralAt(f.Position));
                    Assert.That(lateral, Is.LessThan(FieldGeometry.HalfWidthYards + 12f),
                        $"#{f.PlayerId} is {lateral:0.0} yd off centre at tick {i}");
                }
            }
        }

        [Test]
        public void EveryPlay_BakesWithoutBlowingTheSampleBudget()
        {
            // 22 bodies x ~7 s x 60 Hz is about 9k samples a play. A designer authoring a 30 s play, or
            // dropping in forty of them, should fail a test rather than a frame.
            int total = 0;
            foreach (var play in m_Drive.Plays)
            {
                var baked = BakedPlay.Bake(play, m_Drive.Roster, m_Drive.SampleHz);
                Assert.That(baked.Duration, Is.InRange(2f, 15f), $"\"{play.Name}\" has an implausible duration");
                total += baked.TotalSampleCount;
            }

            Assert.That(total, Is.LessThan(200000), "the whole drive should bake to well under 200k samples");
        }

        // ── the headline: rewind equality ──────────────────────────────────────────────────────────

        [Test]
        public void SeekingToAPlay_MatchesPlayingForwardToIt_UnderADifferentTickSchedule()
        {
            // The property a per-frame integrator cannot have. One sim walks there at 60 Hz with a jittered
            // schedule; the other jumps straight there. They must agree exactly, because neither of them
            // accumulated anything to disagree about.
            const int targetPlay = 2;
            const float targetTime = 1.4f;

            var walked = new FootballSim(ScriptedDrive.Build());
            walked.Director.SeekToPlay(targetPlay, targetTime);
            walked.Tick(0.0);
            string a = Digest(walked);

            var jumped = new FootballSim(ScriptedDrive.Build());
            float[] jitter = { 0.004f, 0.031f, 0.016f, 0.049f, 0.008f };
            for (int i = 0; i < 500; i++) jumped.Tick(jitter[i % jitter.Length]);
            jumped.Director.SeekToPlay(targetPlay, targetTime);
            jumped.Tick(0.0);
            string b = Digest(jumped);

            Assert.That(b, Is.EqualTo(a), "a seek must not depend on how the sim got there");
        }

        [Test]
        public void ReplayingAPlay_ReproducesItExactly()
        {
            var sim = new FootballSim(ScriptedDrive.Build());
            var first = new List<string>();

            sim.Director.SeekToPlay(1, 0f);
            for (int i = 0; i < 120; i++) { sim.Tick(1.0 / 60.0); first.Add(Digest(sim)); }

            sim.Director.SeekToPlay(1, 0f);
            for (int i = 0; i < 120; i++)
            {
                sim.Tick(1.0 / 60.0);
                Assert.That(Digest(sim), Is.EqualTo(first[i]), $"replay diverged at frame {i}");
            }
        }

        [Test]
        public void LoopingTheDrive_ProducesTheSameSecondLapAsTheFirst()
        {
            // The loop seam is where an accumulating scoreboard would double-count. The ledger folds once at
            // bind time precisely so lap two is indistinguishable from lap one.
            var sim = new FootballSim(ScriptedDrive.Build());

            var lap1 = new List<string>();
            sim.Director.SeekToPlay(0, 0f);
            for (int i = 0; i < 60; i++) { sim.Tick(1.0 / 60.0); lap1.Add(Digest(sim)); }

            // Run out the rest of the drive and back round to the top.
            for (int i = 0; i < 6000 && sim.Director.PlayIndex != 0; i++) sim.Tick(1.0 / 60.0);
            for (int i = 0; i < 6000 && sim.Director.PlayIndex == 0; i++) sim.Tick(1.0 / 60.0);
            for (int i = 0; i < 60000 && sim.Director.PlayIndex != 0; i++) sim.Tick(1.0 / 60.0);

            sim.Director.SeekToPlay(0, 0f);
            for (int i = 0; i < 60; i++)
            {
                sim.Tick(1.0 / 60.0);
                Assert.That(Digest(sim), Is.EqualTo(lap1[i]), $"lap 2 diverged at frame {i}");
            }
        }

        [Test]
        public void TimeScale_ChangesPacingButNotTheFrameAtAGivenPlayTime()
        {
            // Slow motion has to be a pacing decision, not a different simulation — otherwise the hero
            // play's beat schedule would quietly change what the walls show.
            var fast = new FootballSim(ScriptedDrive.Build());
            fast.Director.SeekToPlay(5, 2.0f);
            fast.Tick(0.0);

            var slow = new FootballSim(ScriptedDrive.Build());
            slow.Director.TimeScale = 0.3f;
            slow.Director.SeekToPlay(5, 2.0f);
            slow.Tick(0.0);

            Assert.That(Digest(slow), Is.EqualTo(Digest(fast)));
        }

        /// <summary>
        /// Every player pose, the ball, and the situation, rounded to a stable precision. Comparing strings
        /// makes a failure readable — the first differing character points at the body that drifted.
        /// </summary>
        static string Digest(FootballSim sim)
        {
            var sb = new StringBuilder(1024);
            foreach (var f in sim.Frames)
                sb.Append(f.PlayerId).Append(':')
                  .Append(f.Position.x.ToString("F3")).Append(',')
                  .Append(f.Position.z.ToString("F3")).Append(',')
                  .Append(f.Rotation.eulerAngles.y.ToString("F2")).Append('|');

            sb.Append("ball:").Append(sim.Ball.Carriage).Append(',')
              .Append(sim.Ball.Position.x.ToString("F3")).Append(',')
              .Append(sim.Ball.Position.y.ToString("F3")).Append('|');

            var s = sim.Situation;
            sb.Append("sit:").Append(s.Down).Append(',')
              .Append(s.DistanceYards.ToString("F2")).Append(',')
              .Append(s.LineOfScrimmageYard.ToString("F2")).Append(',')
              .Append(s.HomeScore).Append('-').Append(s.AwayScore);

            return sb.ToString();
        }
    }
}
