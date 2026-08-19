using Stadium3D.CaveBox;
using NUnit.Framework;
using UnityEngine;

namespace Stadium3D.CaveBox.Tests
{
    /// <summary>
    /// Region-mapping proof that runs in the Test Runner without the editor GUI: the wall/floor region
    /// math is pure, so it is verified here off the Unity run loop. The in-scene rendering and
    /// wand-focus halves still need the editor.
    /// </summary>
    public class CaveRegionMapTests
    {
        [Test]
        public void PopulateDefault_DefinesFloorAndThreeWalls()
        {
            var map = ScriptableObject.CreateInstance<CaveRegionMap>();
            CaveRegionMap.PopulateDefault(map);

            Assert.AreEqual(4, map.Regions.Count);
            Assert.IsTrue(map.TryGetRegion("Floor", out _));
            Assert.IsTrue(map.TryGetRegion("FrontWall", out _));
            Assert.IsTrue(map.TryGetRegion("LeftWall", out _));
            Assert.IsTrue(map.TryGetRegion("RightWall", out _));

            Object.DestroyImmediate(map);
        }

        [Test]
        public void PopulateDefault_RegionsOrientReadableFaceIntoTheRoom()
        {
            var map = ScriptableObject.CreateInstance<CaveRegionMap>();
            CaveRegionMap.PopulateDefault(map);

            // A world-space UI reads from its -Z side, so +Z points into the surface (away from the
            // operator) and the readable -Z face points into the room. Guards the facing convention.
            AssertForward(map, "FrontWall", Vector3.forward);   // +Z into the +Z wall
            AssertForward(map, "LeftWall", Vector3.left);       // +Z into the -X wall
            AssertForward(map, "RightWall", Vector3.right);     // +Z into the +X wall
            AssertForward(map, "Floor", Vector3.down);          // +Z down into the floor

            Object.DestroyImmediate(map);
        }

        static void AssertForward(CaveRegionMap map, string region, Vector3 expected)
        {
            Assert.IsTrue(map.TryGetRegion(region, out var r), $"missing region {region}");
            var forward = r.Rotation * Vector3.forward;
            Assert.That(Vector3.Angle(forward, expected), Is.LessThan(0.01f),
                $"{region} +Z should point {expected}, was {forward}");
        }

        [Test]
        public void TryGetRegion_UnknownName_ReturnsFalse()
        {
            var map = ScriptableObject.CreateInstance<CaveRegionMap>();
            CaveRegionMap.PopulateDefault(map);

            Assert.IsFalse(map.TryGetRegion("Ceiling", out var region));
            Assert.IsNull(region);

            Object.DestroyImmediate(map);
        }

        [Test]
        public void GetLocalSlot_SingleWindow_SitsAtRegionCenter()
        {
            var region = new CaveRegion
            {
                Center = new Vector3(0f, 1.4f, 1.05f),
                EulerAngles = new Vector3(0f, 180f, 0f),
                Size = new Vector2(3f, 1.2f)
            };

            var slot = region.GetLocalSlot(0, 1);
            Assert.That(Vector3.Distance(slot.position, region.Center), Is.LessThan(1e-4f));
        }

        [Test]
        public void GetLocalSlot_TwoWindows_AreSymmetricAboutCenter()
        {
            var region = new CaveRegion
            {
                Center = Vector3.zero,
                EulerAngles = Vector3.zero,
                Size = new Vector2(4f, 1f)
            };

            var a = region.GetLocalSlot(0, 2).position;
            var b = region.GetLocalSlot(1, 2).position;

            // Midpoint lands on the region centre, one window per half (spacing = 4/2 = 2 → x = ±1).
            Assert.That(Vector3.Distance((a + b) * 0.5f, region.Center), Is.LessThan(1e-4f));
            Assert.That(Mathf.Abs(a.x), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(Mathf.Abs(b.x), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void GetLocalSlot_FourWindows_FormTwoByTwoGrid()
        {
            var region = new CaveRegion
            {
                Center = Vector3.zero,
                EulerAngles = Vector3.zero,
                Size = new Vector2(4f, 2f)
            };

            var p = new Vector3[4];
            for (int i = 0; i < 4; i++)
                p[i] = region.GetLocalSlot(i, 4).position;

            // 2x2 grid on a 4x2 region → columns at x = ±1, rows at y = ±0.5.
            // 0 = top-left, 1 = top-right, 2 = bottom-left, 3 = bottom-right.
            Assert.That(p[0].x, Is.EqualTo(-1f).Within(1e-4f));  Assert.That(p[0].y, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(p[1].x, Is.EqualTo(1f).Within(1e-4f));   Assert.That(p[1].y, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(p[2].x, Is.EqualTo(-1f).Within(1e-4f));  Assert.That(p[2].y, Is.EqualTo(-0.5f).Within(1e-4f));
            Assert.That(p[3].x, Is.EqualTo(1f).Within(1e-4f));   Assert.That(p[3].y, Is.EqualTo(-0.5f).Within(1e-4f));

            // Centroid stays on the region centre.
            var centroid = (p[0] + p[1] + p[2] + p[3]) * 0.25f;
            Assert.That(Vector3.Distance(centroid, region.Center), Is.LessThan(1e-4f));
        }

        [Test]
        public void GetLocalSlot_AdoptsRegionRotation()
        {
            var region = new CaveRegion
            {
                Center = Vector3.zero,
                EulerAngles = new Vector3(0f, 90f, 0f),
                Size = new Vector2(2f, 1f)
            };

            var slot = region.GetLocalSlot(0, 1);
            Assert.That(Quaternion.Angle(slot.rotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(1e-3f));
        }

        [Test]
        public void GetLocalSlot_IndexClampedToRange()
        {
            var region = new CaveRegion { Center = Vector3.zero, Size = new Vector2(4f, 1f) };

            var over = region.GetLocalSlot(5, 2).position;   // index past the last slot
            var last = region.GetLocalSlot(1, 2).position;
            Assert.That(Vector3.Distance(over, last), Is.LessThan(1e-4f));
        }


    }
}
