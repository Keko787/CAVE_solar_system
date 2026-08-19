using System;
using UnityEngine;

namespace Stadium3D.CaveBox
{
    /// <summary>
    /// One named placement zone on a CAVE surface (a wall or the floor), expressed as data in the
    /// region map's local frame — never a scene <see cref="Transform"/>, so the mapping stays a config
    /// asset that can be tuned per mission without touching code (implementation plan §4.2). A region
    /// owns a small patch of a surface (<see cref="Size"/>) centred on <see cref="Center"/> and
    /// oriented by <see cref="EulerAngles"/> so a feed window placed in it faces into the room toward
    /// the operator.
    /// </summary>
    [Serializable]
    public class CaveRegion
    {
        [Tooltip("Stable name used to look the region up (e.g. \"FrontWall\", \"Floor\").")]
        public string Name = "Region";

        [Tooltip("Region centre, in the region map root's local space (metres).")]
        public Vector3 Center;

        [Tooltip("Facing of a window placed here, as local euler angles. A world-space UI reads from " +
                 "its -Z side, so orient +Z INTO the surface (away from the operator); the readable " +
                 "face then points into the room toward the operator.")]
        public Vector3 EulerAngles;

        [Tooltip("Usable surface (metres): X is the available width, Y the available height. Windows lay " +
                 "out in a roughly square grid filling this area, centred on the region.")]
        public Vector2 Size = new Vector2(3f, 1.2f);

        /// <summary>Local rotation windows in this region adopt.</summary>
        public Quaternion Rotation => Quaternion.Euler(EulerAngles);

        /// <summary>
        /// Local-space pose for window <paramref name="index"/> of <paramref name="count"/>, laid out in a
        /// roughly square grid — <c>ceil(sqrt(count))</c> columns across <see cref="Size"/>.x, filled row
        /// by row top-to-bottom down <see cref="Size"/>.y — centred on <see cref="Center"/>. A single
        /// window sits at the centre; two fall back to a side-by-side row. Pure and deterministic (no
        /// <see cref="Transform"/>), so it is unit-tested off the Unity run loop; the spawner lifts the
        /// result into world space through the region-map root.
        /// </summary>
        public Pose GetLocalSlot(int index, int count)
        {
            count = Mathf.Max(1, count);
            index = Mathf.Clamp(index, 0, count - 1);

            int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count)));
            int rows = Mathf.CeilToInt((float)count / cols);
            int col = index % cols;
            int row = index / cols;

            var rot = Rotation;
            float stepX = Size.x / cols;
            float stepY = Size.y / rows;
            float offsetX = -0.5f * Size.x + stepX * (col + 0.5f);
            float offsetY = 0.5f * Size.y - stepY * (row + 0.5f);   // row 0 across the top
            Vector3 pos = Center + rot * new Vector3(offsetX, offsetY, 0f);
            return new Pose(pos, rot);
        }
    }
}
