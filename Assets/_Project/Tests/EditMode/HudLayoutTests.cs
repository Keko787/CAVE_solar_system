using NUnit.Framework;
using Stadium3D.Pitch;
using UnityEngine;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// The stat readout's placement maths, checked against the geometry §8 is built on.
    /// </summary>
    public class HudLayoutTests
    {
        [Test]
        public void TheReadableBandIsTheCentralThirdOfTheFrontWall()
        {
            // 1.28 m of 3.556 m — 60° of comfortable vision out of the 116° the wall subtends.
            Assert.That(HudLayout.ReadableFraction, Is.EqualTo(0.36f).Within(0.005f));
            Assert.That(HudLayout.ReadableMin, Is.EqualTo(0.32f).Within(0.005f));
            Assert.That(HudLayout.ReadableMax, Is.EqualTo(0.68f).Within(0.005f));

            Assert.That(HudLayout.IsReadable(0.5f), Is.True);
            Assert.That(HudLayout.IsReadable(0.13f), Is.False, "the corners are where 64% of the wall is");
            Assert.That(HudLayout.IsReadable(0.87f), Is.False);
        }

        [Test]
        public void AnElementIsOnlyReadableIfBothItsEdgesAre()
        {
            Assert.That(HudLayout.IsReadable(0.5f, 0.30f), Is.True);
            Assert.That(HudLayout.IsReadable(0.5f, 0.50f), Is.False,
                "a centred element wider than the band still has its ends out in the periphery");
        }

        /// <summary>
        /// The one that makes the plane distance a free choice rather than a tuned constant.
        /// </summary>
        [Test]
        public void ThePlaneDistanceDoesNotChangeWhereAnElementLandsOnScreen()
        {
            var viewport = new Vector2(0.25f, 0.8f);

            for (int i = 0; i < 2; i++)
            {
                float distance = i == 0 ? 2f : 40f;

                var local = HudLayout.LocalPosition(false, 42f, 1.6f, distance, viewport);
                var half = HudLayout.HalfExtents(false, 42f, 1.6f, distance);

                Assert.That(local.x / half.x * 0.5f + 0.5f, Is.EqualTo(viewport.x).Within(0.0001f));
                Assert.That(local.y / half.y * 0.5f + 0.5f, Is.EqualTo(viewport.y).Within(0.0001f));
            }
        }

        [Test]
        public void AnElementScalesWithTheFrameRatherThanStayingAFixedSize()
        {
            float near = HudLayout.WorldHeight(false, 42f, 1.6f, 2f, 0.05f);
            float far = HudLayout.WorldHeight(false, 42f, 1.6f, 40f, 0.05f);

            Assert.That(far, Is.GreaterThan(near),
                "the same fraction of a bigger frame is more metres — which is what keeps it the same " +
                "fraction of the wall once it is rendered");
        }

        [Test]
        public void AnOrthographicFrameIgnoresDistance()
        {
            var near = HudLayout.HalfExtents(true, 12f, 1.6f, 1f);
            var far = HudLayout.HalfExtents(true, 12f, 1.6f, 30f);

            Assert.That(near, Is.EqualTo(far), "the floor camera's frame is the same size all the way down");
            Assert.That(near.y, Is.EqualTo(12f).Within(0.0001f));
            Assert.That(near.x, Is.EqualTo(12f * 1.6f).Within(0.0001f));
        }

        [Test]
        public void TheCentreOfTheFrameIsTheCentreOfTheFrame()
        {
            var local = HudLayout.LocalPosition(false, 60f, 1.074f, 3f, new Vector2(0.5f, 0.5f));

            Assert.That(local.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(local.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(local.z, Is.EqualTo(3f).Within(0.0001f), "and it sits on the plane it was asked for");
        }
    }
}
