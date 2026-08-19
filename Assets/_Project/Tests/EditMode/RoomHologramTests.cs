using NUnit.Framework;
using UnityEngine;
using Stadium3D.Pitch;
using Stadium3D.EditorTools;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// The M3.5 figure: the only stereoscopic content in the demo, and therefore the only part of it that
    /// could not be reproduced by bolting four televisions to the same walls.
    /// </summary>
    /// <remarks>
    /// Two claims are worth pinning here, because both fail quietly. The figure has to be a <b>person's
    /// height</b> — if it is built at 1.6 m or 2.2 m nobody in the room will be able to say which, they will
    /// just report that the demo felt slightly off, and the go/no-go answer would be wrong for a reason that
    /// has nothing to do with stereo. And it has to be on a layer the display cameras <b>keep</b> while every
    /// pitch camera <b>drops</b> it, or it either never appears in the room or turns up floating over the
    /// field in the middle of a broadcast shot.
    /// </remarks>
    public class RoomHologramTests
    {
        [Test]
        public void Figure_IsBuiltAtAPersonsHeight()
        {
            var root = RoomHologramBuilder.Build(null);
            try
            {
                var bounds = MeasureRenderers(root, out int rendererCount);

                Assert.That(rendererCount, Is.GreaterThan(6),
                    "a single capsule tests the wrong thing — the comparison has to be about depth, not about " +
                    "whether a coloured pill looks like a person");

                Assert.That(bounds.max.y, Is.EqualTo(RoomHologramBuilder.FigureHeight).Within(0.12f),
                    "the figure must stand a person's height");
                Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(-0.05f),
                    "and stand ON the floor, not sunk into it or hovering above it");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Figure_HasHumanProportions()
        {
            var root = RoomHologramBuilder.Build(null);
            try
            {
                // Measure the FIGURE, not the root. The root also carries the ground ring — a 0.9 m quad
                // lying on the floor — so measuring from there answers a question about the decoration and
                // reports the answer as a shoulder span. It read 0.90 m against 1.88 m of height and called
                // a correctly proportioned figure a pillar.
                var figure = root.transform.Find("Figure");
                Assert.That(figure, Is.Not.Null, "the builder no longer produces a \"Figure\" child");

                var bounds = MeasureRenderers(figure.gameObject, out _);

                // Shoulder span against height. A real adult is roughly a quarter of their height across the
                // shoulders; anything much narrower reads as a post and defeats the exercise.
                float width = bounds.size.x;
                float ratio = width / RoomHologramBuilder.FigureHeight;

                Assert.That(ratio, Is.InRange(0.18f, 0.40f),
                    $"shoulder span {width:0.00} m against {RoomHologramBuilder.FigureHeight:0.00} m of height " +
                    "should read as a person, not a pillar");
                Assert.That(bounds.size.z, Is.LessThan(width),
                    "a person is wider across the shoulders than front to back");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Figure_IsOnTheHologramLayer_AllTheWayDown()
        {
            var root = RoomHologramBuilder.Build(null);
            try
            {
                int layer = LayerMask.NameToLayer(PitchLayers.Hologram);
                Assert.That(layer, Is.GreaterThanOrEqualTo(0));

                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    Assert.That(t.gameObject.layer, Is.EqualTo(layer),
                        $"\"{t.name}\" is on the wrong layer — one stray part renders in the wrong place");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Hologram_SurvivesTheDisplayCameraStrip()
        {
            // PitchIsolation removes PitchMask from every MiddleVR display camera. If the hologram layer were
            // in that mask, the one thing the room exists to show would be the one thing it never showed.
            int layer = LayerMask.NameToLayer(PitchLayers.Hologram);
            Assert.That((PitchLayers.PitchMask & (1 << layer)), Is.Zero);
        }

        [Test]
        public void NoWallCamera_SeesTheHologram()
        {
            // The mirror failure: a pitch camera that picks it up renders a person standing in mid-air over
            // a football field, at a scale that makes no sense in that shot.
            int layer = LayerMask.NameToLayer(PitchLayers.Hologram);
            int bit = 1 << layer;

            Assert.That(PitchLayers.BroadcastMask & bit, Is.Zero, "broadcast");
            Assert.That(PitchLayers.RightWallMask & bit, Is.Zero, "right wall");
            Assert.That(PitchLayers.LeftWallMask & bit, Is.Zero, "left wall");
            Assert.That(PitchLayers.FloorMask & bit, Is.Zero, "floor");
        }

        [Test]
        public void ScaleRod_IsTallEnoughToMeasureAPersonAgainst()
        {
            var rod = RoomHologramBuilder.BuildScaleRod(null, Vector3.zero);
            try
            {
                var bounds = MeasureRenderers(rod, out _);
                Assert.That(bounds.max.y, Is.GreaterThanOrEqualTo(RoomHologramBuilder.FigureHeight),
                    "the rod has to reach past the figure or it cannot answer 'is this life-size'");
            }
            finally { Object.DestroyImmediate(rod); }
        }

        static Bounds MeasureRenderers(GameObject root, out int count)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            count = renderers.Length;
            Assert.That(count, Is.GreaterThan(0), "nothing was built");

            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }
    }
}
