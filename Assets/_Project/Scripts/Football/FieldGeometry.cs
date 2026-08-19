using UnityEngine;

namespace Stadium3D.Football
{
    /// <summary>
    /// The single unit boundary of the football sim. Everything inside the sim runs in <b>metres</b>,
    /// because every existing type it has to meet does — <c>CaveRegion.Size</c>, camera extents, Unity
    /// transforms. Everything a viewer reads is in <b>yards</b> (and speed in mph). This class owns every
    /// conversion between the three, and it is the only place any of them may happen.
    /// </summary>
    /// <remarks>
    /// That rule is not stylistic. The known <c>DroneFeedWindow</c> stretch bug in the source project was
    /// exactly this shape — a view carrying its own implicit ratio — and it looked almost right. A single
    /// <c>pos.z / 0.9144</c> written inline in a panel script reintroduces it here, so stat types store the
    /// yard value and no view ever converts.
    ///
    /// <para><b>Coordinates.</b> The pitch is authored in its own local frame with the 50-yard line at the
    /// origin: +X runs along the length of the field toward the away goal, +Z runs across it toward the
    /// left sideline (as seen from the home end), Y is up with the turf at 0.</para>
    ///
    /// <para><b>Yard lines.</b> Football numbers yard lines 0→50→0 from both ends, which is ambiguous as a
    /// coordinate. The sim uses an <em>absolute</em> yard instead: 0 at the home goal line, 50 at midfield,
    /// 100 at the away goal line, with end zones running to −10 and +110. <see cref="MarkedYardLine"/>
    /// converts back to the number painted on the grass when a readout needs it.</para>
    /// </remarks>
    public static class FieldGeometry
    {
        /// <summary>Metres in one yard. Exact by definition.</summary>
        public const float MetersPerYard = 0.9144f;

        /// <summary>Miles per hour in one metre per second. 3600 / 1609.344.</summary>
        public const float MphPerMps = 2.2369362920544f;

        /// <summary>Playing field between the goal lines, in yards.</summary>
        public const float FieldLengthYards = 100f;

        /// <summary>One end zone's depth, in yards.</summary>
        public const float EndZoneDepthYards = 10f;

        /// <summary>Sideline to sideline, in yards. 53 1/3.</summary>
        public const float FieldWidthYards = 160f / 3f;

        /// <summary>Centre of the field to a sideline, in yards.</summary>
        public const float HalfWidthYards = FieldWidthYards * 0.5f;

        /// <summary>
        /// Lateral distance from the centre of the field to a hash mark, in yards. NFL hashes are 70 ft 9 in
        /// from each sideline, which leaves 18 ft 6 in between them — so 9 ft 3 in either side of centre.
        /// </summary>
        public const float HashLateralYards = 9.25f / 3f;   // 9 ft 3 in → 3.0833 yd

        /// <summary>Absolute yard of the home goal line.</summary>
        public const float HomeGoalLineYard = 0f;

        /// <summary>Absolute yard of the away goal line.</summary>
        public const float AwayGoalLineYard = 100f;

        /// <summary>Absolute yard at midfield.</summary>
        public const float MidfieldYard = 50f;

        // ── unit conversion ────────────────────────────────────────────────────────────────────────

        /// <summary>Yards → metres.</summary>
        public static float YardsToMeters(float yards) => yards * MetersPerYard;

        /// <summary>Metres → yards.</summary>
        public static float MetersToYards(float meters) => meters / MetersPerYard;

        /// <summary>Metres per second → miles per hour, for the live telemetry readout.</summary>
        public static float MpsToMph(float mps) => mps * MphPerMps;

        // ── field space ────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A point on the turf, from an absolute yard (0 = home goal, 100 = away goal) and a lateral offset
        /// in yards from the centre of the field (+ toward the left sideline). Y is always 0 — this is the
        /// ground plane; bodies and the ball add their own height.
        /// </summary>
        public static Vector3 Spot(float absoluteYard, float lateralYards) => new Vector3(
            YardsToMeters(absoluteYard - MidfieldYard),
            0f,
            YardsToMeters(lateralYards));

        /// <summary>Absolute yard of a world point in pitch-local space.</summary>
        public static float YardAt(Vector3 pitchLocal) => MetersToYards(pitchLocal.x) + MidfieldYard;

        /// <summary>Lateral offset in yards of a world point in pitch-local space.</summary>
        public static float LateralAt(Vector3 pitchLocal) => MetersToYards(pitchLocal.z);

        /// <summary>
        /// The number painted on the grass for an absolute yard — 0..50, counting down from both goal lines.
        /// A readout says "OWN 35" or "OPP 45"; this supplies the 35 and the 45.
        /// </summary>
        public static int MarkedYardLine(float absoluteYard)
        {
            float clamped = Mathf.Clamp(absoluteYard, HomeGoalLineYard, AwayGoalLineYard);
            return Mathf.RoundToInt(clamped <= MidfieldYard ? clamped : FieldLengthYards - clamped);
        }

        /// <summary>
        /// True while the point is inside the sidelines and between the two end lines (back of each end
        /// zone). Used by <see cref="PlayValidator"/> to reject an alignment that would put a body off the
        /// field, which is otherwise only visible once the demo is running in the CAVE.
        /// </summary>
        public static bool InBounds(Vector3 pitchLocal)
        {
            float yard = YardAt(pitchLocal);
            float lateral = LateralAt(pitchLocal);
            return yard >= HomeGoalLineYard - EndZoneDepthYards
                && yard <= AwayGoalLineYard + EndZoneDepthYards
                && Mathf.Abs(lateral) <= HalfWidthYards;
        }

        /// <summary>
        /// The absolute yard a team is driving toward. Home moves toward 100, away toward 0 — so "yards to
        /// the goal line" and "did this cross for a touchdown" both come from one place instead of a sign
        /// convention repeated at every call site.
        /// </summary>
        public static float GoalLineFor(TeamSide side) =>
            side == TeamSide.Home ? AwayGoalLineYard : HomeGoalLineYard;

        /// <summary>+1 when <paramref name="side"/> advances up the absolute yard axis, −1 when it goes down.</summary>
        public static float DirectionFor(TeamSide side) => side == TeamSide.Home ? 1f : -1f;

        /// <summary>
        /// Yards <paramref name="side"/> still has to travel from <paramref name="absoluteYard"/> to score.
        /// Never negative.
        /// </summary>
        public static float YardsToGoal(TeamSide side, float absoluteYard) =>
            Mathf.Max(0f, (GoalLineFor(side) - absoluteYard) * DirectionFor(side));
    }
}
