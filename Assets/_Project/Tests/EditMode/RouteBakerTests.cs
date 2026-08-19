using System.Collections.Generic;
using Stadium3D.Football;
using NUnit.Framework;
using UnityEngine;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// The motion contract a baked route has to satisfy, and the four defects of the per-frame integrator
    /// this baker replaced. A route is solved once at load and then only ever read, so a defect here is not
    /// one bad frame — it is a whole play that is wrong for as long as it is on the walls, and nothing
    /// inside the runtime is in a position to notice. That is why this suite asserts the <em>envelope</em>
    /// structurally (nothing outruns the top speed, nothing steps further than a frame of it allows, no
    /// facing snaps) rather than checking a handful of hand-computed poses: the envelope is what the old
    /// <c>SimulatedFeedSource.Advance</c> broke, and a pose-by-pose test would happily pass on motion that
    /// still reads wrong. The four named regressions below each pin one of its failures, so a drift back
    /// toward that shape fails in the Test Runner instead of in front of an audience in the CAVE.
    /// </summary>
    public class RouteBakerTests
    {
        // ── fixtures ───────────────────────────────────────────────────────────────────────────────

        const float SampleHz = 60f;

        /// <summary>Where the ball is spotted for every route here. Any spot bakes the same; a fixed one
        /// keeps the authored yards and the baked metres comparable by hand when something does fail.</summary>
        const float LineOfScrimmageYard = 30f;
        const float BallLateralYards = 0f;

        /// <summary>
        /// Comfortably longer than anything in this fixture bakes to, so <see cref="MotionTrack.Duration"/>
        /// is the pad rather than the route's natural end. That matters for the arrival assertions: past the
        /// natural end the baker pins the pose to the terminal node verbatim, so "did the body get there" is
        /// a question about the authored point instead of about where the last resampled instant landed.
        /// </summary>
        const float MinDuration = 10f;

        /// <summary>
        /// The pre-snap facing used throughout: a body squared up the opposite way from its first step. It is
        /// the worst case for the turn-rate limit, and it is exactly what the old <c>LookRotation</c> snapped
        /// through in a single frame.
        /// </summary>
        const float PreSnapFacingDegrees = -90f;

        static readonly MotionLimits Limits = MotionLimits.Skill;

        /// <summary>The authored waypoints as pitch-local points, the pairing <see cref="RouteBaker"/> expects.</summary>
        static List<Vector3> PitchLocal(List<RouteWaypoint> waypoints)
        {
            var points = new List<Vector3>(waypoints.Count);
            for (int i = 0; i < waypoints.Count; i++)
                points.Add(waypoints[i].ToPitchLocal(LineOfScrimmageYard, BallLateralYards, TeamSide.Home));
            return points;
        }

        static MotionTrack Bake(List<RouteWaypoint> waypoints) =>
            RouteBaker.Bake(PitchLocal(waypoints), waypoints, Limits, PreSnapFacingDegrees,
                            MinDuration, SampleHz);

        /// <summary>
        /// A forty-yard go route, straight downfield, stopping at the top. Long enough that the speed solve
        /// reaches the top-speed cap, holds it, and has to brake back off it — so it is the route that
        /// stresses the speed envelope rather than the turn.
        /// </summary>
        static List<RouteWaypoint> GoRoute() => new List<RouteWaypoint>
        {
            new RouteWaypoint { DepthYards = 0f,  LateralYards = 0f },
            new RouteWaypoint { DepthYards = 20f, LateralYards = 0f },
            new RouteWaypoint { DepthYards = 40f, LateralYards = 0f, Kind = WaypointKind.Stop },
        };

        /// <summary>
        /// A twelve-yard dig: sprint downfield, square in, run it across. The break is where the grip cap,
        /// the turn-rate limit and the accel/decel ramps all bite at once, so it is the route that stresses
        /// the facing.
        /// </summary>
        static List<RouteWaypoint> DigRoute() => new List<RouteWaypoint>
        {
            new RouteWaypoint { DepthYards = 0f,  LateralYards = 0f },
            new RouteWaypoint { DepthYards = 12f, LateralYards = 0f,  Kind = WaypointKind.Cut, SpeedFraction = 0.5f },
            new RouteWaypoint { DepthYards = 12f, LateralYards = 10f, Kind = WaypointKind.Stop },
        };

        /// <summary>
        /// No sample may sit further from the one before it than a frame of top speed accounts for. Written
        /// once so both routes can be held to it — the go route because it is the fastest, the dig because a
        /// corner is where a path resample is most likely to short-cut.
        /// </summary>
        static void AssertNoTeleport(MotionTrack track, string label)
        {
            // 5% over the exact bound, for the resampler's straight-line blend between path nodes.
            float ceiling = Limits.MaxSpeedMps * (1f / SampleHz) * 1.05f;

            for (int i = 1; i < track.Count; i++)
            {
                float step = Vector3.Distance(track.At(i - 1).Position, track.At(i).Position);
                Assert.That(step, Is.LessThanOrEqualTo(ceiling),
                    $"{label}: sample {i} at t = {track.At(i).Time:0.000}s jumped {step:0.000} m in one frame");
            }
        }

        /// <summary>
        /// No adjacent pair of samples may rotate faster than the authored turn rate. The bound is tight —
        /// coming off the line the solve turns at exactly the limit — so the eps only has to absorb float
        /// rounding and the sub-millidegree steps a zero-length path segment contributes at a node boundary.
        /// A snapped facing is tens of degrees in a frame and is nowhere near it.
        /// </summary>
        static void AssertNoFacingSnap(MotionTrack track, string label)
        {
            float ceiling = Limits.MaxTurnRateDegPerSec * (1f / SampleHz) + 0.25f;

            for (int i = 1; i < track.Count; i++)
            {
                float turned = Mathf.Abs(Mathf.DeltaAngle(
                    track.At(i - 1).HeadingDegrees, track.At(i).HeadingDegrees));

                Assert.That(turned, Is.LessThanOrEqualTo(ceiling),
                    $"{label}: sample {i} at t = {track.At(i).Time:0.000}s turned {turned:0.00}° in one frame");
            }
        }

        // ── the motion contract ────────────────────────────────────────────────────────────────────

        [Test]
        public void SnapPose_StartsAtRestOnTheAuthoredAlignment()
        {
            var route = GoRoute();
            var track = Bake(route);
            var alignment = PitchLocal(route)[0];

            // Nobody is moving at the snap. This one is exact: the zeroth sample is written, not solved.
            Assert.That(track.Sample(0f).SpeedMps, Is.EqualTo(0f));
            Assert.That(track.SnapPose.Time, Is.EqualTo(0f));
            Assert.That(track.SnapPose.DistanceMeters, Is.EqualTo(0f));
            Assert.That(track.SnapPose.PeakSpeedMps, Is.EqualTo(0f));

            // The pre-snap facing is carried through verbatim rather than derived from the first step, so a
            // lineman in his stance faces the defense and not the direction he is about to move.
            Assert.That(track.SnapPose.HeadingDegrees, Is.EqualTo(PreSnapFacingDegrees));

            // The position comes off the spline evaluated at its first knot, which is the authored point to
            // within the arithmetic — hence a tolerance here where the fields above take none.
            Assert.That(Vector3.Distance(track.SnapPose.Position, alignment), Is.LessThan(1e-3f));
        }

        [Test]
        public void EverySample_StaysInsideTheSpeedEnvelope()
        {
            var track = Bake(GoRoute());
            float ceiling = Limits.MaxSpeedMps * 1.02f;

            for (int i = 0; i < track.Count; i++)
                Assert.That(track.At(i).SpeedMps, Is.LessThanOrEqualTo(ceiling),
                    $"sample {i} at t = {track.At(i).Time:0.000}s outran the top-speed cap");

            Assert.That(track.PeakSpeedMps, Is.LessThanOrEqualTo(ceiling));

            // ...and the cap is genuinely being approached, so the bound above is not passing on a route that
            // simply never got going.
            Assert.That(track.PeakSpeedMps, Is.GreaterThan(Limits.MaxSpeedMps * 0.9f),
                "forty yards of straight route has to reach something near top speed for this to mean anything");
        }

        [Test]
        public void AdjacentSamples_NeverTeleport()
        {
            AssertNoTeleport(Bake(GoRoute()), "go route");
            AssertNoTeleport(Bake(DigRoute()), "dig route");
        }

        [Test]
        public void AdjacentHeadings_NeverSnapPastTheTurnRate()
        {
            AssertNoFacingSnap(Bake(GoRoute()), "go route");

            var dig = Bake(DigRoute());
            AssertNoFacingSnap(dig, "dig route");

            // The dig starts squared up backwards and finishes running across the field, so the facing really
            // does have to travel — otherwise a baker that never turned at all would pass the bound above.
            float travelled = Mathf.Abs(Mathf.DeltaAngle(
                dig.SnapPose.HeadingDegrees, dig.TerminalPose.HeadingDegrees));
            Assert.That(travelled, Is.GreaterThan(45f), "the dig's facing has to end up somewhere else entirely");
        }

        [Test]
        public void StraightRoute_BakesTheAuthoredPolylineLength()
        {
            var route = GoRoute();
            var track = Bake(route);
            var points = PitchLocal(route);

            float authored = 0f;
            for (int i = 1; i < points.Count; i++)
                authored += Vector3.Distance(points[i - 1], points[i]);

            // Derived from the authored yards rather than typed: forty yards of straight route, in metres.
            Assert.That(authored, Is.EqualTo(FieldGeometry.YardsToMeters(40f)).Within(1e-3f));

            // A spline through collinear control points is the line itself, so the resampled path can only
            // lose the odd millimetre to the arc-length walk. 5% is loose enough never to fire on that and
            // tight enough to catch a leg being skipped or a corner being cut.
            Assert.That(track.TotalDistanceMeters, Is.EqualTo(authored).Within(authored * 0.05f));
        }

        [Test]
        public void DistanceAndPeakSpeed_AreMonotoneNonDecreasing()
        {
            var track = Bake(DigRoute());
            float distance = 0f, peak = 0f;

            for (int i = 0; i < track.Count; i++)
            {
                var sample = track.At(i);

                // 1e-4 m of slack absorbs the resampler's lerp rounding at a node boundary. A real regression
                // here — a distance that resets, a peak that forgets — is metres, not microns.
                Assert.That(sample.DistanceMeters, Is.GreaterThanOrEqualTo(distance - 1e-4f),
                    $"distance went backwards at sample {i}");
                Assert.That(sample.PeakSpeedMps, Is.GreaterThanOrEqualTo(peak - 1e-4f),
                    $"peak speed was forgotten at sample {i}");

                // The running peak is written as a max of everything seen so far, so it can never trail the
                // speed at its own sample.
                Assert.That(sample.PeakSpeedMps, Is.GreaterThanOrEqualTo(sample.SpeedMps));

                distance = Mathf.Max(distance, sample.DistanceMeters);
                peak = Mathf.Max(peak, sample.PeakSpeedMps);
            }

            Assert.That(distance, Is.GreaterThan(0f), "the dig route covers ground, so this swept real values");
        }

        // ── the four defects of the old per-frame integrator ───────────────────────────────────────

        [Test]
        public void DuplicateFirstWaypoint_BecomesADwell_NotASkippedWaypoint()
        {
            // The old Advance() guarded against a zero-length step by force-advancing to the next waypoint.
            // So a body already standing on its first waypoint — which is the pre-snap case for all 22
            // players — silently lost that waypoint, and with it whatever the author had attached to it.
            // Here the repeat merges into the point it repeats, carrying its dwell, and the rest of the route
            // is still run.
            var route = new List<RouteWaypoint>
            {
                new RouteWaypoint { DepthYards = 0f,  LateralYards = 0f },
                new RouteWaypoint { DepthYards = 0f,  LateralYards = 0f, Kind = WaypointKind.Dwell, DwellSeconds = 0.75f },
                new RouteWaypoint { DepthYards = 12f, LateralYards = 0f, Kind = WaypointKind.Stop },
            };

            var points = PitchLocal(route);
            var track = Bake(route);

            // Still standing on the repeated point while the dwell it merged into runs.
            var duringDwell = track.Sample(0.5f);
            Assert.That(Vector3.Distance(duringDwell.Position, points[0]), Is.LessThan(1e-3f),
                "the repeated waypoint is a dwell, so nothing has left the alignment yet");
            Assert.That(duringDwell.SpeedMps, Is.EqualTo(0f).Within(1e-2f));

            // ...and then it runs the leg the old guard would have eaten.
            Assert.That(track.Sample(1.5f).SpeedMps, Is.GreaterThan(1f), "the body leaves the dwell and runs");
            Assert.That(Vector3.Distance(track.TerminalPose.Position, points[2]), Is.LessThan(1e-3f),
                "the far waypoint is still the end of the route");

            float authored = FieldGeometry.YardsToMeters(12f);
            Assert.That(track.TotalDistanceMeters, Is.EqualTo(authored).Within(authored * 0.05f),
                "the whole authored leg was travelled, not half of it");
        }

        [Test]
        public void SingleWaypointRoute_BakesAStandingTrack()
        {
            // One waypoint modulo one waypoint is a body stepping onto itself forever, so the old integrator
            // had no answer for a route with a single point — which is how most of the offensive line is
            // authored. Here it is an ordinary track that happens to stand still, with a real duration and a
            // real terminal pose, so nothing downstream needs to special-case it.
            var route = new List<RouteWaypoint>
            {
                new RouteWaypoint { DepthYards = -1f, LateralYards = 2f },
            };

            var stance = PitchLocal(route)[0];
            var track = Bake(route);

            Assert.That(track.Duration, Is.GreaterThanOrEqualTo(MinDuration - 1e-3f),
                "a standing body still has to cover the whole play");
            Assert.That(track.Count, Is.GreaterThan(1));
            Assert.That(track.TotalDistanceMeters, Is.EqualTo(0f).Within(1e-3f));

            for (int i = 0; i < track.Count; i++)
            {
                var sample = track.At(i);

                // Exact rather than within a tolerance: a standing track is written from the authored point,
                // never interpolated, so it cannot drift by even a float.
                Assert.That(Vector3.Distance(sample.Position, stance), Is.EqualTo(0f), $"sample {i} drifted");
                Assert.That(sample.HeadingDegrees, Is.EqualTo(PreSnapFacingDegrees),
                    $"sample {i}: the authored stance facing is held, not derived from a step that never happens");
                Assert.That(sample.SpeedMps, Is.EqualTo(0f), $"sample {i}");
            }
        }

        [Test]
        public void SampleBeyondDuration_ReturnsTheTerminalPose_NeverWraps()
        {
            // Seg = (Seg + 1) % Loop.Length made every route an infinite patrol: run past the end and the
            // body teleported back to its alignment and ran the route again, with no terminal state anywhere.
            // A play is a finite event, so past the end the terminal pose repeats verbatim and looping stays a
            // transport decision that lives in the director.
            var track = Bake(DigRoute());

            var atEnd = track.Sample(track.Duration);
            var wayPast = track.Sample(track.Duration + 5f);

            // Both take the terminal clamp, so every field matches with no tolerance at all.
            Assert.That(wayPast.Time, Is.EqualTo(atEnd.Time));
            Assert.That(Vector3.Distance(wayPast.Position, atEnd.Position), Is.EqualTo(0f));
            Assert.That(wayPast.HeadingDegrees, Is.EqualTo(atEnd.HeadingDegrees));
            Assert.That(wayPast.SpeedMps, Is.EqualTo(atEnd.SpeedMps));
            Assert.That(wayPast.DistanceMeters, Is.EqualTo(atEnd.DistanceMeters));
            Assert.That(wayPast.PeakSpeedMps, Is.EqualTo(atEnd.PeakSpeedMps));

            // And it is the end of the route, not the start of a second lap.
            Assert.That(Vector3.Distance(wayPast.Position, track.SnapPose.Position), Is.GreaterThan(1f),
                "sampling past the end must not walk the body back to its alignment");
        }

        [Test]
        public void StartsFromRest_CannotReachTopSpeedInstantly()
        {
            // travel = Speed * dt put a body at its full authored speed on the very first frame after the
            // snap, which reads as a teleport off the line — no amount of tuning fixes it, because there is
            // no acceleration in the expression at all. The solve here starts at rest and is bounded by the
            // authored acceleration, so a tenth of a second in the body has barely begun to move.
            var track = Bake(GoRoute());
            var early = track.Sample(0.1f);

            Assert.That(early.SpeedMps, Is.GreaterThan(0f), "it is moving — this is a ramp, not a stall");
            Assert.That(early.SpeedMps, Is.LessThan(Limits.MaxSpeedMps * 0.25f),
                "a tenth of a second cannot buy a quarter of top speed");

            // The tighter statement of the same thing: bounded by v = a·t, with headroom for the trapezoidal
            // integration between path nodes.
            Assert.That(early.SpeedMps, Is.LessThanOrEqualTo(Limits.AccelMps2 * 0.1f * 1.1f));
        }
    }
}
