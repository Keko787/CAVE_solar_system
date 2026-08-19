using UnityEngine;

namespace Stadium3D.Football
{
    /// <summary>The ball as the renderer and the front wall see it.</summary>
    public readonly struct BallState
    {
        public readonly BallCarriage Carriage;

        /// <summary>Roster id of the carrier, or 0 when nobody has it.</summary>
        public readonly int CarrierId;

        /// <summary>Pitch-local position, including height.</summary>
        public readonly Vector3 Position;

        /// <summary>Velocity, m/s — for a spin rate and a trail.</summary>
        public readonly Vector3 Velocity;

        public BallState(BallCarriage carriage, int carrierId, Vector3 position, Vector3 velocity)
        {
            Carriage = carriage;
            CarrierId = carrierId;
            Position = position;
            Velocity = velocity;
        }

        /// <summary>Absolute yard of the ball, for the ball-spot readout and the first-down line.</summary>
        public float YardLine => FieldGeometry.YardAt(Position);
    }

    /// <summary>
    /// Derives <see cref="BallState"/> from a play's authored <see cref="BallEvent"/> list as a
    /// <b>pure function of time</b>. The ball is never integrated.
    /// </summary>
    /// <remarks>
    /// That is not a shortcut, it is the point. A thrown ball is placed on an arc whose endpoints are the
    /// passer's hand at the authored release and the receiver's hands at the authored catch, so it
    /// <em>provably</em> arrives on time and in the right place. A gravity-integrated ball only lands
    /// correctly once someone has tuned the launch velocity until it happens to, and it stops landing
    /// correctly the moment the route underneath it changes.
    ///
    /// <para>Placing the release point requires sampling the passer at a time in the past relative to the
    /// frame being drawn. Only a seekable <see cref="MotionTrack"/> can answer that.</para>
    /// </remarks>
    public static class BallSolver
    {
        /// <summary>Where the ball rides relative to a carrier: forward of the chest, at carry height.</summary>
        static readonly Vector3 HandOffset = new Vector3(0f, 1.0f, 0.25f);

        /// <summary>Release point offset from the passer.</summary>
        static readonly Vector3 ReleaseOffset = new Vector3(0f, 1.85f, 0.3f);

        /// <summary>Where a caught ball sits relative to the receiver at the catch.</summary>
        static readonly Vector3 CatchOffset = new Vector3(0f, 1.5f, 0.2f);

        const float Eps = 1e-5f;

        /// <summary>The ball at <paramref name="time"/> seconds after the snap.</summary>
        public static BallState Sample(PlayScript play, IPlayerTrackLookup tracks, float time)
        {
            if (play == null || play.BallEvents == null || play.BallEvents.Count == 0)
                return new BallState(BallCarriage.Dead, 0, GroundSpot(play), Vector3.zero);

            FindBracket(play, time, out var current, out var next);

            if (current == null)
                return new BallState(BallCarriage.Dead, 0, GroundSpot(play), Vector3.zero);

            switch (current.Kind)
            {
                case BallEventKind.Snap:
                case BallEventKind.Handoff:
                case BallEventKind.Catch:
                    return Held(current.ToPlayerId, tracks, time);

                case BallEventKind.Throw:
                    return InFlight(play, tracks, current, next, time);

                case BallEventKind.Incomplete:
                {
                    Vector3 ground = FieldGeometry.Spot(current.TargetYardLine, current.TargetLateralYards);
                    float since = time - current.Time;
                    bool dead = since >= Mathf.Max(0.01f, current.DeadAfterSeconds);
                    // A short decaying slide so the ball settles instead of stopping dead on contact.
                    float slide = dead ? 1f : Mathf.SmoothStep(0f, 1f, since / Mathf.Max(0.01f, current.DeadAfterSeconds));
                    Vector3 pos = ground + Vector3.up * 0.12f + Vector3.right * (0.6f * slide);
                    return new BallState(dead ? BallCarriage.Dead : BallCarriage.Loose, 0, pos, Vector3.zero);
                }

                case BallEventKind.Down:
                case BallEventKind.CrossGoalLine:
                default:
                {
                    // Frozen where the carrier was when the whistle went.
                    var s = tracks != null ? tracks.SampleOrDefault(current.FromPlayerId, current.Time) : default;
                    Vector3 pos = s.Position + s.Rotation * HandOffset;
                    return new BallState(BallCarriage.Dead, current.FromPlayerId, pos, Vector3.zero);
                }
            }
        }

        /// <summary>
        /// The absolute yard the ball is on at a time. The floor view's ball marker and the in-play spot
        /// readout both come from here, so they cannot disagree.
        /// </summary>
        public static float YardAt(PlayScript play, IPlayerTrackLookup tracks, float time) =>
            Sample(play, tracks, time).YardLine;

        static BallState Held(int carrierId, IPlayerTrackLookup tracks, float time)
        {
            if (tracks == null)
                return new BallState(BallCarriage.Held, carrierId, Vector3.zero, Vector3.zero);

            var s = tracks.SampleOrDefault(carrierId, time);
            Vector3 pos = s.Position + s.Rotation * HandOffset;
            Vector3 vel = s.Rotation * Vector3.forward * s.SpeedMps;
            return new BallState(BallCarriage.Held, carrierId, pos, vel);
        }

        static BallState InFlight(PlayScript play, IPlayerTrackLookup tracks,
                                  BallEvent throwEvent, BallEvent resolution, float time)
        {
            if (resolution == null)
                return Held(throwEvent.FromPlayerId, tracks, time);

            // The passer AT THE RELEASE INSTANT — in the past relative to the frame being drawn.
            var passer = tracks != null ? tracks.SampleOrDefault(throwEvent.FromPlayerId, throwEvent.Time) : default;
            Vector3 p0 = passer.Position + passer.Rotation * ReleaseOffset;

            Vector3 p1;
            if (resolution.Kind == BallEventKind.Catch)
            {
                var receiver = tracks != null ? tracks.SampleOrDefault(resolution.ToPlayerId, resolution.Time) : default;
                p1 = receiver.Position + receiver.Rotation * CatchOffset;
            }
            else
            {
                p1 = FieldGeometry.Spot(resolution.TargetYardLine, resolution.TargetLateralYards) + Vector3.up * 0.9f;
            }

            float span = Mathf.Max(resolution.Time - throwEvent.Time, Eps);
            float s = Mathf.Clamp01((time - throwEvent.Time) / span);
            float apex = Mathf.Max(0f, throwEvent.ApexMeters);

            Vector3 pos = Vector3.Lerp(p0, p1, s) + Vector3.up * (4f * apex * s * (1f - s));

            // Analytic derivative, so the trail and spin match the arc exactly.
            Vector3 chord = (p1 - p0) / span;
            float dHeight = 4f * apex * (1f - 2f * s) / span;
            Vector3 vel = chord + Vector3.up * dHeight;

            return new BallState(BallCarriage.InFlight, 0, pos, vel);
        }

        /// <summary>The last event at or before <paramref name="time"/>, and the one after it.</summary>
        static void FindBracket(PlayScript play, float time, out BallEvent current, out BallEvent next)
        {
            current = null;
            next = null;

            var events = play.BallEvents;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e == null) continue;

                if (e.Time <= time)
                {
                    current = e;
                }
                else
                {
                    next = e;
                    break;
                }
            }

            // Before the snap the ball is with whoever takes it.
            if (current == null && events.Count > 0)
            {
                next = events[0];
                current = events[0];
            }
        }

        static Vector3 GroundSpot(PlayScript play) => play == null
            ? Vector3.zero
            : FieldGeometry.Spot(play.LineOfScrimmageYard, play.BallLateralYards) + Vector3.up * 0.12f;
    }
}
