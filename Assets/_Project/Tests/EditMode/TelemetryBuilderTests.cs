using Stadium3D.Football;
using NUnit.Framework;
using UnityEngine;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// The live readout's unit boundary, and the one scan inside it that is easy to get wrong.
    ///
    /// Two things are pinned here. First, <see cref="LiveTelemetry"/> hands a panel finished numbers — mph
    /// and yards — so no view ever carries its own ratio. That is exactly the shape of the DroneFeedWindow
    /// stretch the source project shipped, and it is nasty because a metre value leaking into a yard field
    /// reads about 9% short: a plausible number for a route rather than an obviously broken one, so nobody
    /// catches it in the CAVE.
    ///
    /// Second, separation measures the nearest <b>opponent</b>, not the nearest body. A plain
    /// nearest-neighbour scan reports the guard standing beside a receiver instead of the corner covering
    /// him — wrong on every snap, and right-looking on most of them, since the two are often the same body.
    /// The fixture below deliberately makes a teammate strictly closer so that mistake cannot pass.
    /// </summary>
    public class TelemetryBuilderTests
    {
        // Ids come from the roster's own (side, number) fold rather than hand-picked integers, so no test
        // body can accidentally take id 0 — which the builder reserves to mean "no opponent to measure".
        static readonly int k_Receiver = PlayerIdentity.PlayerKey(TeamSide.Home, 12);
        static readonly int k_Guard    = PlayerIdentity.PlayerKey(TeamSide.Home, 66);
        static readonly int k_Corner   = PlayerIdentity.PlayerKey(TeamSide.Away, 24);
        static readonly int k_Safety   = PlayerIdentity.PlayerKey(TeamSide.Away, 31);

        /// <summary>A body at rest unless the test says otherwise. Rotation and possession never reach telemetry.</summary>
        static PlayerFrame Frame(int id, TeamSide side, Vector3 position,
                                 float speedMps = 0f, float peakSpeedMps = 0f, float distanceMeters = 0f) =>
            new PlayerFrame(id, side, position, Quaternion.identity, speedMps, peakSpeedMps, distanceMeters, false);

        /// <summary>Where the focused receiver stands below: own 35, on the centre of the field.</summary>
        static Vector3 LineOfScrimmage => FieldGeometry.Spot(35f, 0f);

        /// <summary>A spot <paramref name="yards"/> across the field from <paramref name="from"/>, in metres.</summary>
        static Vector3 AcrossField(Vector3 from, float yards) =>
            from + new Vector3(0f, 0f, FieldGeometry.YardsToMeters(yards));

        [Test]
        public void SpeedMph_TenMetresPerSecond_ReadsTwentyTwoPointThreeSeven()
        {
            var frames = new[]
            {
                Frame(k_Receiver, TeamSide.Home, LineOfScrimmage, speedMps: 10f, peakSpeedMps: 12f)
            };

            var readout = TelemetryBuilder.Build(k_Receiver, frames, "Go");

            // 10 m/s is what a viewer reads as 22.37 on the wall. Held to 0.01 because a panel that rounds to
            // one decimal must never disagree with the value sitting behind it.
            Assert.AreEqual(22.37f, readout.SpeedMph, 0.01f);
            Assert.AreEqual(12f * FieldGeometry.MphPerMps, readout.TopSpeedMph, 1e-2f,
                "top speed takes the same conversion — two ratios is how the two numbers drift apart");
            Assert.AreEqual(k_Receiver, readout.PlayerId);
            Assert.AreEqual("Go", readout.RouteName);
        }

        [Test]
        public void DistanceYards_IsTheYardValue_NotTheMetresTheSimMeasuredIn()
        {
            float meters = FieldGeometry.YardsToMeters(43.5f);
            var frames = new[]
            {
                Frame(k_Receiver, TeamSide.Home, LineOfScrimmage, distanceMeters: meters)
            };

            var readout = TelemetryBuilder.Build(k_Receiver, frames, "Post");

            Assert.AreEqual(43.5f, readout.DistanceYards, 1e-3f);
            Assert.That(readout.DistanceYards, Is.GreaterThan(meters + 1f),
                "yards must read larger than the metres they came from — a raw metre value reaching the panel is the bug");
        }

        [Test]
        public void Separation_WithATeammateStrictlyCloser_StillMeasuresToTheOpponent()
        {
            var self = LineOfScrimmage;
            var frames = new[]
            {
                Frame(k_Receiver, TeamSide.Home, self),
                Frame(k_Guard,    TeamSide.Home, AcrossField(self, 1f)),   // strictly the nearest body on the field
                Frame(k_Corner,   TeamSide.Away, AcrossField(self, 6f)),   // the man actually covering him
                Frame(k_Safety,   TeamSide.Away, AcrossField(self, 14f)),  // a further opponent, to prove the scan sorts
            };

            var readout = TelemetryBuilder.Build(k_Receiver, frames, "Slant");

            Assert.AreEqual(k_Corner, readout.NearestOpponentId,
                "the nearest body here is a teammate; the readout has to name the cover man");
            Assert.AreEqual(6f, readout.SeparationYards, 1e-3f);
            Assert.That(readout.SeparationYards, Is.GreaterThan(2f),
                "a naive nearest-body scan would report the guard at about a yard");
        }

        [Test]
        public void SeparationYards_IsInYards_NotTheMetresBetweenTheTwoBodies()
        {
            var self = LineOfScrimmage;
            float meters = FieldGeometry.YardsToMeters(10f);
            var frames = new[]
            {
                Frame(k_Receiver, TeamSide.Home, self),
                Frame(k_Corner,   TeamSide.Away, self + new Vector3(0f, 0f, meters)),
            };

            var readout = TelemetryBuilder.Build(k_Receiver, frames, "Comeback");

            Assert.AreEqual(10f, readout.SeparationYards, 1e-3f);
            Assert.That(readout.SeparationYards, Is.GreaterThan(meters),
                "ten yards is 9.144 m — the number on the wall is the larger of the two, every time");
        }

        [Test]
        public void Separation_ReadsTheSameFromEitherSideOfThePair()
        {
            var self = LineOfScrimmage;
            // Three yards downfield and four across: a 3-4-5 triangle, so the gap is exactly five yards
            // without hand-computing a square root that would only pin this one arrangement.
            var cover = self + new Vector3(FieldGeometry.YardsToMeters(3f), 0f, FieldGeometry.YardsToMeters(4f));
            var frames = new[]
            {
                Frame(k_Receiver, TeamSide.Home, self),
                Frame(k_Corner,   TeamSide.Away, cover),
            };

            var fromReceiver = TelemetryBuilder.Build(k_Receiver, frames, "Fade");
            var fromCorner   = TelemetryBuilder.Build(k_Corner, frames, "Man");

            Assert.AreEqual(5f, fromReceiver.SeparationYards, 1e-3f);
            Assert.AreEqual(fromReceiver.SeparationYards, fromCorner.SeparationYards, 1e-3f,
                "the two team walls are describing one gap and must not disagree about how wide it is");
            Assert.AreEqual(k_Corner, fromReceiver.NearestOpponentId);
            Assert.AreEqual(k_Receiver, fromCorner.NearestOpponentId,
                "opponent is relative to whoever is focused, not to a fixed side");
        }

        [Test]
        public void Build_UnknownPlayerId_ReturnsAZeroedReadoutRatherThanThrowing()
        {
            var frames = new[]
            {
                Frame(k_Receiver, TeamSide.Home, LineOfScrimmage, speedMps: 8f, peakSpeedMps: 9f, distanceMeters: 20f),
                Frame(k_Corner,   TeamSide.Away, AcrossField(LineOfScrimmage, 4f)),
            };

            // A wall can be focused on a player who is not in the current personnel package — a substitution,
            // or a play script that names someone the formation left off. That is a blank card, not a crash
            // in the middle of a demo.
            Assert.DoesNotThrow(() => TelemetryBuilder.Build(4242, frames, "Curl"));
            var readout = TelemetryBuilder.Build(4242, frames, "Curl");

            Assert.AreEqual(4242, readout.PlayerId, "the readout still says who it was asked about");
            Assert.AreEqual(0f, readout.SpeedMph);
            Assert.AreEqual(0f, readout.TopSpeedMph);
            Assert.AreEqual(0f, readout.DistanceYards);
            Assert.AreEqual(0, readout.NearestOpponentId);
            Assert.That(readout.SeparationYards, Is.LessThan(0f),
                "negative is the 'nothing to measure' sentinel; a panel must be able to tell it from a real zero-yard gap");
            Assert.AreEqual("Curl", readout.RouteName,
                "the assignment label comes from the play script, not from the missing frame");
        }

        [Test]
        public void Separation_WithNoOpponentsOnTheField_IsNegativeAndNamesNobody()
        {
            var self = LineOfScrimmage;
            var frames = new[]
            {
                Frame(k_Receiver, TeamSide.Home, self, speedMps: 6f, peakSpeedMps: 6f, distanceMeters: 10f),
                Frame(k_Guard,    TeamSide.Home, AcrossField(self, 2f)),
            };

            var readout = TelemetryBuilder.Build(k_Receiver, frames, "Screen");

            Assert.AreEqual(0, readout.NearestOpponentId, "zero is the id for nobody");
            Assert.That(readout.SeparationYards, Is.LessThan(0f),
                "zero yards would read as a defender standing on him — an absent measurement has to be a different number");

            // The rest of the card stays live: an unmeasurable gap must not blank the numbers around it.
            Assert.AreEqual(6f * FieldGeometry.MphPerMps, readout.SpeedMph, 1e-2f);
            Assert.AreEqual(FieldGeometry.MetersToYards(10f), readout.DistanceYards, 1e-3f);
        }
    }
}
