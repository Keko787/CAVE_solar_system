using System;
using UnityEngine;

namespace Stadium3D.Football
{
    /// <summary>
    /// The physical envelope a baked route has to respect. These are the knobs that stop authored waypoints
    /// from producing motion nobody believes — a body at full sprint on the first frame after the snap, a
    /// facing that snaps ninety degrees in one frame, a receiver rounding a hard break without losing a step.
    /// </summary>
    /// <remarks>
    /// Every limit is a <em>cap</em>, never a target. <see cref="RouteBaker"/> solves the fastest motion that
    /// satisfies all of them, so raising a limit can only ever make a route faster and lowering it can only
    /// make it slower — a route can never become invalid by tuning these.
    /// </remarks>
    [Serializable]
    public struct MotionLimits
    {
        [Tooltip("Top speed, metres per second. An NFL receiver peaks around 10.2 m/s (22.8 mph); a lineman " +
                 "around 6.7 m/s.")]
        public float MaxSpeedMps;

        [Tooltip("Acceleration from a standstill, m/s². Roughly 6 for a skill player, 4 for a lineman — so " +
                 "a receiver needs about 1.7 s to reach top speed, which is why nobody is at full speed at " +
                 "the snap.")]
        public float AccelMps2;

        [Tooltip("Braking, m/s². Higher than acceleration; a body stops faster than it starts.")]
        public float DecelMps2;

        [Tooltip("Lateral grip, m/s² — the friction circle. Caps cornering speed at sqrt(grip / curvature), " +
                 "so the tighter the authored break, the slower the body comes through it.")]
        public float LateralGripMps2;

        [Tooltip("How fast the body can rotate, degrees per second. Applied to the facing after the speed " +
                 "solve, so a cut turns over several frames instead of snapping.")]
        public float MaxTurnRateDegPerSec;

        /// <summary>A skill player — receiver, back, defensive back.</summary>
        public static MotionLimits Skill => new MotionLimits
        {
            MaxSpeedMps = 10.2f,
            AccelMps2 = 6.0f,
            DecelMps2 = 8.0f,
            LateralGripMps2 = 12.0f,
            MaxTurnRateDegPerSec = 540f,
        };

        /// <summary>A quarterback: drops and slides rather than sprints.</summary>
        public static MotionLimits Quarterback => new MotionLimits
        {
            MaxSpeedMps = 8.5f,
            AccelMps2 = 5.0f,
            DecelMps2 = 7.0f,
            LateralGripMps2 = 10.0f,
            MaxTurnRateDegPerSec = 480f,
        };

        /// <summary>A lineman, either side of the ball.</summary>
        public static MotionLimits Lineman => new MotionLimits
        {
            MaxSpeedMps = 6.7f,
            AccelMps2 = 4.0f,
            DecelMps2 = 6.0f,
            LateralGripMps2 = 7.0f,
            MaxTurnRateDegPerSec = 360f,
        };

        /// <summary>A linebacker or tight end — between skill and line.</summary>
        public static MotionLimits Hybrid => new MotionLimits
        {
            MaxSpeedMps = 8.8f,
            AccelMps2 = 5.2f,
            DecelMps2 = 7.2f,
            LateralGripMps2 = 9.5f,
            MaxTurnRateDegPerSec = 450f,
        };

        /// <summary>The default envelope for a position, so a roster does not have to spell out all five numbers.</summary>
        public static MotionLimits For(PlayerPosition position)
        {
            switch (position)
            {
                case PlayerPosition.Quarterback:    return Quarterback;
                case PlayerPosition.OffensiveLine:
                case PlayerPosition.DefensiveLine:  return Lineman;
                case PlayerPosition.TightEnd:
                case PlayerPosition.Linebacker:     return Hybrid;
                default:                            return Skill;
            }
        }

        /// <summary>
        /// A copy with every limit floored above zero. The baker divides by several of these, and a
        /// default-constructed <see cref="MotionLimits"/> (all zeroes, which is what an un-filled Inspector
        /// field gives you) would otherwise produce infinities rather than an obvious failure.
        /// </summary>
        public MotionLimits Sanitized() => new MotionLimits
        {
            MaxSpeedMps = Mathf.Max(0.1f, MaxSpeedMps),
            AccelMps2 = Mathf.Max(0.1f, AccelMps2),
            DecelMps2 = Mathf.Max(0.1f, DecelMps2),
            LateralGripMps2 = Mathf.Max(0.1f, LateralGripMps2),
            MaxTurnRateDegPerSec = Mathf.Max(1f, MaxTurnRateDegPerSec),
        };
    }
}
