using Stadium3D.Football;
using NUnit.Framework;
using UnityEngine;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// The unit boundary, pinned. <see cref="FieldGeometry"/> is the only place metres, yards and mph are
    /// allowed to meet, which makes it the only place a factor can be wrong — and a wrong factor here does
    /// not throw or look broken, it just moves every marker and every stat by a plausible amount. The
    /// <c>DroneFeedWindow</c> stretch bug in the source project was this exact failure and survived review
    /// because the result still looked like a field.
    ///
    /// <para>So these tests pin the conversions against real-world ground truth (0.9144 m to the yard,
    /// 45.72 m to the goal line, 22.37 mph at 10 m/s) rather than against the constants that produce them.
    /// Deriving the expectation from <c>MetersPerYard</c> would make the suite agree with any value that
    /// constant happened to hold, which is precisely the error being guarded against. Where a number is a
    /// consequence rather than a definition it is derived from the constants, as the house rule asks.</para>
    ///
    /// <para>The round-trip tolerances are 1e-5 and that is provably safe rather than hopeful: a
    /// multiply-then-divide by the same float carries at most 2u relative error, which is strictly less
    /// than two ULPs, so the result lands within one ULP of the input. One ULP below 128 is 7.63e-6.</para>
    /// </summary>
    public class FieldGeometryTests
    {
        // Every test point stays inside the end lines, so |value| < 128 and the one-ULP argument above holds.
        const float YardTolerance = 1e-5f;
        const float PositionTolerance = 1e-3f;

        /// <summary>One international foot, in metres. Used to step just across a boundary line.</summary>
        const float FootInMeters = 0.3048f;

        // ── unit conversion ────────────────────────────────────────────────────────────────────────

        [Test]
        public void YardsToMeters_UsesTheExactInternationalYard()
        {
            // The definition, not a consequence — asserted against the literal on purpose. Deriving this
            // from MetersPerYard would make the test agree with whatever that constant said.
            Assert.That(FieldGeometry.YardsToMeters(1f), Is.EqualTo(0.9144f).Within(1e-7f));
            Assert.That(FieldGeometry.MetersToYards(0.9144f), Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void YardsToMeters_AndBack_RoundTripsAcrossTheWholeFieldIncludingEndZones()
        {
            // Every yard from the back of one end zone to the back of the other. Stats are carried in yards
            // and positions in metres, so a value crosses this boundary and comes back on almost every frame.
            for (int yard = -10; yard <= 110; yard++)
            {
                float y = yard;
                Assert.That(FieldGeometry.MetersToYards(FieldGeometry.YardsToMeters(y)),
                    Is.EqualTo(y).Within(YardTolerance), $"yard {yard} did not survive the trip to metres");
            }
        }

        [Test]
        public void MetersToYards_AndBack_RoundTripsAcrossTheWholeFieldIncludingEndZones()
        {
            // The other direction, which is the one a telemetry readout takes.
            float backOfEndZone = FieldGeometry.YardsToMeters(
                FieldGeometry.AwayGoalLineYard + FieldGeometry.EndZoneDepthYards - FieldGeometry.MidfieldYard);

            for (int step = -60; step <= 60; step++)
            {
                float meters = backOfEndZone * (step / 60f);
                Assert.That(FieldGeometry.YardsToMeters(FieldGeometry.MetersToYards(meters)),
                    Is.EqualTo(meters).Within(YardTolerance), $"{meters} m did not survive the trip to yards");
            }
        }

        [Test]
        public void MpsToMph_PinsTheConversionConstant()
        {
            // 10 m/s is 22.369 mph. Pinned against the literal for the same reason as the yard: this is the
            // number the speed readout on the wall has to be right about.
            Assert.That(FieldGeometry.MpsToMph(10f), Is.EqualTo(22.37f).Within(0.01f));

            // Linear and sign-preserving, so a closing speed reads the way a viewer expects.
            Assert.That(FieldGeometry.MpsToMph(0f), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(FieldGeometry.MpsToMph(-10f), Is.EqualTo(-22.37f).Within(0.01f));
            Assert.That(FieldGeometry.MpsToMph(20f), Is.EqualTo(2f * FieldGeometry.MpsToMph(10f)).Within(1e-4f));
        }

        // ── field space ────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Spot_AtMidfield_IsTheOrigin()
        {
            var midfield = FieldGeometry.Spot(FieldGeometry.MidfieldYard, 0f);

            // The 50 is the authored origin of the pitch frame. The subtraction is exact in float, so this
            // is really an equality; the tolerance is only here to match the house rule on floats.
            Assert.That(midfield.x, Is.EqualTo(0f).Within(1e-6f), "the 50-yard line is the origin of pitch-local space");
            Assert.That(midfield.z, Is.EqualTo(0f).Within(1e-6f), "zero lateral offset is the centre of the field");
        }

        [Test]
        public void Spot_AtEitherGoalLine_IsFortyFiveSevenTwoMetresFromCentre()
        {
            // 50 yards either way. The literal is the ground truth being protected.
            var home = FieldGeometry.Spot(FieldGeometry.HomeGoalLineYard, 0f);
            var away = FieldGeometry.Spot(FieldGeometry.AwayGoalLineYard, 0f);

            Assert.That(home.x, Is.EqualTo(-45.72f).Within(PositionTolerance));
            Assert.That(away.x, Is.EqualTo(45.72f).Within(PositionTolerance));

            // +X runs toward the away goal, per the documented frame. A sign flip here would mirror the
            // whole sim and still look like football until a yard line was read off a wall.
            Assert.That(away.x, Is.GreaterThan(home.x), "+X must run from the home goal line toward the away one");
        }

        [Test]
        public void Spot_AtEitherEndLine_IsFiftyFourEightSixFourMetresFromCentre()
        {
            // 60 yards either way: 50 to the goal line plus a 10-yard end zone.
            var homeEndLine = FieldGeometry.Spot(
                FieldGeometry.HomeGoalLineYard - FieldGeometry.EndZoneDepthYards, 0f);
            var awayEndLine = FieldGeometry.Spot(
                FieldGeometry.AwayGoalLineYard + FieldGeometry.EndZoneDepthYards, 0f);

            Assert.That(homeEndLine.x, Is.EqualTo(-54.864f).Within(PositionTolerance));
            Assert.That(awayEndLine.x, Is.EqualTo(54.864f).Within(PositionTolerance));
        }

        [Test]
        public void Spot_PutsEveryPointOnTheTurfPlane()
        {
            // Spot is the ground plane; bodies and the ball add their own height on top. If it ever returned
            // a non-zero Y, every body would float by that amount and nothing would report an error.
            Assert.That(FieldGeometry.Spot(FieldGeometry.MidfieldYard, 0f).y, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(FieldGeometry.Spot(12f, -20f).y, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(FieldGeometry.Spot(105f, FieldGeometry.HalfWidthYards).y, Is.EqualTo(0f).Within(1e-6f));
        }

        [Test]
        public void Spot_YardAtAndLateralAt_RoundTripAPoint()
        {
            // Routes are authored in yards, baked into metres, and read back in yards for the stat line.
            float[] yards = { -10f, 0f, 17f, 37f, FieldGeometry.MidfieldYard, 83f, 100f, 110f };
            float[] laterals = { 0f, 12.5f, -20.25f, FieldGeometry.HashLateralYards, -FieldGeometry.HalfWidthYards };

            foreach (float yard in yards)
            {
                foreach (float lateral in laterals)
                {
                    var spot = FieldGeometry.Spot(yard, lateral);

                    Assert.That(FieldGeometry.YardAt(spot), Is.EqualTo(yard).Within(PositionTolerance),
                        $"yard {yard} at lateral {lateral} did not come back");
                    Assert.That(FieldGeometry.LateralAt(spot), Is.EqualTo(lateral).Within(PositionTolerance),
                        $"lateral {lateral} at yard {yard} did not come back");
                }
            }
        }

        [Test]
        public void YardAt_AndLateralAt_ReadTheAxesTheyAreDocumentedToRead()
        {
            // Pins the axis assignment rather than the arithmetic. An X/Z swap round-trips perfectly through
            // Spot and so would pass every test above, while rotating the entire field ninety degrees.
            Assert.That(FieldGeometry.YardAt(new Vector3(0f, 99f, 77f)),
                Is.EqualTo(FieldGeometry.MidfieldYard).Within(PositionTolerance),
                "the yard axis is X — height and lateral offset must not move it");

            Assert.That(FieldGeometry.LateralAt(new Vector3(77f, 99f, 0f)), Is.EqualTo(0f).Within(PositionTolerance),
                "the lateral axis is Z — the yard line and height must not move it");

            // +Z runs toward the left sideline, so a positive lateral offset must read back positive.
            Assert.That(FieldGeometry.LateralAt(FieldGeometry.Spot(FieldGeometry.MidfieldYard, 10f)),
                Is.GreaterThan(0f));
        }

        [Test]
        public void FieldDimensions_MatchTheRealPitch()
        {
            // Consequences of the constants rather than definitions, so these are derived — but the metre
            // figures on the right are the real ones, which is what makes the check worth having.
            float goalLineToGoalLine =
                FieldGeometry.Spot(FieldGeometry.AwayGoalLineYard, 0f).x
                - FieldGeometry.Spot(FieldGeometry.HomeGoalLineYard, 0f).x;
            Assert.That(goalLineToGoalLine, Is.EqualTo(91.44f).Within(PositionTolerance), "100 yards between the goal lines");

            // 53 1/3 yards sideline to sideline.
            Assert.That(FieldGeometry.YardsToMeters(FieldGeometry.FieldWidthYards),
                Is.EqualTo(48.768f).Within(PositionTolerance));
            Assert.That(FieldGeometry.HalfWidthYards,
                Is.EqualTo(FieldGeometry.FieldWidthYards * 0.5f).Within(1e-5f));

            // The hashes sit 18 ft 6 in apart, which is 5.6388 m.
            Assert.That(FieldGeometry.YardsToMeters(FieldGeometry.HashLateralYards * 2f),
                Is.EqualTo(18.5f * FootInMeters).Within(PositionTolerance));
        }

        // ── the painted number ─────────────────────────────────────────────────────────────────────

        [Test]
        public void MarkedYardLine_CountsDownFromBothGoalLines()
        {
            // The absolute yard runs 0→100 one way; the paint on the grass runs 0→50→0. Getting this
            // backwards puts "OPP 35" on the wall when the ball is on the offense's own 35.
            Assert.That(FieldGeometry.MarkedYardLine(35f), Is.EqualTo(35));
            Assert.That(FieldGeometry.MarkedYardLine(65f), Is.EqualTo(35), "the away 35 is also painted 35");
            Assert.That(FieldGeometry.MarkedYardLine(50f), Is.EqualTo(50), "midfield is the only yard painted 50");

            Assert.That(FieldGeometry.MarkedYardLine(0f), Is.EqualTo(0));
            Assert.That(FieldGeometry.MarkedYardLine(100f), Is.EqualTo(0));
            Assert.That(FieldGeometry.MarkedYardLine(20f), Is.EqualTo(20));
            Assert.That(FieldGeometry.MarkedYardLine(80f), Is.EqualTo(20));
        }

        [Test]
        public void MarkedYardLine_IsSymmetricAboutMidfieldAndNeverLeavesZeroToFifty()
        {
            // Whole yards only — the .5 cases sit on a rounding boundary and are not a promise the code makes.
            for (int yard = 0; yard <= 100; yard++)
            {
                int mark = FieldGeometry.MarkedYardLine(yard);

                Assert.That(mark, Is.EqualTo(FieldGeometry.MarkedYardLine(100 - yard)),
                    $"yard {yard} and yard {100 - yard} are the same painted number");
                Assert.That(mark, Is.InRange(0, 50), $"yard {yard} produced a number that is not painted on any field");
            }
        }

        [Test]
        public void MarkedYardLine_ClampsPointsInsideTheEndZonesToTheGoalLine()
        {
            // There is no paint behind the goal line, so a ball in the end zone reads as the goal line
            // rather than a negative number.
            Assert.That(FieldGeometry.MarkedYardLine(-10f), Is.EqualTo(0));
            Assert.That(FieldGeometry.MarkedYardLine(-3f), Is.EqualTo(0));
            Assert.That(FieldGeometry.MarkedYardLine(110f), Is.EqualTo(0));
            Assert.That(FieldGeometry.MarkedYardLine(103f), Is.EqualTo(0));
        }

        // ── bounds ─────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void InBounds_RejectsAFootOutsideTheSidelineAndAcceptsAFootInside()
        {
            // A foot is far larger than any float noise at this scale, so this is a real edge test and not a
            // coin flip on the exact boundary — which the code makes no promise about either way.
            float sidelineZ = FieldGeometry.YardsToMeters(FieldGeometry.HalfWidthYards);

            Assert.IsTrue(FieldGeometry.InBounds(new Vector3(0f, 0f, sidelineZ - FootInMeters)),
                "a foot inside the left sideline is on the field");
            Assert.IsFalse(FieldGeometry.InBounds(new Vector3(0f, 0f, sidelineZ + FootInMeters)),
                "a foot outside the left sideline is off it");

            // Both sidelines, because the check is on the absolute lateral offset.
            Assert.IsTrue(FieldGeometry.InBounds(new Vector3(0f, 0f, -sidelineZ + FootInMeters)));
            Assert.IsFalse(FieldGeometry.InBounds(new Vector3(0f, 0f, -sidelineZ - FootInMeters)));
        }

        [Test]
        public void InBounds_AcceptsTheEndZonesButRejectsPastTheEndLines()
        {
            // The end zones are in bounds — a body standing in one is legal, which is the whole point of
            // measuring to the back of the end zone rather than to the goal line.
            Assert.IsTrue(FieldGeometry.InBounds(FieldGeometry.Spot(-8f, 0f)), "a body in the home end zone is on the field");
            Assert.IsTrue(FieldGeometry.InBounds(FieldGeometry.Spot(108f, 0f)), "a body in the away end zone is on the field");

            // Two yards clear of each end line, again to stay well off the exact boundary.
            Assert.IsFalse(FieldGeometry.InBounds(FieldGeometry.Spot(-12f, 0f)));
            Assert.IsFalse(FieldGeometry.InBounds(FieldGeometry.Spot(112f, 0f)));
        }

        [Test]
        public void InBounds_AcceptsTheAlignmentSpotsAPlayActuallyUses()
        {
            // The hash marks and midfield are where alignments are authored. If the validator rejected these
            // the failure would only show up as a play that refuses to bake.
            Assert.IsTrue(FieldGeometry.InBounds(FieldGeometry.Spot(FieldGeometry.MidfieldYard, 0f)));
            Assert.IsTrue(FieldGeometry.InBounds(
                FieldGeometry.Spot(FieldGeometry.MidfieldYard, FieldGeometry.HashLateralYards)));
            Assert.IsTrue(FieldGeometry.InBounds(
                FieldGeometry.Spot(FieldGeometry.MidfieldYard, -FieldGeometry.HashLateralYards)));
            Assert.IsTrue(FieldGeometry.InBounds(FieldGeometry.Spot(FieldGeometry.HomeGoalLineYard, 0f)));
            Assert.IsTrue(FieldGeometry.InBounds(FieldGeometry.Spot(FieldGeometry.AwayGoalLineYard, 0f)));
        }

        // ── which way each team drives ─────────────────────────────────────────────────────────────

        [Test]
        public void GoalLineFor_AndDirectionFor_AgreeOnWhichWayEachTeamDrives()
        {
            Assert.That(FieldGeometry.GoalLineFor(TeamSide.Home),
                Is.EqualTo(FieldGeometry.AwayGoalLineYard).Within(1e-6f), "home drives toward 100");
            Assert.That(FieldGeometry.GoalLineFor(TeamSide.Away),
                Is.EqualTo(FieldGeometry.HomeGoalLineYard).Within(1e-6f), "away drives toward 0");

            Assert.That(FieldGeometry.DirectionFor(TeamSide.Home), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(FieldGeometry.DirectionFor(TeamSide.Away), Is.EqualTo(-1f).Within(1e-6f));

            // The pair has to be consistent: stepping from midfield along the direction must close on the
            // goal line, not run away from it. This is the invariant the sign convention exists to hold.
            foreach (TeamSide side in new[] { TeamSide.Home, TeamSide.Away })
            {
                float goal = FieldGeometry.GoalLineFor(side);
                float stepped = FieldGeometry.MidfieldYard + FieldGeometry.DirectionFor(side) * 10f;

                Assert.That(Mathf.Abs(goal - stepped), Is.LessThan(Mathf.Abs(goal - FieldGeometry.MidfieldYard)),
                    $"{side} moving along its own direction must get closer to its own goal line");
            }
        }

        [Test]
        public void YardsToGoal_IsTheDistanceRemainingForBothSides()
        {
            // The same spot on the field is a different distance depending on who has the ball.
            Assert.That(FieldGeometry.YardsToGoal(TeamSide.Home, 25f), Is.EqualTo(75f).Within(PositionTolerance));
            Assert.That(FieldGeometry.YardsToGoal(TeamSide.Away, 25f), Is.EqualTo(25f).Within(PositionTolerance));

            Assert.That(FieldGeometry.YardsToGoal(TeamSide.Home, FieldGeometry.MidfieldYard),
                Is.EqualTo(FieldGeometry.MidfieldYard).Within(PositionTolerance));
            Assert.That(FieldGeometry.YardsToGoal(TeamSide.Away, FieldGeometry.MidfieldYard),
                Is.EqualTo(FieldGeometry.MidfieldYard).Within(PositionTolerance));

            // At its own goal line each side has the full field to cross; at the other's it has none.
            Assert.That(FieldGeometry.YardsToGoal(TeamSide.Home, FieldGeometry.HomeGoalLineYard),
                Is.EqualTo(FieldGeometry.FieldLengthYards).Within(PositionTolerance));
            Assert.That(FieldGeometry.YardsToGoal(TeamSide.Home, FieldGeometry.AwayGoalLineYard),
                Is.EqualTo(0f).Within(PositionTolerance));
            Assert.That(FieldGeometry.YardsToGoal(TeamSide.Away, FieldGeometry.AwayGoalLineYard),
                Is.EqualTo(FieldGeometry.FieldLengthYards).Within(PositionTolerance));
            Assert.That(FieldGeometry.YardsToGoal(TeamSide.Away, FieldGeometry.HomeGoalLineYard),
                Is.EqualTo(0f).Within(PositionTolerance));
        }

        [Test]
        public void YardsToGoal_ForTheTwoSides_SumsToTheFieldLength()
        {
            // Between the goal lines the two distances have to partition the field. This catches a sign or
            // an off-by-fifty on one side that the individual spot checks above could still agree with.
            for (int yard = 0; yard <= 100; yard++)
            {
                float sum = FieldGeometry.YardsToGoal(TeamSide.Home, yard)
                          + FieldGeometry.YardsToGoal(TeamSide.Away, yard);

                Assert.That(sum, Is.EqualTo(FieldGeometry.FieldLengthYards).Within(PositionTolerance),
                    $"the two distances from yard {yard} must add up to the field");
            }
        }

        [Test]
        public void YardsToGoal_FallsByTheYardsGainedAlongTheDriveDirection()
        {
            // A gain is a gain for either side. This is the property DriveDirector folds down and distance
            // with, so it matters more than any single value.
            const float gain = 15f;

            foreach (TeamSide side in new[] { TeamSide.Home, TeamSide.Away })
            {
                float start = FieldGeometry.MidfieldYard - FieldGeometry.DirectionFor(side) * 30f;
                float after = start + FieldGeometry.DirectionFor(side) * gain;

                Assert.That(FieldGeometry.YardsToGoal(side, after),
                    Is.EqualTo(FieldGeometry.YardsToGoal(side, start) - gain).Within(PositionTolerance),
                    $"{side} gaining {gain} yards must have {gain} fewer to go");
            }
        }

        [Test]
        public void YardsToGoal_NeverReturnsNegative()
        {
            // Past the goal line the distance is zero, not negative — a touchdown is scored, it is not
            // minus five yards to go. A negative here would print on the wall as "-5 to go".
            for (int yard = -15; yard <= 115; yard++)
            {
                Assert.That(FieldGeometry.YardsToGoal(TeamSide.Home, yard), Is.GreaterThanOrEqualTo(0f),
                    $"home at yard {yard}");
                Assert.That(FieldGeometry.YardsToGoal(TeamSide.Away, yard), Is.GreaterThanOrEqualTo(0f),
                    $"away at yard {yard}");
            }

            // Specifically in the end zone each side is driving toward, where the raw subtraction is negative.
            Assert.That(FieldGeometry.YardsToGoal(TeamSide.Home, 105f), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(FieldGeometry.YardsToGoal(TeamSide.Away, -5f), Is.EqualTo(0f).Within(1e-6f));
        }
    }
}
