using Stadium3D.CaveBox;
using NUnit.Framework;
using UnityEngine;

namespace Stadium3D.CaveBox.Tests
{
    /// <summary>
    /// Phase 3 movable-panel proof: the local→world placement lift and the wall↔floor region cycle are
    /// pure, so they run in the Test Runner without the editor. The in-CAVE motion and wand gesture
    /// still need the editor.
    /// </summary>
    public class RegionPlacementTests
    {
        [Test]
        public void WorldSlot_NullRoot_EqualsLocalSlot()
        {
            var region = new CaveRegion
            {
                Center = new Vector3(1f, 2f, 3f),
                EulerAngles = new Vector3(0f, 90f, 0f),
                Size = new Vector2(2f, 1f)
            };

            var local = region.GetLocalSlot(0, 1);
            var world = RegionPlacement.WorldSlot(region, null, 0, 1);

            Assert.That(Vector3.Distance(world.position, local.position), Is.LessThan(1e-5f));
            Assert.That(Quaternion.Angle(world.rotation, local.rotation), Is.LessThan(1e-3f));
        }

        [Test]
        public void TryWorldSlot_DefaultMap_FindsFloorFacingDown()
        {
            var map = ScriptableObject.CreateInstance<CaveRegionMap>();
            CaveRegionMap.PopulateDefault(map);

            Assert.IsTrue(RegionPlacement.TryWorldSlot(map, "Floor", null, 0, 1, out var pose));
            // Floor +Z points down into the floor (readable face up) — the facing convention holds here too.
            Assert.That(Vector3.Angle(pose.rotation * Vector3.forward, Vector3.down), Is.LessThan(0.01f));

            Object.DestroyImmediate(map);
        }

        [Test]
        public void TryWorldSlot_UnknownRegion_ReturnsFalse()
        {
            var map = ScriptableObject.CreateInstance<CaveRegionMap>();
            CaveRegionMap.PopulateDefault(map);

            Assert.IsFalse(RegionPlacement.TryWorldSlot(map, "Nope", null, 0, 1, out _));

            Object.DestroyImmediate(map);
        }

        [Test]
        public void NextInCycle_TogglesWallAndFloor()
        {
            var cycle = new[] { "FrontWall", "Floor" };
            Assert.AreEqual("Floor", RegionPanel.NextInCycle("FrontWall", cycle));
            Assert.AreEqual("FrontWall", RegionPanel.NextInCycle("Floor", cycle));
        }

        [Test]
        public void NextInCycle_UnknownCurrent_ReturnsFirst()
        {
            var cycle = new[] { "FrontWall", "Floor" };
            Assert.AreEqual("FrontWall", RegionPanel.NextInCycle("LeftWall", cycle));
        }

        [Test]
        public void NextInCycle_EmptyOrNull_ReturnsNull()
        {
            Assert.IsNull(RegionPanel.NextInCycle("FrontWall", new string[0]));
            Assert.IsNull(RegionPanel.NextInCycle("FrontWall", null));
        }
    }
}
