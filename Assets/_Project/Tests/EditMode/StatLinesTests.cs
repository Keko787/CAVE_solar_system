using NUnit.Framework;
using Stadium3D.Football;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// The stat line a team wall shows for the player it is following.
    /// </summary>
    /// <remarks>
    /// Worth testing because the choice of <em>which</em> stats to show is a decision rather than a
    /// transcription, and because the common case on a football field is a player who has not touched the
    /// ball — eleven of the twenty-two on any snap, and every one of them a chance to put a row of zeroes
    /// on a wall.
    /// </remarks>
    public class StatLinesTests
    {
        static PlayerIdentity Player(PlayerPosition position, int number = 9) => new PlayerIdentity
        {
            Number = number,
            Name = "Ellis",
            Position = position,
            Side = TeamSide.Home,
            HeightInches = 74,
            WeightPounds = 215,
        };

        [Test]
        public void AQuarterbackGetsCompletionsAndARating()
        {
            var stats = new PlayerStats
            {
                PassAttempts = 18, PassCompletions = 12, PassingYards = 214f, PassingTouchdowns = 2,
            };

            string line = StatLines.ForPosition(Player(PlayerPosition.Quarterback), stats);

            Assert.That(line, Does.Contain("12/18"));
            Assert.That(line, Does.Contain("214 yds"));
            Assert.That(line, Does.Contain("2 TD"));
            Assert.That(line, Does.Contain("rtg"));
            Assert.That(line, Does.Not.Contain("INT"), "a clean sheet is shown by the absence of the field");
        }

        [Test]
        public void AReceiverGetsCatchesTargetsAndALong()
        {
            var stats = new PlayerStats
            {
                Targets = 7, Receptions = 5, ReceivingYards = 96f, ReceivingTouchdowns = 1, LongestReception = 41f,
            };

            string line = StatLines.ForPosition(Player(PlayerPosition.WideReceiver), stats);

            Assert.That(line, Does.Contain("5/7 rec"));
            Assert.That(line, Does.Contain("96 yds"));
            Assert.That(line, Does.Contain("long 41"));
        }

        /// <summary>
        /// Position leads, but it does not overrule the stats that exist.
        /// </summary>
        [Test]
        public void ABackWhoHasOnlyCaughtPassesGetsTheReceivingLine()
        {
            var stats = new PlayerStats { Targets = 4, Receptions = 4, ReceivingYards = 38f };

            string line = StatLines.ForPosition(Player(PlayerPosition.RunningBack), stats);

            Assert.That(line, Does.Contain("4/4 rec"));
            Assert.That(line, Does.Not.Contain("car"),
                "showing him 0 carries while four catches sit in the same struct is the worse answer");
        }

        [Test]
        public void APlayerWhoHasNotTouchedTheBallGetsADashRatherThanZeroes()
        {
            foreach (var position in new[] { PlayerPosition.Quarterback, PlayerPosition.WideReceiver,
                                             PlayerPosition.Cornerback, PlayerPosition.OffensiveLine })
            {
                string line = StatLines.ForPosition(Player(position), default);

                Assert.That(line, Is.EqualTo(StatLines.Empty),
                    $"{position} with an empty line should read as uninvolved, not as a bad game");
            }
        }

        [Test]
        public void ADefenderWithASackGetsTheSackRatherThanADash()
        {
            var one = StatLines.ForPosition(Player(PlayerPosition.DefensiveLine), new PlayerStats { Sacks = 1 });
            var two = StatLines.ForPosition(Player(PlayerPosition.Linebacker), new PlayerStats { Sacks = 2 });

            Assert.That(one, Is.EqualTo("1 sack"));
            Assert.That(two, Is.EqualTo("2 sacks"));
        }

        [Test]
        public void TheCardCarriesNumberNameAndVitals()
        {
            var identity = Player(PlayerPosition.WideReceiver, number: 85);

            Assert.That(StatLines.Card(identity), Does.Contain("#85"));
            Assert.That(StatLines.Card(identity), Does.Contain("ELLIS"));

            string vitals = StatLines.Vitals(identity);
            Assert.That(vitals, Does.Contain("6-2"), "74 inches is six foot two");
            Assert.That(vitals, Does.Contain("215 lb"));
        }

        [Test]
        public void SeparationIsOmittedRatherThanShownAsNegativeWhenThereIsNobodyToMeasureAgainst()
        {
            var alone = new LiveTelemetry(9, 14.2f, 17.6f, 22f, -1f, 0, "Go");
            var covered = new LiveTelemetry(9, 14.2f, 17.6f, 22f, 3.4f, 24, "Go");

            Assert.That(StatLines.Live(alone), Does.Not.Contain("sep"));
            Assert.That(StatLines.Live(alone), Does.Not.Contain("-"));
            Assert.That(StatLines.Live(covered), Does.Contain("sep 3"));
        }

        [Test]
        public void TheLiveLineLeadsWithSpeed()
        {
            string line = StatLines.Live(new LiveTelemetry(9, 14.2f, 17.6f, 22f, 3.4f, 24, "Go"));

            Assert.That(line, Does.StartWith("14.2 mph"));
            Assert.That(line, Does.Contain("top 17.6"));
            Assert.That(line, Does.Contain("22 yds"));
        }
    }
}
