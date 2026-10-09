using System;
using UnityEngine;

namespace Stadium3D.Space
{
    /// <summary>
    /// One body's place in the CAVE-sized solar system: how big it is, how far out it sits, and how long it takes
    /// to come back round.
    /// </summary>
    /// <remarks>
    /// <see cref="DiameterMeters"/> is a diameter rather than a radius on purpose. Unity's sphere primitive is one
    /// unit across at scale 1, so the number drops straight into <c>localScale</c> with no arithmetic in between —
    /// which means the table below can be read against a tape measure in the actual room.
    /// </remarks>
    public readonly struct BodySpec
    {
        /// <summary>GameObject name the layout tool looks for, case-insensitively.</summary>
        public readonly string Name;

        /// <summary>Rendered diameter in metres. Goes straight into a uniform <c>localScale</c>.</summary>
        public readonly float DiameterMeters;

        /// <summary>Distance from whatever this body orbits, in metres. Zero for the Sun.</summary>
        public readonly float OrbitRadiusMeters;

        /// <summary>Real sidereal period in Earth days. Only ratios of this are used, never the absolute value.</summary>
        public readonly float SiderealPeriodDays;

        /// <summary>The body this one goes round, or <c>null</c> for the Sun, which sits at the centre.</summary>
        public readonly string Orbits;

        public BodySpec(string name, float diameterMeters, float orbitRadiusMeters,
                        float siderealPeriodDays, string orbits)
        {
            Name = name;
            DiameterMeters = diameterMeters;
            OrbitRadiusMeters = orbitRadiusMeters;
            SiderealPeriodDays = siderealPeriodDays;
            Orbits = orbits;
        }

        /// <summary>True for the one body that does not orbit anything — the Sun.</summary>
        public bool IsCentre => string.IsNullOrEmpty(Orbits);
    }

    /// <summary>
    /// The solar system, normalised to fit inside this specific CAVE. Sizes and orbit radii are fixed data rather
    /// than something to eyeball in the editor, for the same reason <c>WallFraming</c> exists: the room is a
    /// measured object, so "will this fit, and will it read" is a solvable question.
    /// </summary>
    /// <remarks>
    /// <para><b>Sizes.</b> True relative diameters put Jupiter 58x wider than Pluto — anything that makes Jupiter
    /// impressive makes Pluto a speck two or three pixels across. So diameters follow a square-root law on the
    /// real diameter, which keeps the ordering and the sense of "the gas giants are the big ones" while
    /// compressing the range to 8:1. Everything here lands between 1.2 cm and 28 cm.</para>
    ///
    /// <para><b>Distances.</b> Logarithmic in semi-major axis. Linear distances would put Mercury at 1% of the
    /// available radius and pile all four terrestrials on top of the Sun. The log law spreads 0.387–39.48 AU
    /// across 0.30–0.95 m.</para>
    ///
    /// <para><b>The constraint that actually set the numbers</b> is not the room — it is orbit intersection.
    /// Adjacent orbits are as little as 3.8 cm apart (Neptune to Pluto) and 9.5 cm apart (Jupiter to Saturn), and
    /// the sum of two neighbours' radii has to fit inside that gap or the spheres visibly cross each other's
    /// paths. Jupiter and Saturn bind hardest, and they are the reason Earth is 2.7 cm rather than 5 cm.
    /// <c>SolarSystemLayoutTests</c> checks every adjacent pair, so changing one diameter without re-deriving the
    /// rest will fail the suite rather than quietly producing a tangle.</para>
    ///
    /// <para><b>Saturn's rings are not modelled here.</b> The A-ring reaches roughly 2.35 Saturn radii, which is
    /// 9.5 cm at this scale and would cut straight through Jupiter's and Uranus' orbits. Multiply every diameter
    /// by 0.61 if a ring disc goes in.</para>
    /// </remarks>
    public static class SolarSystemLayout
    {
        /// <summary>Interior width of the CAVE, wall to wall, metres. Agrees with <c>WallFraming.WideWallWidthMeters</c>.</summary>
        public const float RoomWidthMeters = 3.556f;

        /// <summary>Floor to ceiling, metres. Agrees with <c>WallFraming.WallHeightMeters</c>.</summary>
        public const float RoomHeightMeters = 2.2225f;

        /// <summary>
        /// Front wall to back wall, metres, measured off CAVE_Room.prefab: those two surfaces sit at z = ±1.1113.
        /// </summary>
        /// <remarks>
        /// Deliberately not <c>WallFraming.SideWallWidthMeters</c> (2.3876) and not the side walls' own 2.7 scale.
        /// The prefab disagrees with the constants and with itself. This is the enclosed volume, which is the one
        /// a planet can actually pass through, and it is the smallest of the three, so it is also the safe choice.
        /// </remarks>
        public const float RoomDepthMeters = 2.2226f;

        /// <summary>Height of the system's centre above the floor: the walls' own centre line, so about eye height.</summary>
        public const float CentreHeightMeters = 1.1113f;

        /// <summary>Where the Sun goes, in CAVE_Room's local space.</summary>
        public static Vector3 Centre => new Vector3(0f, CentreHeightMeters, 0f);

        /// <summary>Earth's sidereal period. The denominator for every displayed period.</summary>
        public const float EarthSiderealDays = 365.256f;

        /// <summary>Seconds one Earth orbit takes on screen, before <see cref="DefaultCompression"/> is applied.</summary>
        public const float DefaultEarthPeriodSeconds = 12f;

        /// <summary>
        /// Exponent applied to the period ratio. 1 gives true relative periods, where Pluto takes 248 Earth orbits
        /// and nobody in the room ever sees it move. 0.25 compresses the real 1030:1 range to 5.7:1, so the whole
        /// system is legibly in motion at once and the inner planets are still visibly the quick ones.
        /// </summary>
        public const float DefaultCompression = 0.25f;

        /// <summary>Golden angle, used to spread starting positions so the bodies never begin in a row.</summary>
        public const float GoldenAngleDegrees = 137.50776f;

        /// <summary>
        /// Preset A: every body orbiting at once, sized so no two orbits intersect and the outermost clears the
        /// walls. Ordered inside-out, and that order doubles as the phase index.
        /// </summary>
        public static readonly BodySpec[] Bodies =
        {
            //           name        diameter  orbit    period (days)  orbits
            new BodySpec("Sun",      0.281f,   0f,           0f,       null),
            new BodySpec("Mercury",  0.017f,   0.300f,      87.969f,   "Sun"),
            new BodySpec("Venus",    0.026f,   0.388f,     224.701f,   "Sun"),
            new BodySpec("Earth",    0.027f,   0.433f,     365.256f,   "Sun"),
            new BodySpec("Moon",     0.014f,   0.023f,      27.322f,   "Earth"),
            new BodySpec("Mars",     0.020f,   0.493f,     686.980f,   "Sun"),
            new BodySpec("Jupiter",  0.089f,   0.660f,    4332.589f,   "Sun"),
            new BodySpec("Saturn",   0.081f,   0.755f,   10759.220f,   "Sun"),
            new BodySpec("Uranus",   0.054f,   0.849f,   30685.400f,   "Sun"),
            new BodySpec("Neptune",  0.053f,   0.912f,   60189.000f,   "Sun"),
            new BodySpec("Pluto",    0.012f,   0.950f,   90560.000f,   "Sun"),
        };

        /// <summary>Finds a body by name, case-insensitively. Returns false rather than throwing.</summary>
        public static bool TryGet(string name, out BodySpec spec)
        {
            foreach (var body in Bodies)
            {
                if (string.Equals(body.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    spec = body;
                    return true;
                }
            }

            spec = default;
            return false;
        }

        /// <summary>How many seconds one orbit should take on screen, given the real period.</summary>
        /// <param name="siderealDays">The body's real period. Non-positive means "does not orbit".</param>
        /// <param name="earthPeriodSeconds">Seconds for one Earth orbit.</param>
        /// <param name="compression">See <see cref="DefaultCompression"/>. Clamped to 0..1.</param>
        public static float DisplayPeriodSeconds(float siderealDays,
                                                 float earthPeriodSeconds = DefaultEarthPeriodSeconds,
                                                 float compression = DefaultCompression)
        {
            if (siderealDays <= 0f || earthPeriodSeconds <= 0f)
                return 0f;

            return earthPeriodSeconds * Mathf.Pow(siderealDays / EarthSiderealDays, Mathf.Clamp01(compression));
        }

        /// <summary>Starting angle for the body at <paramref name="index"/> in <see cref="Bodies"/>.</summary>
        public static float PhaseDegrees(int index) => Mathf.Repeat(index * GoldenAngleDegrees, 360f);

        /// <summary>
        /// Distance from the Sun to the far edge of the outermost body — the figure that has to clear the walls.
        /// </summary>
        public static float OuterExtentMeters
        {
            get
            {
                float extent = 0f;
                foreach (var body in Bodies)
                {
                    if (body.IsCentre)
                        continue;

                    // A moon rides on its planet, so its reach is measured out from the planet's own orbit.
                    float hostRadius = body.Orbits == "Sun" || !TryGet(body.Orbits, out var host)
                        ? 0f
                        : host.OrbitRadiusMeters;

                    extent = Mathf.Max(extent, hostRadius + body.OrbitRadiusMeters + body.DiameterMeters * 0.5f);
                }

                return extent;
            }
        }
    }
}
