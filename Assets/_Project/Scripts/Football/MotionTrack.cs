using System;
using UnityEngine;

namespace Stadium3D.Football
{
    /// <summary>
    /// One frozen instant of one player's motion. Speed, cumulative distance and running peak speed are
    /// <b>baked fields, not accumulators</b> — every one is a value at a time, so reading a sample never
    /// depends on which samples were read before it.
    /// </summary>
    public readonly struct MotionSample
    {
        /// <summary>Seconds from the snap.</summary>
        public readonly float Time;

        /// <summary>Pitch-local position on the turf.</summary>
        public readonly Vector3 Position;

        /// <summary>Facing about Y, degrees. Not derived from velocity — a backpedalling defender faces the ball.</summary>
        public readonly float HeadingDegrees;

        /// <summary>Speed along the path, m/s.</summary>
        public readonly float SpeedMps;

        /// <summary>Distance travelled since the snap, metres. Monotone non-decreasing.</summary>
        public readonly float DistanceMeters;

        /// <summary>Highest speed reached so far this play, m/s. Monotone non-decreasing.</summary>
        public readonly float PeakSpeedMps;

        public MotionSample(float time, Vector3 position, float headingDegrees, float speedMps,
                            float distanceMeters, float peakSpeedMps)
        {
            Time = time;
            Position = position;
            HeadingDegrees = headingDegrees;
            SpeedMps = speedMps;
            DistanceMeters = distanceMeters;
            PeakSpeedMps = peakSpeedMps;
        }

        /// <summary>
        /// Blend two adjacent samples. Heading uses <see cref="Mathf.LerpAngle"/> so the 179 → −179 seam
        /// takes the short way; a plain lerp there would spin a body most of the way round the compass in one
        /// frame, which is the single most visible artefact this type exists to prevent.
        /// </summary>
        public static MotionSample Lerp(in MotionSample a, in MotionSample b, float t)
        {
            t = Mathf.Clamp01(t);
            return new MotionSample(
                Mathf.Lerp(a.Time, b.Time, t),
                Vector3.Lerp(a.Position, b.Position, t),
                Mathf.LerpAngle(a.HeadingDegrees, b.HeadingDegrees, t),
                Mathf.Lerp(a.SpeedMps, b.SpeedMps, t),
                Mathf.Lerp(a.DistanceMeters, b.DistanceMeters, t),
                Mathf.Lerp(a.PeakSpeedMps, b.PeakSpeedMps, t));
        }

        /// <summary>Facing as a rotation, for a marker transform.</summary>
        public Quaternion Rotation => Quaternion.Euler(0f, HeadingDegrees, 0f);
    }

    /// <summary>
    /// One player's whole run for one play, as an immutable fixed-rate array of <see cref="MotionSample"/>.
    /// This is the type the entire design rests on.
    /// </summary>
    /// <remarks>
    /// Believable motion needs a solver — acceleration, braking, grip-limited cuts, turn-rate-limited facing.
    /// Exact rewind needs a pure function of time. You get both by running the solver <b>once</b> at load and
    /// freezing the result here, so that per frame there is no integration at all. Nothing accumulates, so
    /// nothing drifts, so seeking backwards is free and exact — which is what makes replay, looping and slow
    /// motion fall out for nothing instead of each needing its own machinery.
    ///
    /// <para>Sampling past <see cref="Duration"/> returns the terminal sample verbatim, forever. It never
    /// wraps: a play is a finite event, and the loop is a transport decision that lives in
    /// <see cref="DriveDirector"/> rather than a modulo buried in the motion code.</para>
    /// </remarks>
    public sealed class MotionTrack
    {
        readonly MotionSample[] m_Samples;
        readonly float m_SampleHz;

        /// <summary>Samples per second. Fixed for the whole track, which is what makes lookup O(1).</summary>
        public float SampleHz => m_SampleHz;

        /// <summary>Seconds from the snap to the last sample.</summary>
        public float Duration { get; }

        /// <summary>Number of baked samples.</summary>
        public int Count => m_Samples.Length;

        /// <summary>Total path length, metres.</summary>
        public float TotalDistanceMeters => m_Samples.Length == 0 ? 0f : m_Samples[m_Samples.Length - 1].DistanceMeters;

        /// <summary>Fastest the body ever moved on this route, m/s.</summary>
        public float PeakSpeedMps => m_Samples.Length == 0 ? 0f : m_Samples[m_Samples.Length - 1].PeakSpeedMps;

        public MotionTrack(MotionSample[] samples, float sampleHz)
        {
            if (samples == null || samples.Length == 0)
                throw new ArgumentException("A MotionTrack needs at least one sample.", nameof(samples));

            m_Samples = samples;
            m_SampleHz = Mathf.Max(1f, sampleHz);
            Duration = samples[samples.Length - 1].Time;
        }

        /// <summary>The sample at index <paramref name="i"/>, clamped into range.</summary>
        public MotionSample At(int i) => m_Samples[Mathf.Clamp(i, 0, m_Samples.Length - 1)];

        /// <summary>
        /// The motion at <paramref name="time"/> seconds after the snap. O(1): the fixed rate makes the index
        /// a multiplication, so there is no binary search and — critically — no cursor to carry between calls.
        /// Below zero returns the pre-snap pose; at or past <see cref="Duration"/> returns the terminal pose.
        /// </summary>
        public MotionSample Sample(float time)
        {
            if (m_Samples.Length == 1)
                return m_Samples[0];
            if (time <= 0f)
                return m_Samples[0];
            if (time >= Duration)
                return m_Samples[m_Samples.Length - 1];

            float scaled = time * m_SampleHz;
            int i = (int)scaled;
            if (i >= m_Samples.Length - 1)
                return m_Samples[m_Samples.Length - 1];

            return MotionSample.Lerp(m_Samples[i], m_Samples[i + 1], scaled - i);
        }

        /// <summary>
        /// The pose the body holds before the snap. Written from the authored waypoint directly and never
        /// interpolated, so it is bit-exact with what the designer laid out — which is the whole reason a
        /// play can be rewound to its formation and look identical every time.
        /// </summary>
        public MotionSample SnapPose => m_Samples[0];

        /// <summary>The pose the body finishes in, used as the start of the next huddle blend.</summary>
        public MotionSample TerminalPose => m_Samples[m_Samples.Length - 1];

        /// <summary>
        /// A track that stands still at one pose for <paramref name="duration"/>. Used for a body with a
        /// single authored waypoint, and to pad a short route out to the play's derived length.
        /// </summary>
        public static MotionTrack Standing(Vector3 position, float headingDegrees, float duration, float sampleHz)
        {
            sampleHz = Mathf.Max(1f, sampleHz);
            duration = Mathf.Max(0f, duration);
            int count = Mathf.Max(2, Mathf.RoundToInt(duration * sampleHz) + 1);

            var samples = new MotionSample[count];
            for (int i = 0; i < count; i++)
                samples[i] = new MotionSample(i / sampleHz, position, headingDegrees, 0f, 0f, 0f);

            return new MotionTrack(samples, sampleHz);
        }
    }
}
