using System.Collections.Generic;
using NUnit.Framework;
using Stadium3D.Football;
using Stadium3D.Pitch;
using UnityEngine;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// The depth separation that keeps two players standing in line from stacking their numbers.
    /// </summary>
    /// <remarks>
    /// The assertion worth having here is the ordering one. A lift that is merely <em>proportional to
    /// depth</em> passes every "the numbers moved" check and separates nothing at all, because screen
    /// height goes as <c>(worldY − eyeY) / depth</c> and a lift of <c>k · depth</c> contributes a flat
    /// <c>k</c> to near and far alike. Only a test that compares two players at different depths against
    /// each other can tell the working version from that one.
    /// </remarks>
    public class JerseyNumbersTests
    {
        readonly List<GameObject> m_Junk = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in m_Junk) if (go != null) Object.DestroyImmediate(go);
            m_Junk.Clear();
        }

        PlayerMarker Marker(TeamSide side, int number, float z)
        {
            var go = new GameObject($"#{number}");
            m_Junk.Add(go);

            var numberGo = new GameObject("Number");
            numberGo.transform.SetParent(go.transform, false);
            var text = numberGo.AddComponent<TextMesh>();

            var marker = go.AddComponent<PlayerMarker>();
            marker.Configure(null, null, null, null, text);
            marker.Bind(new PlayerIdentity { Number = number, Side = side });

            go.transform.position = new Vector3(0f, 0f, z);
            return marker;
        }

        Camera Eye(params string[] visibleLayers)
        {
            var go = new GameObject("Eye");
            m_Junk.Add(go);

            var camera = go.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 3.4f, 0f);
            camera.cullingMask = PitchLayers.Mask(visibleLayers);
            return camera;
        }

        static float NumberHeight(PlayerMarker marker) =>
            marker.transform.GetChild(0).localPosition.y;

        [Test]
        public void TheNearerPlayersNumberSitsBelowTheFurtherOne()
        {
            var near = Marker(TeamSide.Home, 12, 14f);
            var far = Marker(TeamSide.Home, 88, 16f);
            var camera = Eye(PitchLayers.RightWall);

            JerseyNumbers.Orient(camera, new[] { near, far });

            Assert.That(NumberHeight(near), Is.LessThan(NumberHeight(far)),
                "two players in line have to label themselves at different heights or the numbers collide");

            // The nearest rests where it was put and everything behind rises off it, so no number is ever
            // pushed down onto the body it labels — which is what a mean-referenced lift did.
            Assert.That(NumberHeight(near), Is.EqualTo(PlayerMarker.NumberRestHeight).Within(1e-3f));
            Assert.That(NumberHeight(far), Is.GreaterThan(PlayerMarker.NumberRestHeight));
        }

        /// <summary>
        /// The back of the group has to stay ordered too. A hard limit hands every player past it the same
        /// height, which un-separates exactly the crowd it was meant to tidy, and a "numbers moved"
        /// assertion cannot tell that apart from working.
        /// </summary>
        /// <remarks>
        /// Twelve metres back, not sixty. The saturating curve is strictly increasing in exact arithmetic
        /// and not in <c>float</c> — past about 28 m the exponential falls under single-precision
        /// resolution and two lifts really are equal. Asserting otherwise would be pinning a claim the
        /// type cannot keep, and at that range the bodies are a few pixels wide anyway.
        /// </remarks>
        [Test]
        public void NumbersStayOrderedAcrossTheDepthsAWallActuallyShows()
        {
            var front = Marker(TeamSide.Home, 12, 14f);
            var deep = Marker(TeamSide.Home, 88, 20f);
            var deeper = Marker(TeamSide.Home, 21, 26f);

            JerseyNumbers.Orient(Eye(PitchLayers.RightWall), new[] { front, deep, deeper });

            Assert.That(NumberHeight(deep), Is.GreaterThan(NumberHeight(front)));
            Assert.That(NumberHeight(deeper), Is.GreaterThan(NumberHeight(deep)),
                "two players well past the limit must still differ, or the limit has flattened them together");

            Assert.That(NumberHeight(deeper) - PlayerMarker.NumberRestHeight,
                Is.LessThanOrEqualTo(JerseyNumbers.DefaultLiftLimit),
                "and none of them may climb off the top of the wall");
        }

        [Test]
        public void ACameraThatDrawsNoNumbersLeavesThemAlone()
        {
            var marker = Marker(TeamSide.Home, 12, 14f);
            marker.transform.GetChild(0).localPosition = new Vector3(0f, 99f, 0f);

            // The broadcast wall: bodies but no numbers. This is the case a Players-layer gate got wrong
            // in the other direction, skipping the two walls that do draw them.
            var camera = Eye(PitchLayers.Shared, PitchLayers.Players, PitchLayers.Stadium);

            JerseyNumbers.Orient(camera, new[] { marker });

            Assert.That(NumberHeight(marker), Is.EqualTo(99f).Within(1e-4f));
        }

        [Test]
        public void AWallOnlyTouchesItsOwnTeamsNumbers()
        {
            var ours = Marker(TeamSide.Home, 12, 14f);
            var theirs = Marker(TeamSide.Away, 44, 16f);
            theirs.transform.GetChild(0).localPosition = new Vector3(0f, 99f, 0f);

            JerseyNumbers.Orient(Eye(PitchLayers.RightWall), new[] { ours, theirs });

            Assert.That(NumberHeight(theirs), Is.EqualTo(99f).Within(1e-4f),
                "the home wall ghosts the away team out; labelling them anyway is the contradiction this fixed");

            // With only one number drawn there is nothing to separate it from, so it rests.
            Assert.That(NumberHeight(ours), Is.EqualTo(PlayerMarker.NumberRestHeight).Within(1e-3f));
        }
    }
}
