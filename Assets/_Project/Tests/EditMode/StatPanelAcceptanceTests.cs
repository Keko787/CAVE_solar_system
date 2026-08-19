using System.Collections.Generic;
using NUnit.Framework;
using Stadium3D.EditorTools;
using Stadium3D.Pitch;
using UnityEngine;
using E = Stadium3D.Football.ScriptedDrive.Expected;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// M5's acceptance check: what the walls actually say, against the drive's authored expectation.
    /// </summary>
    /// <remarks>
    /// <b>M1 already proves the ledger is right, and that is not the same claim.</b>
    /// <c>ScriptedDriveTests</c> asserts the fold produces 2-of-3 for 76 yards and a touchdown; this asserts
    /// that a viewer in the room <em>reads</em> those numbers. Everything between the two is a place the
    /// value can survive intact and still arrive wrong: the wrong stat chosen for a position, a label wired
    /// to the neighbouring field, a rounding that turns 76 into 75, a card filled for the wrong player. Each
    /// of those leaves the box score perfect and the wall wrong, and none of them look like a defect —
    /// which is this project's whole failure pattern.
    ///
    /// <para>So these tests read the strings back off the built labels rather than calling the formatter,
    /// and compare them against <see cref="ScriptedDrive.Expected"/> — the answer that was authored beside
    /// the drive, not the answer the code happens to produce.</para>
    /// </remarks>
    public class StatPanelAcceptanceTests
    {
        readonly List<GameObject> m_Spawned = new List<GameObject>();
        FootballSim m_Sim;

        [SetUp]
        public void SetUp() => m_Sim = new FootballSim(ScriptedDrive.Build());

        [TearDown]
        public void TearDown()
        {
            foreach (var go in m_Spawned)
                if (go != null) Object.DestroyImmediate(go);

            m_Spawned.Clear();
        }

        T Camera<T>(string name) where T : PitchCameraBase
        {
            var go = new GameObject(name);
            m_Spawned.Add(go);
            go.AddComponent<Camera>();
            return go.AddComponent<T>();
        }

        static string TextOf(StatHud hud, string label)
        {
            foreach (var element in hud.Elements)
                if (element.Label != null && element.Label.name == label) return element.Label.text;

            Assert.Fail($"the readout has no label called \"{label}\"");
            return null;
        }

        /// <summary>Run the whole drive out, so the box score is the finished one.</summary>
        void PlayToTheEnd()
        {
            m_Sim.Director.SeekToPlay(E.Plays - 1, 0f);

            // Past the end of the last play and into PostPlay, which is when the ledger counts it.
            for (int i = 0; i < 600; i++) m_Sim.Tick(1.0 / 60.0);
        }

        /// <summary>
        /// The one the milestone is defined by: the passer's card carries M1's authored line.
        /// </summary>
        [Test]
        public void ThePassersCardShowsTheAuthoredPasserLine()
        {
            PlayToTheEnd();

            var wall = Camera<TeamIsoPitchCamera>("HomeIsoCamera");
            StatPanelBuilder.Build(null, wall, null);

            var hud = wall.GetComponentInChildren<TeamWallStatHud>(true);
            int vance = PlayerIdentity.PlayerKey(TeamSide.Home, E.PasserNumber);

            hud.Apply(m_Sim, vance);

            string stats = TextOf(hud, "Stats");

            Assert.That(stats, Does.Contain($"{E.PassCompletions}/{E.PassAttempts}"),
                "a sack is not a pass attempt, and the wall has to agree with the ledger about that");
            Assert.That(stats, Does.Contain($"{E.PassingYards:0} yds"));
            Assert.That(stats, Does.Contain($"{E.PassingTouchdowns} TD"));

            Assert.That(TextOf(hud, "Name"), Does.Contain($"#{E.PasserNumber}"));
        }

        [Test]
        public void ThePassersCardIsAboutThePasserAndNotWhoeverIsNearby()
        {
            PlayToTheEnd();

            var wall = Camera<TeamIsoPitchCamera>("HomeIsoCamera");
            StatPanelBuilder.Build(null, wall, null);

            var hud = wall.GetComponentInChildren<TeamWallStatHud>(true);
            var roster = m_Sim.Roster;
            int vance = PlayerIdentity.PlayerKey(TeamSide.Home, E.PasserNumber);

            hud.Apply(m_Sim, vance);
            string forVance = TextOf(hud, "Stats");

            // Somebody else on the same side, who did not throw the ball.
            int other = 0;
            foreach (var identity in roster.All)
            {
                if (identity.Side != TeamSide.Home || identity.Id == vance) continue;
                if (identity.Position == PlayerPosition.Quarterback) continue;
                other = identity.Id;
                break;
            }

            Assert.That(other, Is.Not.Zero);
            hud.Apply(m_Sim, other);

            Assert.That(TextOf(hud, "Stats"), Is.Not.EqualTo(forVance),
                "a card that shows the same line whoever it is asked about is not reading the player id");
            Assert.That(TextOf(hud, "Stats"), Does.Not.Contain("rtg"),
                "and nobody but the passer has a passer rating");
        }

        /// <summary>
        /// The front wall walks the authored down-and-distance sequence, snap by snap.
        /// </summary>
        /// <remarks>
        /// The same sequence M1 asserts against the ledger, read here off the label instead. It is the
        /// single most load-bearing string in the room — §8 gives it the largest type in the readable band
        /// precisely because it is the thing a viewer checks continuously.
        /// </remarks>
        [Test]
        public void TheFrontWallWalksTheAuthoredDownAndDistanceSequence()
        {
            var front = Camera<BroadcastPitchCamera>("BroadcastCamera");
            StatPanelBuilder.Build(front, null, null);

            var hud = front.GetComponentInChildren<BroadcastStatHud>(true);

            for (int play = 0; play < E.Sequence.Length; play++)
            {
                m_Sim.Director.SeekToPlay(play, 0f);
                m_Sim.Tick(0.0);

                hud.Apply(m_Sim);

                var expected = E.Sequence[play];
                string shown = TextOf(hud, "DownDistance");

                Assert.That(shown, Is.EqualTo(m_Sim.Situation.DownAndDistance()),
                    $"play {play}: the wall has to say what the situation says");

                Assert.That(m_Sim.Situation.Down, Is.EqualTo(expected.Down), $"play {play} down");
                Assert.That(m_Sim.Situation.DistanceYards, Is.EqualTo(expected.Distance).Within(0.01f),
                    $"play {play} distance");
            }
        }

        [Test]
        public void TheFrontWallShowsTheScoreTheDriveEndsOn()
        {
            PlayToTheEnd();

            var front = Camera<BroadcastPitchCamera>("BroadcastCamera");
            StatPanelBuilder.Build(front, null, null);

            var hud = front.GetComponentInChildren<BroadcastStatHud>(true);
            hud.Apply(m_Sim);

            string score = TextOf(hud, "Score");

            Assert.That(score, Does.Contain($"{m_Sim.Roster.HomeAbbreviation} {E.HomeScoreAfter}"));
            Assert.That(score, Does.Contain($"{m_Sim.Roster.AwayAbbreviation} {E.AwayScoreAfter}"));
        }

        /// <summary>
        /// Possession is a mark on the score, so it has to be against the right team.
        /// </summary>
        [Test]
        public void ThePossessionMarkSitsAgainstWhoeverHasTheBall()
        {
            m_Sim.Director.SeekToPlay(0, 0f);
            m_Sim.Tick(0.0);

            var front = Camera<BroadcastPitchCamera>("BroadcastCamera");
            StatPanelBuilder.Build(front, null, null);

            var hud = front.GetComponentInChildren<BroadcastStatHud>(true);
            hud.Apply(m_Sim);

            string score = TextOf(hud, "Score");
            var roster = m_Sim.Roster;

            string withBall = m_Sim.Situation.Possession == TeamSide.Home
                ? roster.HomeAbbreviation
                : roster.AwayAbbreviation;

            int mark = score.IndexOf('•');
            Assert.That(mark, Is.GreaterThanOrEqualTo(0), "the score carries a possession mark");
            Assert.That(score.IndexOf(withBall), Is.GreaterThan(mark),
                "and the mark comes immediately before the team that has the ball, not the other one");
        }

        /// <summary>
        /// The common case: most of the field has done nothing, and their cards should say so quietly.
        /// </summary>
        [Test]
        public void ALinemansCardCarriesHisIdentityAndNoStatLine()
        {
            PlayToTheEnd();

            var wall = Camera<TeamIsoPitchCamera>("HomeIsoCamera");
            StatPanelBuilder.Build(null, wall, null);

            var hud = wall.GetComponentInChildren<TeamWallStatHud>(true);

            int lineman = 0;
            foreach (var identity in m_Sim.Roster.All)
            {
                if (identity.Side != TeamSide.Home || identity.Position != PlayerPosition.OffensiveLine) continue;
                lineman = identity.Id;
                break;
            }

            Assert.That(lineman, Is.Not.Zero, "the drive fields an offensive line");
            hud.Apply(m_Sim, lineman);

            Assert.That(TextOf(hud, "Name"), Is.Not.Empty, "he is still somebody");
            Assert.That(TextOf(hud, "Vitals"), Is.Not.Empty);
            Assert.That(TextOf(hud, "Stats"), Is.Empty,
                "and a gap under the vitals reads as uninvolved, where a row of zeroes reads as a bad game");
        }
    }
}
