using System;
using UnityEngine;

namespace Stadium3D.Football
{
    /// <summary>
    /// What a designer actually authors: a point on the field in <b>yard coordinates</b>, so a route reads
    /// like a play diagram rather than a list of metres.
    /// </summary>
    /// <remarks>
    /// Yards here are relative to the ball, not absolute: <see cref="DepthYards"/> is measured downfield from
    /// the line of scrimmage and <see cref="LateralYards"/> across from the ball. That is how a play is drawn
    /// and talked about ("twelve-yard dig from the slot"), and it means the same play asset can be run from
    /// any spot on the field — <see cref="RouteBaker"/> is handed the ball's placement and resolves the
    /// alignment against it.
    /// </remarks>
    [Serializable]
    public class RouteWaypoint
    {
        [Tooltip("Downfield from the line of scrimmage, in yards, in the offense's direction of travel. " +
                 "Negative is behind the LOS — a quarterback's drop, a running back's alignment.")]
        public float DepthYards;

        [Tooltip("Across the field from the ball, in yards. Positive toward the offense's RIGHT — the same " +
                 "convention a play diagram uses, so authored numbers can be read straight off one.")]
        public float LateralYards;

        [Tooltip("What this point means to the speed solve. Through = ordinary, Cut = a deliberate break, " +
                 "Dwell = stand here, Stop = end of the route.")]
        public WaypointKind Kind = WaypointKind.Through;

        [Range(0.05f, 1f)]
        [Tooltip("Ceiling on speed through this point, as a fraction of the body's top speed. 1 = no extra " +
                 "limit beyond what the curvature already imposes. Lower it to make a break read sharper.")]
        public float SpeedFraction = 1f;

        [Min(0f)]
        [Tooltip("Seconds to stand still here. Only meaningful for Kind = Dwell — a lineman holding his " +
                 "stance, a receiver settling into a zone.")]
        public float DwellSeconds;

        [Tooltip("Hold an authored facing through this point instead of facing along the path. This is how " +
                 "a defender backpedals and how a blocker stays square.")]
        public bool PinFacing;

        [Tooltip("The facing to hold when PinFacing is set, in degrees about Y in pitch-local space.")]
        public float FacingDegrees;

        public RouteWaypoint() { }

        public RouteWaypoint(float depthYards, float lateralYards, WaypointKind kind = WaypointKind.Through)
        {
            DepthYards = depthYards;
            LateralYards = lateralYards;
            Kind = kind;
        }

        /// <summary>
        /// This waypoint as a pitch-local point, given where the ball is spotted and which way the offense is
        /// driving. The single place route-relative yards become field coordinates.
        /// </summary>
        public Vector3 ToPitchLocal(float lineOfScrimmageYard, float ballLateralYards, TeamSide offense)
        {
            float direction = FieldGeometry.DirectionFor(offense);
            return FieldGeometry.Spot(
                lineOfScrimmageYard + DepthYards * direction,
                ballLateralYards + LateralYards * direction);
        }

        /// <summary>A copy, so a baked play can never be mutated through the asset it was built from.</summary>
        public RouteWaypoint Clone() => new RouteWaypoint
        {
            DepthYards = DepthYards,
            LateralYards = LateralYards,
            Kind = Kind,
            SpeedFraction = SpeedFraction,
            DwellSeconds = DwellSeconds,
            PinFacing = PinFacing,
            FacingDegrees = FacingDegrees,
        };
    }
}
