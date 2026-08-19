using System.Collections.Generic;
using UnityEngine;

namespace Stadium3D.CaveBox
{
    /// <summary>
    /// The wall/floor → airspace-region mapping as a config asset (implementation plan §4.2): named
    /// regions on the CAVE surfaces, defined as data rather than hardcoded transforms so "how much
    /// stays fixed vs. reconfigurable per mission" is a config change, not a code change.
    /// Wall panels and windows read this for placement; nothing here references scene
    /// objects, so it stays a pure asset. The defaults (<see cref="PopulateDefault"/>) match the
    /// VR-Demo <c>CAVE_Room</c> geometry copied into this repo (≈3.56 m wide × 2.22 m deep, walls
    /// ≈2.22 m tall) and are meant to be tuned in-editor against the MiddleVR Preview Window.
    /// </summary>
    [CreateAssetMenu(fileName = "CaveRegionMap", menuName = "Stadium3D/CAVE Region Map", order = 0)]
    public class CaveRegionMap : ScriptableObject
    {
        [SerializeField]
        List<CaveRegion> m_Regions = new();

        public IReadOnlyList<CaveRegion> Regions => m_Regions;

        /// <summary>Find a region by name.</summary>
        public bool TryGetRegion(string name, out CaveRegion region)
        {
            for (int i = 0; i < m_Regions.Count; i++)
            {
                if (m_Regions[i] != null && m_Regions[i].Name == name)
                {
                    region = m_Regions[i];
                    return true;
                }
            }
            region = null;
            return false;
        }

        /// <summary>The named region, or null if it isn't defined.</summary>
        public CaveRegion GetRegion(string name) => TryGetRegion(name, out var r) ? r : null;

        /// <summary>Add a region if one with this name isn't already defined. Returns true if it was added.
        /// Used by builders to extend the map non-destructively (e.g. the tables window's FrontWallLower band)
        /// without regenerating the whole asset.</summary>
        public bool EnsureRegion(string name, Vector3 center, Vector3 eulerAngles, Vector2 size)
        {
            if (TryGetRegion(name, out _))
                return false;
            m_Regions.Add(new CaveRegion { Name = name, Center = center, EulerAngles = eulerAngles, Size = size });
            return true;
        }

        /// <summary>
        /// Replace the region list with the default CAVE layout: a top-down floor map plus front,
        /// left, and right wall zones, each facing into the room. Shared by the editor asset builder
        /// and the region-map tests so the "what a default map looks like" contract is verified.
        /// </summary>
        public static void PopulateDefault(CaveRegionMap map)
        {
            if (map == null)
                return;

            // A world-space UI reads from its -Z side, so EulerAngles orient +Z INTO the surface
            // (away from the operator); the readable face then points back into the room.
            map.m_Regions = new List<CaveRegion>
            {
                // Floor: top-down map. +Z points down into the floor; read from above.
                new CaveRegion
                {
                    Name = "Floor",
                    Center = new Vector3(0f, 0.02f, 0f),
                    EulerAngles = new Vector3(90f, 0f, 0f),
                    Size = new Vector2(2.4f, 2.0f)
                },
                // Front wall (+Z): +Z into the wall (+Z); readable face toward the operator.
                new CaveRegion
                {
                    Name = "FrontWall",
                    Center = new Vector3(0f, 1.4f, 1.05f),
                    EulerAngles = new Vector3(0f, 0f, 0f),
                    Size = new Vector2(3.0f, 1.2f)
                },
                // Left wall (-X): +Z into the wall (-X); readable face toward the operator.
                new CaveRegion
                {
                    Name = "LeftWall",
                    Center = new Vector3(-1.72f, 1.4f, 0f),
                    EulerAngles = new Vector3(0f, -90f, 0f),
                    Size = new Vector2(1.8f, 1.2f)
                },
                // Right wall (+X): +Z into the wall (+X); readable face toward the operator.
                new CaveRegion
                {
                    Name = "RightWall",
                    Center = new Vector3(1.72f, 1.4f, 0f),
                    EulerAngles = new Vector3(0f, 90f, 0f),
                    Size = new Vector2(1.8f, 1.2f)
                }
            };
        }
    }
}
