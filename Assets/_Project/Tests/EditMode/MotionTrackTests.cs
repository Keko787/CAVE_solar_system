using System;
using Stadium3D.Football;
using NUnit.Framework;
using UnityEngine;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// Evaluation purity: <see cref="MotionTrack.Sample"/> has to be a function of time and nothing else.
    /// That single property is what buys replay, looping and slow motion for free — a transport can scrub
    /// backwards, hold a frame, or run at a tenth speed without any of them needing their own machinery,
    /// because there is nothing to rewind. The moment a cursor or an accumulator creeps back in (which is
    /// exactly the shape the old per-frame integrator had) the failure is silent: playback still looks
    /// right forwards and only drifts once someone scrubs, which is to say it is only ever discovered
    /// with an audience in the CAVE. So the suite reads the same instants in a scrambled order and
    /// demands bit-identical answers, rather than trusting a forward sweep to notice.
    /// </summary>
    public class MotionTrackTests
    {
        // ── fixtures ───────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A synthetic route with a curl in it: the position sweeps in two axes at unrelated rates and the
        /// heading walks the whole compass, so a sweep crosses the ±180 seam several times and every field
        /// is genuinely varying rather than constant (a constant field would pass a purity test that a
        /// hidden cursor should fail).
        /// </summary>
        static MotionTrack BuildWindingTrack(int count, float sampleHz, float phase = 0f)
        {
            var samples = new MotionSample[count];
            var previous = Vector3.zero;
            float distance = 0f, peak = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / sampleHz;

                // Authored in field coordinates rather than raw metres, so the numbers stay in the range a
                // real route occupies instead of drifting off the turf.
                Vector3 position = FieldGeometry.Spot(
                    FieldGeometry.MidfieldYard + 14f * Mathf.Sin(t * 0.7f + phase),
                    9f * Mathf.Cos(t * 1.1f + phase));

                float step = i == 0 ? 0f : Vector3.Distance(previous, position);
                float speed = i == 0 ? 0f : step * sampleHz;
                distance += step;
                peak = Mathf.Max(peak, speed);

                // Wrapped into [−180, 180) so the heading track crosses the seam on its own.
                float heading = Mathf.Repeat(t * 143f + phase * Mathf.Rad2Deg, 360f) - 180f;

                samples[i] = new MotionSample(t, position, heading, speed, distance, peak);
                previous = position;
            }

            return new MotionTrack(samples, sampleHz);
        }

        /// <summary>
        /// Every field, with no tolerance at all. Sampling the same time twice runs the same arithmetic on
        /// the same inputs, so anything short of bit-identical means state leaked between the two calls —
        /// and a tolerance here would let exactly the drift this suite exists to catch slip through.
        /// </summary>
        static void AssertIdentical(in MotionSample expected, in MotionSample actual, string context)
        {
            Assert.That(actual.Time, Is.EqualTo(expected.Time), $"{context}: Time");
            Assert.That(actual.Position, Is.EqualTo(expected.Position), $"{context}: Position");
            Assert.That(actual.HeadingDegrees, Is.EqualTo(expected.HeadingDegrees), $"{context}: HeadingDegrees");
            Assert.That(actual.SpeedMps, Is.EqualTo(expected.SpeedMps), $"{context}: SpeedMps");
            Assert.That(actual.DistanceMeters, Is.EqualTo(expected.DistanceMeters), $"{context}: DistanceMeters");
            Assert.That(actual.PeakSpeedMps, Is.EqualTo(expected.PeakSpeedMps), $"{context}: PeakSpeedMps");
        }

        /// <summary>
        /// Times spanning the whole track and overshooting both ends, so a sweep exercises the pre-snap
        /// clamp, the interpolating body and the terminal clamp in one pass.
        /// </summary>
        static float[] SweepTimes(MotionTrack track, int steps)
        {
            var times = new float[steps];
            for (int i = 0; i < steps; i++)
                times[i] = Mathf.Lerp(-0.5f, track.Duration + 0.5f, i / (float)(steps - 1));
            return times;
        }

        // ── purity ─────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Sample_ReadInShuffledOrder_ReturnsExactlyWhatTheForwardSweepReturned()
        {
            // 101 is prime and 37 is coprime with it, so stepping by 37 visits every index exactly once.
            // A fixed stride rather than Random: a purity test that only fails on some seeds is worse than
            // no test, because the failure arrives detached from the change that caused it.
            const int steps = 101;
            const int stride = 37;

            var track = BuildWindingTrack(120, 20f);
            var times = SweepTimes(track, steps);

            var forward = new MotionSample[steps];
            for (int i = 0; i < steps; i++)
                forward[i] = track.Sample(times[i]);

            for (int k = 0; k < steps; k++)
            {
                int i = (k * stride + 43) % steps;
                AssertIdentical(forward[i], track.Sample(times[i]), $"shuffled read {k} of time index {i}");
            }
        }

        [Test]
        public void Sample_ReadBackwards_ReturnsExactlyWhatTheForwardSweepReturned()
        {
            // The scrub case specifically. Ordinary playback only ever walks time forwards, so a cursor
            // that never rewinds would survive every forward test and fail the first time an operator
            // drags the scrubber left.
            const int steps = 64;

            var track = BuildWindingTrack(90, 24f);
            var times = SweepTimes(track, steps);

            var forward = new MotionSample[steps];
            for (int i = 0; i < steps; i++)
                forward[i] = track.Sample(times[i]);

            for (int i = steps - 1; i >= 0; i--)
                AssertIdentical(forward[i], track.Sample(times[i]), $"reverse read of time index {i}");
        }

        [Test]
        public void Sample_InterleavedAcrossTwoTracks_LeavesNeitherTrackDisturbed()
        {
            // Twenty-two bodies are sampled per frame from one clock. A cursor held statically rather than
            // per instance would pass every single-track test in this file and still corrupt every play.
            const int steps = 48;

            var a = BuildWindingTrack(80, 20f);
            var b = BuildWindingTrack(140, 30f, phase: 1.3f);

            var timesA = SweepTimes(a, steps);
            var timesB = SweepTimes(b, steps);

            var aloneA = new MotionSample[steps];
            var aloneB = new MotionSample[steps];
            for (int i = 0; i < steps; i++) aloneA[i] = a.Sample(timesA[i]);
            for (int i = 0; i < steps; i++) aloneB[i] = b.Sample(timesB[i]);

            for (int i = 0; i < steps; i++)
            {
                AssertIdentical(aloneA[i], a.Sample(timesA[i]), $"track A interleaved at {i}");
                AssertIdentical(aloneB[i], b.Sample(timesB[i]), $"track B interleaved at {i}");
            }
        }

        [Test]
        public void Sample_RepeatedAtOneInstant_NeverBudges()
        {
            // A held frame — paused transport, or a slow-motion pass that lands on the same time for
            // several frames running. Nothing may creep.
            var track = BuildWindingTrack(60, 20f);
            float t = track.Duration * 0.37f;

            var first = track.Sample(t);
            for (int i = 0; i < 32; i++)
                AssertIdentical(first, track.Sample(t), $"repeat {i}");
        }

        // ── the heading seam ───────────────────────────────────────────────────────────────────────

        [Test]
        public void Sample_AcrossTheHeadingSeam_TakesTheShortWayRoundTheCompass()
        {
            // Two degrees apart the short way, 358 the long way. A plain lerp would put the midpoint at 0 —
            // the body facing its own goal line for a frame — which is the single most visible artefact
            // MotionSample.Lerp exists to prevent.
            var track = new MotionTrack(new[]
            {
                new MotionSample(0f, FieldGeometry.Spot(FieldGeometry.MidfieldYard, 0f), 179f, 0f, 0f, 0f),
                new MotionSample(1f, FieldGeometry.Spot(FieldGeometry.MidfieldYard, 0f), -179f, 0f, 0f, 0f),
            }, 1f);

            float mid = track.Sample(0.5f).HeadingDegrees;

            // DeltaAngle folds +180 and −180 together, so either sign of the seam passes — which sign the
            // blend lands on is not something callers should depend on.
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(mid, 180f)), Is.LessThan(1e-2f),
                "the midpoint of 179 → −179 sits on the far side of the compass, not between them");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(mid, 0f)), Is.GreaterThan(90f),
                "a plain lerp would land here, spinning the body most of the way round in one frame");

            // A quarter of the way is a quarter of two degrees, not a quarter of 358.
            Assert.AreEqual(179.5f, track.Sample(0.25f).HeadingDegrees, 1e-2f);
        }

        [Test]
        public void Sample_AcrossTheHeadingSeamTheOtherWay_TurnsTheOppositeDirection()
        {
            var track = new MotionTrack(new[]
            {
                new MotionSample(0f, FieldGeometry.Spot(FieldGeometry.MidfieldYard, 0f), -179f, 0f, 0f, 0f),
                new MotionSample(1f, FieldGeometry.Spot(FieldGeometry.MidfieldYard, 0f), 179f, 0f, 0f, 0f),
            }, 1f);

            float mid = track.Sample(0.5f).HeadingDegrees;

            Assert.That(Mathf.Abs(Mathf.DeltaAngle(mid, 180f)), Is.LessThan(1e-2f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(mid, 0f)), Is.GreaterThan(90f));

            // Mirror of the forward case: the short way from −179 is downwards through the seam.
            Assert.AreEqual(-179.5f, track.Sample(0.25f).HeadingDegrees, 1e-2f);
        }

        // ── the clamped ends ───────────────────────────────────────────────────────────────────────

        [Test]
        public void Sample_BeforeTheSnap_ReturnsTheFirstSampleVerbatim()
        {
            var track = BuildWindingTrack(50, 20f);

            foreach (float t in new[] { -1e-4f, -0.5f, -12f, -10000f })
                AssertIdentical(track.SnapPose, track.Sample(t), $"pre-snap time {t}");
        }

        [Test]
        public void Sample_AtOrPastTheDuration_ReturnsTheTerminalSampleVerbatim()
        {
            // A play is a finite event: past the whistle the body holds its last pose forever rather than
            // wrapping, because the loop is a transport decision and not a modulo buried in here.
            var track = BuildWindingTrack(50, 20f);

            foreach (float t in new[] { track.Duration, track.Duration + 1e-3f, track.Duration + 60f,
                                        float.PositiveInfinity })
                AssertIdentical(track.TerminalPose, track.Sample(t), $"post-play time {t}");

            Assert.That(track.TotalDistanceMeters, Is.EqualTo(track.TerminalPose.DistanceMeters));
            Assert.That(track.PeakSpeedMps, Is.EqualTo(track.TerminalPose.PeakSpeedMps));
        }

        [Test]
        public void SnapPose_IsTheSameBitExactPoseAsSampleAtZero()
        {
            var track = BuildWindingTrack(50, 20f);

            // No tolerance: the pre-snap pose is written from the authored waypoint and never interpolated,
            // which is what lets a play rewind to its formation and look identical every time.
            AssertIdentical(track.SnapPose, track.Sample(0f), "Sample(0)");
            AssertIdentical(track.SnapPose, track.At(0), "At(0)");
        }

        [Test]
        public void Sample_SweptAcrossTheWholePlay_KeepsDistanceAndPeakSpeedMonotone()
        {
            // Both fields are documented monotone non-decreasing, and both are read straight into the stats
            // ledger. Blending two non-decreasing samples cannot decrease, so this holds at every instant
            // and not merely at the baked ones.
            var track = BuildWindingTrack(120, 20f);
            var times = SweepTimes(track, 200);

            float lastDistance = float.NegativeInfinity;
            float lastPeak = float.NegativeInfinity;

            foreach (float t in times)
            {
                var s = track.Sample(t);
                Assert.That(s.DistanceMeters, Is.GreaterThanOrEqualTo(lastDistance - 1e-3f),
                    $"distance went backwards at t = {t}");
                Assert.That(s.PeakSpeedMps, Is.GreaterThanOrEqualTo(lastPeak - 1e-2f),
                    $"a running peak fell at t = {t}");
                lastDistance = s.DistanceMeters;
                lastPeak = s.PeakSpeedMps;
            }
        }

        // ── standing ───────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Standing_DoesNotMoveAndCoversTheRequestedDuration()
        {
            // 2.5 s at 30 Hz is 75 whole steps, so the sample count lands on the request exactly and there
            // is no rounding to argue about here.
            const float requested = 2.5f;
            const float sampleHz = 30f;

            var spot = FieldGeometry.Spot(FieldGeometry.MidfieldYard + 3f, FieldGeometry.HashLateralYards);
            var track = MotionTrack.Standing(spot, 137f, requested, sampleHz);

            Assert.That(track.Duration, Is.GreaterThanOrEqualTo(requested - 1e-3f),
                "a padded track that stops short of the play leaves the body with nothing to hold");
            Assert.That(track.Count, Is.GreaterThanOrEqualTo(2));

            foreach (float t in SweepTimes(track, 40))
            {
                var s = track.Sample(t);
                Assert.That(Vector3.Distance(s.Position, spot), Is.LessThan(1e-3f), $"drifted at t = {t}");
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(s.HeadingDegrees, 137f)), Is.LessThan(1e-3f),
                    $"facing turned at t = {t}");
                Assert.AreEqual(0f, s.SpeedMps, 1e-2f, $"speed at t = {t}");
                Assert.AreEqual(0f, s.DistanceMeters, 1e-3f, $"distance at t = {t}");
            }

            Assert.AreEqual(0f, track.TotalDistanceMeters, 1e-3f);
            Assert.AreEqual(0f, track.PeakSpeedMps, 1e-2f);
        }

        [Test]
        public void Standing_WithADurationOffTheSampleGrid_LandsWithinHalfASampleOfIt()
        {
            // Standing rounds the count to the nearest whole sample rather than always rounding up, so the
            // property that certainly holds is "within half a sample period of the request" — not a strict
            // floor. Asserting the guarantee the code actually makes rather than the one a grid-aligned
            // duration happens to give.
            const float requested = 2.51f;
            const float sampleHz = 30f;
            const float halfStep = 0.5f / sampleHz;

            var track = MotionTrack.Standing(Vector3.zero, 0f, requested, sampleHz);

            Assert.That(track.Duration, Is.GreaterThan(requested - halfStep - 1e-3f));
            Assert.That(track.Duration, Is.LessThan(requested + halfStep + 1e-3f));
        }

        [Test]
        public void Standing_WithNoDuration_StillHasTwoSamplesSoItRemainsSampleable()
        {
            // The degenerate request has to produce a usable track, not a one-sample track that the O(1)
            // index math would then have to special-case at every call site.
            var spot = FieldGeometry.Spot(FieldGeometry.MidfieldYard, 0f);
            var track = MotionTrack.Standing(spot, 0f, 0f, 30f);

            Assert.That(track.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(track.Duration, Is.GreaterThanOrEqualTo(0f));
            Assert.That(Vector3.Distance(track.Sample(5f).Position, spot), Is.LessThan(1e-3f));
        }

        [Test]
        public void Standing_WithANegativeDuration_IsClampedRatherThanInverted()
        {
            var track = MotionTrack.Standing(Vector3.zero, 0f, -4f, 30f);

            Assert.That(track.Duration, Is.GreaterThanOrEqualTo(0f));
            Assert.That(track.Count, Is.GreaterThanOrEqualTo(2));
        }

        // ── construction ───────────────────────────────────────────────────────────────────────────

        [Test]
        public void Construct_WithNoSamples_ThrowsRatherThanYieldingATrackNothingCanRead()
        {
            // Every accessor on the type indexes the array. An empty track would fail later, somewhere
            // else, during a play — so it fails here instead, at load.
            Assert.Throws<ArgumentException>(() => new MotionTrack(new MotionSample[0], 30f));
            Assert.Throws<ArgumentException>(() => new MotionTrack(null, 30f));
        }

        [Test]
        public void Construct_WithASingleSample_IsAStillTrackRatherThanAnError()
        {
            var only = new MotionSample(0f, FieldGeometry.Spot(FieldGeometry.MidfieldYard, 4f), 90f, 0f, 0f, 0f);
            var track = new MotionTrack(new[] { only }, 30f);

            Assert.That(track.Count, Is.EqualTo(1));
            Assert.That(track.Duration, Is.EqualTo(0f).Within(1e-3f));

            // Every time, including times outside the track, resolves to the one sample verbatim.
            foreach (float t in new[] { -3f, 0f, 0.5f, 90f })
                AssertIdentical(only, track.Sample(t), $"single-sample track at t = {t}");
        }

        [Test]
        public void Construct_WithANonsenseRate_FloorsTheSampleRate()
        {
            // The rate multiplies straight into the index. Zero or negative would collapse every lookup
            // onto the first sample, which reads as a play where nobody moves.
            var samples = new[]
            {
                new MotionSample(0f, Vector3.zero, 0f, 0f, 0f, 0f),
                new MotionSample(1f, Vector3.zero, 0f, 0f, 0f, 0f),
            };

            Assert.That(new MotionTrack(samples, 0f).SampleHz, Is.GreaterThanOrEqualTo(1f));
            Assert.That(new MotionTrack(samples, -60f).SampleHz, Is.GreaterThanOrEqualTo(1f));
        }

        [Test]
        public void At_OutOfRangeIndex_ClampsInsteadOfThrowing()
        {
            var track = BuildWindingTrack(20, 20f);

            AssertIdentical(track.SnapPose, track.At(-7), "At(−7)");
            AssertIdentical(track.TerminalPose, track.At(track.Count + 7), "At(Count + 7)");
        }
    }
}
