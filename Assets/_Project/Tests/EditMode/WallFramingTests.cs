using NUnit.Framework;
using UnityEngine;
using Stadium3D.Football;
using Stadium3D.Pitch;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// The arithmetic behind the two things this room can do that a television cannot: a player at true
    /// physical scale on the side walls, and a floor view at a scale where formations actually separate.
    /// </summary>
    /// <remarks>
    /// These are worth pinning because both claims are falsifiable and neither is obvious. "Life-size" is a
    /// specific assertion — a 1.88 m athlete rendering 1.88 m tall on a 2.2225 m wall — and if the framing
    /// solve is wrong by 20% nobody standing in the room will be able to tell by eye, they will just feel
    /// that the shot is a bit tight or a bit loose. Likewise the floor scale: 1:22 versus 1:31 is the
    /// difference between readable formations and a smear, and the argument for it is arithmetic that
    /// deserves to be checked rather than trusted.
    /// </remarks>
    public class WallFramingTests
    {
        const float Tol = 1e-3f;

        // ── life-size framing ──────────────────────────────────────────────────────────────────────

        [Test]
        public void LifeSizeFov_RendersAPlayerAtTrueScale_AtEveryPlausibleDistance()
        {
            // The headline claim, checked as a round trip rather than against a hand-computed angle: solve
            // the field of view for life-size, then ask how tall the player actually renders, and require
            // the answer to be the player's real height.
            foreach (float distance in new[] { 6f, 10f, 16f, 24f, 40f })
            {
                float fov = WallFraming.LifeSizeVerticalFov(distance);
                float rendered = WallFraming.RenderedHeightOnWall(distance, fov);

                Assert.That(rendered, Is.EqualTo(WallFraming.PlayerHeightMeters).Within(Tol),
                    $"a player {distance} m from the boom should render life-size on the wall");
            }
        }

        [Test]
        public void LifeSizeFraming_MeansFramingExactlyOneWallHeightOfWorld()
        {
            // The derivation's punchline: the subject's height cancels out, so the rule is not "fit the
            // player" but "keep one wall-height of world in frame". Assert that directly, because it is the
            // property the camera rig is actually implemented against.
            const float distance = 16f;
            float fov = WallFraming.LifeSizeVerticalFov(distance);
            float visibleHeight = 2f * distance * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

            Assert.That(visibleHeight, Is.EqualTo(WallFraming.WallHeightMeters).Within(Tol));
        }

        [Test]
        public void LifeSizeFov_IsIndependentOfTheSubjectsHeight()
        {
            // Follows from the same cancellation. If this ever fails, the solve has picked up a dependency on
            // the subject and life-size framing will drift as different players are focused.
            float a = WallFraming.LifeSizeVerticalFov(16f, subjectHeight: 1.7f);
            float b = WallFraming.LifeSizeVerticalFov(16f, subjectHeight: 2.05f);

            Assert.That(a, Is.EqualTo(b).Within(Tol));
        }

        [Test]
        public void FramingScale_TradesScaleForContext()
        {
            const float distance = 16f;
            float tight = WallFraming.LifeSizeVerticalFov(distance, framingScale: 1f);
            float loose = WallFraming.LifeSizeVerticalFov(distance, framingScale: 2f);

            Assert.That(loose, Is.GreaterThan(tight), "a larger framing scale shows more world");
            Assert.That(WallFraming.RenderedHeightOnWall(distance, loose),
                Is.EqualTo(WallFraming.PlayerHeightMeters * 0.5f).Within(Tol),
                "and renders the player at correspondingly smaller scale");
        }

        [Test]
        public void ALifeSizePlayer_FitsInsideTheSideWallsAngularBudget()
        {
            // The claim that makes the near-square side aspect a requirement rather than a liability: at the
            // 1.778 m viewing distance a life-size person subtends about 56 degrees, and the wall offers 64.
            float subtended = WallFraming.AngularSizeDegrees(WallFraming.PlayerHeightMeters, 1.778f);
            float wallSubtended = WallFraming.AngularSizeDegrees(WallFraming.WallHeightMeters, 1.778f);

            Assert.That(subtended, Is.EqualTo(55.8f).Within(1.5f), "a life-size player subtends about 56 degrees");
            Assert.That(subtended, Is.LessThan(wallSubtended), "which has to fit inside what the wall offers");
        }

        // ── the floor ──────────────────────────────────────────────────────────────────────────────

        [Test]
        public void FloorScale_FitsTheFullFieldWidthAcrossTheShortAxis()
        {
            // This is the entire justification for 1:22. The wall's 2.2225 m height carries 48.9 m of field;
            // a gridiron is 48.768 m wide. It fits, with centimetres to spare and no more.
            var coverage = WallFraming.FloorCoverageMeters();
            float fieldWidth = FieldGeometry.YardsToMeters(FieldGeometry.FieldWidthYards);

            Assert.That(coverage.y, Is.GreaterThanOrEqualTo(fieldWidth),
                "1:22 must cover the full field width across the room's short axis");
            Assert.That(coverage.y - fieldWidth, Is.LessThan(1f),
                "and only just — a much larger scale would be wasting the surface");
        }

        [Test]
        public void FloorScale_ShowsMostOfTheFieldLengthButNotAllOfIt()
        {
            var coverage = WallFraming.FloorCoverageMeters();
            float yards = FieldGeometry.MetersToYards(coverage.x);

            Assert.That(yards, Is.InRange(75f, 95f),
                "about 85 of 120 yards, which is why the window has to slide with the ball");
        }

        [Test]
        public void FittingTheWholeFieldWouldMakeJerseyNumbersUnreadable()
        {
            // The counterfactual, asserted rather than asserted-about. Fitting all 120 yards forces roughly
            // 1:31, at which a 20 cm numeral subtends far less than the ~0.4 degrees needed to read two
            // digits from a metre and a half away. This test is why the floor never carries identity.
            float wholeFieldScale = FieldGeometry.YardsToMeters(
                FieldGeometry.FieldLengthYards + 2f * FieldGeometry.EndZoneDepthYards)
                / WallFraming.WideWallWidthMeters;

            Assert.That(wholeFieldScale, Is.GreaterThan(28f), "fitting the length forces a much smaller scale");

            float numeralOnFloor = 0.20f / wholeFieldScale;
            float subtended = WallFraming.AngularSizeDegrees(numeralOnFloor, 1.9f);

            Assert.That(subtended, Is.LessThan(0.4f),
                "a jersey number at that scale is below the legibility threshold, so identity belongs on the side walls");
        }

        [Test]
        public void FloorOrthographicSize_IsHalfTheVerticalCoverage()
        {
            var coverage = WallFraming.FloorCoverageMeters();
            Assert.That(WallFraming.FloorOrthographicSize(), Is.EqualTo(coverage.y * 0.5f).Within(Tol));
        }

        [Test]
        public void FloorWindow_SlidesWithTheBallButNeverLeavesTheField()
        {
            var coverage = WallFraming.FloorCoverageMeters();
            float halfExtent = FieldGeometry.YardsToMeters(
                FieldGeometry.FieldLengthYards * 0.5f + FieldGeometry.EndZoneDepthYards);

            float mid = WallFraming.FloorWindowCenterX(0f, coverage.x, halfExtent);
            Assert.That(mid, Is.EqualTo(0f).Within(Tol), "a ball at midfield centres the window");

            // Push the ball into the end zone and the window must stop at the end line, not follow it out.
            float deep = WallFraming.FloorWindowCenterX(halfExtent, coverage.x, halfExtent);
            Assert.That(deep + coverage.x * 0.5f, Is.LessThanOrEqualTo(halfExtent + Tol),
                "the window must not show apron beyond the end line");
            Assert.That(deep, Is.GreaterThan(0f), "but it should still have travelled toward the ball");
        }

        // ── render targets ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void RenderTextureSizes_MatchTheProjectorViewports()
        {
            // Matched to the .vrx viewport rectangles, which already agree with the physical aspects to four
            // decimals. Deriving them from the aspect instead invites exactly the rounding that produced the
            // inherited feed window's 1.69x stretch.
            var wide = WallFraming.RenderTextureSize(true);
            var side = WallFraming.RenderTextureSize(false);

            Assert.That(wide, Is.EqualTo(new Vector2Int(2560, 1600)));
            Assert.That(side, Is.EqualTo(new Vector2Int(1719, 1600)));
        }

        [Test]
        public void RenderTextureAspects_MatchTheirPhysicalSurfaces()
        {
            var wide = WallFraming.RenderTextureSize(true);
            var side = WallFraming.RenderTextureSize(false);

            float wideSurface = WallFraming.Aspect(WallFraming.WideWallWidthMeters, WallFraming.WallHeightMeters);
            float sideSurface = WallFraming.Aspect(WallFraming.SideWallWidthMeters, WallFraming.WallHeightMeters);

            Assert.That((float)wide.x / wide.y, Is.EqualTo(wideSurface).Within(0.001f));
            Assert.That((float)side.x / side.y, Is.EqualTo(sideSurface).Within(0.001f));
        }

        [Test]
        public void ResolutionScale_PreservesAspect()
        {
            // The perf fallback must not become the stretch bug. Halving the resolution has to halve both
            // axes, not just the one someone happened to type.
            var full = WallFraming.RenderTextureSize(false, 1f);
            var half = WallFraming.RenderTextureSize(false, 0.5f);

            Assert.That((float)half.x / half.y, Is.EqualTo((float)full.x / full.y).Within(0.01f));
        }
    }
}
