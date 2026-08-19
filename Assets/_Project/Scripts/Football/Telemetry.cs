using System.Collections.Generic;
using UnityEngine;

namespace Stadium3D.Football
{
    /// <summary>One player's derived state for the current frame — identity joined to the sampled motion.</summary>
    public readonly struct PlayerFrame
    {
        public readonly int PlayerId;
        public readonly TeamSide Side;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly float SpeedMps;
        public readonly float PeakSpeedMps;
        public readonly float DistanceMeters;
        public readonly bool HasBall;

        public PlayerFrame(int playerId, TeamSide side, Vector3 position, Quaternion rotation,
                           float speedMps, float peakSpeedMps, float distanceMeters, bool hasBall)
        {
            PlayerId = playerId;
            Side = side;
            Position = position;
            Rotation = rotation;
            SpeedMps = speedMps;
            PeakSpeedMps = peakSpeedMps;
            DistanceMeters = distanceMeters;
            HasBall = hasBall;
        }
    }

    /// <summary>
    /// The team walls' live readout for the focused player. Every value is stored in the unit it is
    /// <em>displayed</em> in — mph and yards — so no panel ever performs a conversion.
    /// </summary>
    /// <remarks>
    /// That is a deliberate guard, not a nicety. The source project's feed window carried its own implicit
    /// ratio and shipped a 1.69× stretch that looked almost right. A single <c>meters / 0.9144</c> written
    /// inline in a UI script here would reintroduce exactly that, so the conversion happens once, in
    /// <see cref="FieldGeometry"/>, and the view receives finished numbers.
    /// </remarks>
    public readonly struct LiveTelemetry
    {
        public readonly int PlayerId;

        /// <summary>Current speed, mph.</summary>
        public readonly float SpeedMph;

        /// <summary>Fastest this player has moved on this play, mph. Resets at every snap.</summary>
        public readonly float TopSpeedMph;

        /// <summary>Ground covered this play, yards.</summary>
        public readonly float DistanceYards;

        /// <summary>Distance to the nearest <b>opponent</b>, yards. Negative when there is nobody to measure against.</summary>
        public readonly float SeparationYards;

        /// <summary>Roster id of that nearest opponent, or 0.</summary>
        public readonly int NearestOpponentId;

        /// <summary>The route or assignment label from the play script.</summary>
        public readonly string RouteName;

        public LiveTelemetry(int playerId, float speedMph, float topSpeedMph, float distanceYards,
                             float separationYards, int nearestOpponentId, string routeName)
        {
            PlayerId = playerId;
            SpeedMph = speedMph;
            TopSpeedMph = topSpeedMph;
            DistanceYards = distanceYards;
            SeparationYards = separationYards;
            NearestOpponentId = nearestOpponentId;
            RouteName = routeName;
        }
    }

    /// <summary>Builds <see cref="LiveTelemetry"/> from the frame the sim just produced.</summary>
    public static class TelemetryBuilder
    {
        /// <summary>
        /// Telemetry for one player, given every player's frame this tick.
        /// </summary>
        /// <remarks>
        /// Separation deliberately scans <b>opponents only</b>. A nearest-body scan would report the guard
        /// standing next to a receiver rather than the corner covering him, which is the wrong number and a
        /// very easy one to ship without noticing.
        /// </remarks>
        public static LiveTelemetry Build(int playerId, IReadOnlyList<PlayerFrame> frames, string routeName)
        {
            PlayerFrame self = default;
            bool found = false;

            for (int i = 0; i < frames.Count; i++)
            {
                if (frames[i].PlayerId != playerId) continue;
                self = frames[i];
                found = true;
                break;
            }

            if (!found)
                return new LiveTelemetry(playerId, 0f, 0f, 0f, -1f, 0, routeName);

            float bestSqr = float.MaxValue;
            int nearest = 0;

            for (int i = 0; i < frames.Count; i++)
            {
                var other = frames[i];
                if (other.Side == self.Side) continue;              // opponents only

                float sqr = (other.Position - self.Position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearest = other.PlayerId;
                }
            }

            float separation = nearest == 0
                ? -1f
                : FieldGeometry.MetersToYards(Mathf.Sqrt(bestSqr));

            return new LiveTelemetry(
                playerId,
                FieldGeometry.MpsToMph(self.SpeedMps),
                FieldGeometry.MpsToMph(self.PeakSpeedMps),
                FieldGeometry.MetersToYards(self.DistanceMeters),
                separation,
                nearest,
                routeName);
        }
    }
}
