using System.Collections.Generic;
using UnityEngine;

namespace Stadium3D.Football
{
    /// <summary>
    /// Samples any player's motion at any time — not just the current frame. <see cref="BallSolver"/> needs
    /// this to place a throw's release point at the passer's pose <em>in the past</em>, which an integrating
    /// simulation simply cannot answer.
    /// </summary>
    public interface IPlayerTrackLookup
    {
        /// <summary>The motion for a player at a time, or a zeroed sample if the id is not on this play.</summary>
        MotionSample SampleOrDefault(int playerId, float time);
    }

    /// <summary>
    /// A <see cref="PlayScript"/> with every route baked. Owns the play's real duration and is the only thing
    /// the runtime samples.
    /// </summary>
    public sealed class BakedPlay : IPlayerTrackLookup
    {
        readonly Dictionary<int, MotionTrack> m_Tracks;
        readonly int[] m_PlayerIds;

        /// <summary>The authored play this was built from.</summary>
        public PlayScript Script { get; }

        /// <summary>
        /// The play's length in seconds. <b>Derived, never authored</b> — the longest of every participant's
        /// natural route duration, the last ball event plus its tail, and
        /// <see cref="PlayScript.MinDurationSeconds"/>.
        /// </summary>
        /// <remarks>
        /// Deriving it removes a whole class of content bug: a designer cannot type "3 s" for a forty-metre
        /// route and get a receiver sprinting at 13 m/s. The cost is that a play cannot be pinned to an exact
        /// length — only floored — so anything that needs hard timing has to shorten routes or raise limits.
        /// </remarks>
        public float Duration { get; }

        /// <summary>Every roster id with a track on this play, in authored order.</summary>
        public IReadOnlyList<int> PlayerIds => m_PlayerIds;

        /// <summary>Warnings raised while baking, for the authoring pass.</summary>
        public BakeReport Report { get; }

        BakedPlay(PlayScript script, Dictionary<int, MotionTrack> tracks, int[] ids, float duration, BakeReport report)
        {
            Script = script;
            m_Tracks = tracks;
            m_PlayerIds = ids;
            Duration = duration;
            Report = report;
        }

        /// <summary>
        /// Bake every route on a play. Runs the solve twice: once to discover how long the play naturally is,
        /// then again with that as the floor so every body's track covers the whole play and the ones that
        /// finish early hold a terminal dwell rather than ending mid-air.
        /// </summary>
        public static BakedPlay Bake(PlayScript play, Roster roster, float sampleHz, BakeReport report = null)
        {
            report = report ?? new BakeReport();
            sampleHz = Mathf.Max(1f, sampleHz);

            float ballTail = 0f;
            for (int i = 0; i < play.BallEvents.Count; i++)
            {
                var e = play.BallEvents[i];
                if (e != null) ballTail = Mathf.Max(ballTail, e.Time + Mathf.Max(0f, e.DeadAfterSeconds));
            }

            // Pass 1 — natural durations.
            float natural = 0f;
            for (int i = 0; i < play.Players.Count; i++)
            {
                var ps = play.Players[i];
                if (ps == null) continue;
                var track = BakeOne(play, ps, roster, 0f, sampleHz, null);
                natural = Mathf.Max(natural, track.Duration);
            }

            float duration = Mathf.Max(Mathf.Max(natural, ballTail), Mathf.Max(0f, play.MinDurationSeconds));

            // Pass 2 — every track padded out to the play.
            var tracks = new Dictionary<int, MotionTrack>(play.Players.Count);
            var ids = new List<int>(play.Players.Count);

            for (int i = 0; i < play.Players.Count; i++)
            {
                var ps = play.Players[i];
                if (ps == null || tracks.ContainsKey(ps.PlayerId)) continue;
                tracks.Add(ps.PlayerId, BakeOne(play, ps, roster, duration, sampleHz, report));
                ids.Add(ps.PlayerId);
            }

            return new BakedPlay(play, tracks, ids.ToArray(), duration, report);
        }

        static MotionTrack BakeOne(PlayScript play, PlayerScript ps, Roster roster,
                                   float minDuration, float sampleHz, BakeReport report)
        {
            var identity = roster?.Get(ps.PlayerId);
            var limits = ps.OverrideLimits
                ? ps.Limits
                : (identity != null ? identity.EffectiveLimits : MotionLimits.Skill);

            var points = new List<Vector3>(ps.Waypoints.Count);
            for (int i = 0; i < ps.Waypoints.Count; i++)
            {
                var wp = ps.Waypoints[i];
                if (wp == null) continue;
                points.Add(wp.ToPitchLocal(play.LineOfScrimmageYard, play.BallLateralYards, play.Offense));
            }

            if (points.Count == 0)
            {
                // No route authored: stand on the ball spot rather than at the pitch origin, which would put
                // the body on the 50 and be very obviously wrong on the floor view.
                var spot = FieldGeometry.Spot(play.LineOfScrimmageYard, play.BallLateralYards);
                return MotionTrack.Standing(spot, ps.InitialFacingDegrees, minDuration, sampleHz);
            }

            return RouteBaker.Bake(points, ps.Waypoints, limits, ps.InitialFacingDegrees,
                                   minDuration, sampleHz, report,
                                   $"{play.Name} #{ps.Number}");
        }

        /// <summary>The motion for a player at a time. Clamped at both ends; never wraps.</summary>
        public MotionSample Sample(int playerId, float time) =>
            m_Tracks.TryGetValue(playerId, out var track) ? track.Sample(time) : default;

        /// <inheritdoc/>
        public MotionSample SampleOrDefault(int playerId, float time) => Sample(playerId, time);

        /// <summary>True if this play has a track for the id.</summary>
        public bool Has(int playerId) => m_Tracks.ContainsKey(playerId);

        /// <summary>
        /// The exact pre-snap pose for a player. Bit-exact with the authored alignment — no interpolation, no
        /// tolerance — which is what makes a replayed or looped play line up identically every time.
        /// </summary>
        public MotionSample PoseAtSnap(int playerId) =>
            m_Tracks.TryGetValue(playerId, out var track) ? track.SnapPose : default;

        /// <summary>The pose a player finishes in, used as the start of the next huddle blend.</summary>
        public MotionSample TerminalPose(int playerId) =>
            m_Tracks.TryGetValue(playerId, out var track) ? track.TerminalPose : default;

        /// <summary>Total baked samples across every track — the memory budget an EditMode test asserts on.</summary>
        public int TotalSampleCount
        {
            get
            {
                int n = 0;
                foreach (var kv in m_Tracks) n += kv.Value.Count;
                return n;
            }
        }
    }
}
