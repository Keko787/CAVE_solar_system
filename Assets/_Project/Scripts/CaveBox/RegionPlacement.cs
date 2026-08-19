using UnityEngine;

namespace Stadium3D.CaveBox
{
    /// <summary>
    /// Turns a <see cref="CaveRegion"/> slot (region-local) into a world pose through a region-map root
    /// transform. Shared by anything that places content into CAVE regions — the wall video panels and
    /// the movable stat panels — so the local→world lift lives in one place. Pure when
    /// <paramref name="root"/> is null (returns the local pose), so it is unit-testable off a scene.
    /// </summary>
    public static class RegionPlacement
    {
        /// <summary>World pose for window <paramref name="index"/> of <paramref name="count"/> in a region.</summary>
        public static Pose WorldSlot(CaveRegion region, Transform root, int index, int count)
        {
            var local = region.GetLocalSlot(index, count);
            if (root == null)
                return local;
            return new Pose(root.TransformPoint(local.position), root.rotation * local.rotation);
        }

        /// <summary>Look up a region by name and compute its world slot. False if the region isn't in the map.</summary>
        public static bool TryWorldSlot(CaveRegionMap map, string regionName, Transform root, int index, int count, out Pose pose)
        {
            if (map != null && map.TryGetRegion(regionName, out var region))
            {
                pose = WorldSlot(region, root, index, count);
                return true;
            }
            pose = default;
            return false;
        }
    }
}
