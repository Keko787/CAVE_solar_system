using NUnit.Framework;
using Stadium3D.EditorTools;
using Stadium3D.Pitch;
using UnityEngine;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// The border around each surface, measured rather than looked at.
    /// </summary>
    /// <remarks>
    /// The whole job of a bezel is to be the thing between two pictures, so what matters is that it reaches
    /// the wall's edge — a frame that stops short leaves the two images touching at the corner and looks,
    /// from any single wall, exactly like a frame that works.
    /// </remarks>
    public class WallBezelTests
    {
        GameObject m_Panel;

        [SetUp]
        public void SetUp() => m_Panel = new GameObject("Panel");

        [TearDown]
        public void TearDown()
        {
            if (m_Panel != null) Object.DestroyImmediate(m_Panel);
        }

        static Vector2 Wide => new Vector2(WallFraming.WideWallWidthMeters, WallFraming.WallHeightMeters);
        static Vector2 Side => new Vector2(WallFraming.SideWallWidthMeters, WallFraming.WallHeightMeters);

        [Test]
        public void TheFrameReachesTheEdgeOfTheWallItIsOn()
        {
            foreach (var wall in new[] { Wide, Side })
            {
                var bezel = WallBezelBuilder.Build(m_Panel.transform, wall);
                var bounds = bezel.GetComponent<MeshFilter>().sharedMesh.bounds;

                Assert.That(bounds.size.x, Is.EqualTo(wall.x).Within(0.0001f),
                    "a frame inset from the wall edge leaves the two pictures touching at the corner");
                Assert.That(bounds.size.y, Is.EqualTo(wall.y).Within(0.0001f));

                Object.DestroyImmediate(bezel);
            }
        }

        /// <summary>
        /// Uniform on all four sides, which is not free: the panels are inset by different amounts on the
        /// two axes, so the border has to be measured from the wall rather than from the picture.
        /// </summary>
        [Test]
        public void TheBorderIsTheSameWidthOnEverySide()
        {
            var bezel = WallBezelBuilder.Build(m_Panel.transform, Wide);
            var verts = bezel.GetComponent<MeshFilter>().sharedMesh.vertices;

            float outerX = 0f, outerY = 0f, innerX = float.MaxValue, innerY = float.MaxValue;
            foreach (var v in verts)
            {
                outerX = Mathf.Max(outerX, Mathf.Abs(v.x));
                outerY = Mathf.Max(outerY, Mathf.Abs(v.y));
                innerX = Mathf.Min(innerX, Mathf.Abs(v.x));
                innerY = Mathf.Min(innerY, Mathf.Abs(v.y));
            }

            Assert.That(outerX - innerX, Is.EqualTo(WallBezelBuilder.BezelMeters).Within(0.0001f));
            Assert.That(outerY - innerY, Is.EqualTo(WallBezelBuilder.BezelMeters).Within(0.0001f));
        }

        /// <summary>
        /// In front of the picture, and only just.
        /// </summary>
        /// <remarks>
        /// Behind it, the panel hides the frame and nothing appears to have been built at all. Far in front
        /// of it, §3.4's stereo comfort problem starts: content at 2 cm of disparity over a zero-disparity
        /// background at 1–2 m. Two millimetres is enough to beat z-fighting and a tenth of the distance
        /// that argument is about.
        /// </remarks>
        [Test]
        public void TheFrameSitsJustInFrontOfThePictureRatherThanBehindIt()
        {
            var bezel = WallBezelBuilder.Build(m_Panel.transform, Wide);

            // A CaveRegion orients +Z into the surface, so toward the room is negative.
            Assert.That(bezel.transform.localPosition.z, Is.LessThan(0f),
                "behind the panel it would be invisible, which looks identical to never having been built");
            Assert.That(Mathf.Abs(bezel.transform.localPosition.z), Is.LessThan(0.005f),
                "and this is a bezel, not a picture frame standing off the wall");
        }

        [Test]
        public void TheFrameHasAHoleInIt()
        {
            var bezel = WallBezelBuilder.Build(m_Panel.transform, Wide);
            var mesh = bezel.GetComponent<MeshFilter>().sharedMesh;

            Assert.That(mesh.vertexCount, Is.EqualTo(8), "four corners outside, four inside");
            Assert.That(mesh.triangles.Length / 3, Is.EqualTo(8), "two triangles per side");

            // The centre of the wall has to be picture, not frame.
            foreach (var v in mesh.vertices)
            {
                Assert.That(Mathf.Abs(v.x) + Mathf.Abs(v.y), Is.GreaterThan(0.1f),
                    "a vertex near the middle means this is a filled rectangle covering the screen");
            }
        }

        /// <summary>
        /// The border belongs to the room, so no pitch camera may ever draw one into its picture.
        /// </summary>
        [Test]
        public void NoPitchCameraCanSeeTheBorder()
        {
            int screen = LayerMask.NameToLayer(PitchLayers.CaveScreen);
            Assert.That(screen, Is.GreaterThanOrEqualTo(0));

            foreach (int mask in new[]
            {
                PitchLayers.BroadcastMask, PitchLayers.FloorMask,
                PitchLayers.RightWallMask, PitchLayers.LeftWallMask,
            })
            {
                Assert.That(mask & (1 << screen), Is.Zero,
                    "a bezel rendered into a wall's own video would travel with the picture");
            }

            Assert.That(PitchLayers.PitchMask & (1 << screen), Is.Zero,
                "and PitchIsolation must not strip it from the display cameras that are meant to show it");
        }
    }
}
