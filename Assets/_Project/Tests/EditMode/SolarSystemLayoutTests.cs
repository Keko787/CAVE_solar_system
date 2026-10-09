using System.Linq;
using NUnit.Framework;
using Stadium3D.Space;
using UnityEngine;

namespace Stadium3D.Space.Tests
{
    /// <summary>
    /// The claim <see cref="SolarSystemLayout"/> makes: nine bodies and a moon, all orbiting at once, inside a
    /// room 2.2226 m deep, with nothing clipping a wall and no two orbits crossing.
    /// </summary>
    /// <remarks>
    /// This is worth pinning because the binding constraint is invisible in the editor. The room is the obvious
    /// limit and it is not the one that set the numbers — orbit intersection is. The whole table has 5.5 mm of
    /// slack at its tightest point, between Neptune and Pluto, and 10 mm between Jupiter and Saturn. Nudging any
    /// one diameter up looks harmless in the inspector and quietly puts a gas giant through its neighbour's path
    /// several minutes into a session, at whatever moment the two happen to line up. So every adjacent pair is
    /// checked here, and the suite fails rather than the room looking subtly wrong to somebody standing in it.
    /// </remarks>
    public class SolarSystemLayoutTests
    {
        const float Tol = 1e-4f;

        static BodySpec[] SunOrbiters => SolarSystemLayout.Bodies
            .Where(b => b.Orbits == "Sun")
            .OrderBy(b => b.OrbitRadiusMeters)
            .ToArray();

        // ── fitting the room ───────────────────────────────────────────────────────────────────────

        [Test]
        public void OutermostBody_ClearsTheFrontAndBackWalls()
        {
            // Depth is the tight axis: 2.2226 m between the front and back surfaces, so 1.1113 m of radius.
            float halfDepth = SolarSystemLayout.RoomDepthMeters * 0.5f;

            Assert.That(SolarSystemLayout.OuterExtentMeters, Is.LessThan(halfDepth),
                "the far edge of the outermost body has to stay inside the room, not just its centre");
        }

        [Test]
        public void OutermostBody_ClearsTheSideWallsByMoreThanTheWallsItFaces()
        {
            // Width is the roomy axis. Asserted anyway so that widening an orbit to fix a depth problem cannot
            // silently break the other one.
            float halfWidth = SolarSystemLayout.RoomWidthMeters * 0.5f;

            Assert.That(SolarSystemLayout.OuterExtentMeters, Is.LessThan(halfWidth));
        }

        [Test]
        public void TheWholeSystem_SitsBelowTheCeilingAndAboveTheFloor()
        {
            // The orbital plane is flat and at eye height, so only the Sun's own bulk has any vertical reach.
            Assert.That(SolarSystemLayout.TryGet("Sun", out BodySpec sun), Is.True);

            float top = SolarSystemLayout.CentreHeightMeters + sun.DiameterMeters * 0.5f;
            float bottom = SolarSystemLayout.CentreHeightMeters - sun.DiameterMeters * 0.5f;

            Assert.That(top, Is.LessThan(SolarSystemLayout.RoomHeightMeters));
            Assert.That(bottom, Is.GreaterThan(0f));
        }

        // ── orbits that do not cross ───────────────────────────────────────────────────────────────

        [Test]
        public void NoTwoAdjacentOrbits_Intersect()
        {
            // The constraint that actually set every diameter in the table. Two neighbours clear each other only
            // if the gap between their orbits holds both radii.
            BodySpec[] ordered = SunOrbiters;

            for (int i = 0; i < ordered.Length - 1; i++)
            {
                BodySpec inner = ordered[i];
                BodySpec outer = ordered[i + 1];

                float gap = outer.OrbitRadiusMeters - inner.OrbitRadiusMeters;
                float needed = (inner.DiameterMeters + outer.DiameterMeters) * 0.5f;

                Assert.That(needed, Is.LessThanOrEqualTo(gap + Tol),
                    $"{inner.Name} and {outer.Name} are {gap:0.0000} m apart but need {needed:0.0000} m");
            }
        }

        [Test]
        public void TheSun_DoesNotSwallowMercury()
        {
            Assert.That(SolarSystemLayout.TryGet("Sun", out BodySpec sun), Is.True);
            Assert.That(SolarSystemLayout.TryGet("Mercury", out BodySpec mercury), Is.True);

            float needed = (sun.DiameterMeters + mercury.DiameterMeters) * 0.5f;

            Assert.That(needed, Is.LessThan(mercury.OrbitRadiusMeters),
                "Mercury's orbit has to clear the Sun's surface");
        }

        [Test]
        public void TheMoon_StaysInsideEarthsOwnGap()
        {
            // A moon is the one body that can reach outside its host's lane, so it gets checked against the
            // nearest orbit on either side rather than against the room.
            Assert.That(SolarSystemLayout.TryGet("Earth", out BodySpec earth), Is.True);
            Assert.That(SolarSystemLayout.TryGet("Moon", out BodySpec moon), Is.True);
            Assert.That(SolarSystemLayout.TryGet("Venus", out BodySpec venus), Is.True);
            Assert.That(SolarSystemLayout.TryGet("Mars", out BodySpec mars), Is.True);

            float reach = moon.OrbitRadiusMeters + moon.DiameterMeters * 0.5f;

            float inwardGap = earth.OrbitRadiusMeters - venus.OrbitRadiusMeters - venus.DiameterMeters * 0.5f;
            float outwardGap = mars.OrbitRadiusMeters - earth.OrbitRadiusMeters - mars.DiameterMeters * 0.5f;

            Assert.That(reach, Is.LessThan(inwardGap), "the Moon must not swing into Venus' orbit");
            Assert.That(reach, Is.LessThan(outwardGap), "the Moon must not swing into Mars' orbit");
            Assert.That(moon.OrbitRadiusMeters, Is.GreaterThan((earth.DiameterMeters + moon.DiameterMeters) * 0.5f),
                "the Moon must not sit inside the Earth");
        }

        // ── the compression laws behave ────────────────────────────────────────────────────────────

        [Test]
        public void Diameters_KeepTheRealOrdering()
        {
            // Compression is allowed to shrink the range; it is not allowed to make Mars bigger than Neptune.
            var realDiametersKm = new (string Name, float Km)[]
            {
                ("Pluto", 2377f), ("Moon", 3475f), ("Mercury", 4879f), ("Mars", 6779f),
                ("Venus", 12104f), ("Earth", 12742f), ("Neptune", 49244f), ("Uranus", 50724f),
                ("Saturn", 116464f), ("Jupiter", 139822f), ("Sun", 1392680f),
            };

            for (int i = 0; i < realDiametersKm.Length - 1; i++)
            {
                Assert.That(SolarSystemLayout.TryGet(realDiametersKm[i].Name, out BodySpec smaller), Is.True);
                Assert.That(SolarSystemLayout.TryGet(realDiametersKm[i + 1].Name, out BodySpec larger), Is.True);

                Assert.That(smaller.DiameterMeters, Is.LessThanOrEqualTo(larger.DiameterMeters),
                    $"{smaller.Name} is smaller than {larger.Name} in the sky and must stay smaller in the room");
            }
        }

        [Test]
        public void DisplayPeriods_AreOrderedInsideOutAndStayWatchable()
        {
            // The whole point of compressing time: the ordering survives, and the slowest body still comes round
            // inside a minute so a visitor sees it move rather than being told that it does.
            BodySpec[] ordered = SunOrbiters;
            float previous = 0f;

            foreach (BodySpec body in ordered)
            {
                float seconds = SolarSystemLayout.DisplayPeriodSeconds(body.SiderealPeriodDays);

                Assert.That(seconds, Is.GreaterThan(previous),
                    $"{body.Name} is further out than the last body and must take longer to come round");

                previous = seconds;
            }

            Assert.That(previous, Is.LessThan(60f), "the outermost orbit should complete inside a minute");
        }

        [Test]
        public void DisplayPeriod_OfEarth_IsTheAnchor()
        {
            Assert.That(SolarSystemLayout.DisplayPeriodSeconds(SolarSystemLayout.EarthSiderealDays),
                Is.EqualTo(SolarSystemLayout.DefaultEarthPeriodSeconds).Within(Tol));
        }

        [Test]
        public void DisplayPeriod_AtFullCompressionOfOne_ReproducesTrueRatios()
        {
            // Compression 1 is the escape hatch for anyone who wants real relative time, so it has to be exact.
            Assert.That(SolarSystemLayout.TryGet("Jupiter", out BodySpec jupiter), Is.True);

            float seconds = SolarSystemLayout.DisplayPeriodSeconds(jupiter.SiderealPeriodDays,
                                                                   earthPeriodSeconds: 1f,
                                                                   compression: 1f);

            Assert.That(seconds, Is.EqualTo(jupiter.SiderealPeriodDays / SolarSystemLayout.EarthSiderealDays)
                .Within(1e-3f));
        }

        [Test]
        public void DisplayPeriod_OfAStationaryBody_IsZeroRatherThanInfinite()
        {
            Assert.That(SolarSystemLayout.DisplayPeriodSeconds(0f), Is.EqualTo(0f));
        }

        [Test]
        public void StartingPhases_AreSpreadRatherThanAligned()
        {
            // Golden-angle spacing, so no two bodies start close to a straight line out from the Sun.
            var phases = Enumerable.Range(0, SolarSystemLayout.Bodies.Length)
                .Select(SolarSystemLayout.PhaseDegrees)
                .ToArray();

            foreach (float phase in phases)
                Assert.That(phase, Is.InRange(0f, 360f));

            for (int i = 0; i < phases.Length; i++)
            {
                for (int j = i + 1; j < phases.Length; j++)
                {
                    float separation = Mathf.Abs(Mathf.DeltaAngle(phases[i], phases[j]));
                    Assert.That(separation, Is.GreaterThan(5f),
                        $"bodies {i} and {j} start only {separation:0.0} degrees apart");
                }
            }
        }
    }
}
