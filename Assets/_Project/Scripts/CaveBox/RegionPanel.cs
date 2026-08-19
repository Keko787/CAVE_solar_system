using System.Collections.Generic;
using UnityEngine;

namespace Stadium3D.CaveBox
{
    /// <summary>
    /// A world-space panel anchored to a named CAVE region (from a <see cref="CaveRegionMap"/>) that can
    /// move to another region. Moving eases the transform to the new region's slot, so a stat
    /// panel can slide from the front wall to the floor (and back) on an operator gesture — see
    /// <see cref="RegionPanelMover"/>. Placement/motion only; what the panel displays is a separate
    /// concern (a panel surface).
    /// </summary>
    public class RegionPanel : MonoBehaviour
    {
        [SerializeField] CaveRegionMap m_RegionMap;

        [SerializeField, Tooltip("Transform the region map is expressed relative to (the CAVE room). " +
            "Defaults to this panel's parent, else itself.")]
        Transform m_RegionRoot;

        [SerializeField, Tooltip("Region this panel starts in.")]
        string m_CurrentRegion = "FrontWall";

        [SerializeField, Tooltip("Regions this panel cycles through on a move gesture (e.g. wall ↔ floor).")]
        string[] m_MoveCycle = { "FrontWall", "Floor" };

        [SerializeField, Tooltip("How quickly the panel eases to a new region (higher = snappier).")]
        float m_MoveSharpness = 6f;

        [SerializeField, Tooltip("Snap to the starting region on Start instead of easing from the origin.")]
        bool m_SnapOnStart = true;

        Pose m_Target;
        bool m_HasTarget;

        public string CurrentRegion => m_CurrentRegion;

        /// <summary>The regions this panel cycles through. Read by <see cref="RegionPanelGroup"/>, which moves a
        /// set of panels by <i>step index</i> rather than by each one's own next-region.</summary>
        public IReadOnlyList<string> MoveCycle => m_MoveCycle;

        void Awake()
        {
            if (m_RegionRoot == null)
                m_RegionRoot = transform.parent != null ? transform.parent : transform;
        }

        void Start() => PlaceInto(m_CurrentRegion, m_SnapOnStart);

        /// <summary>Move to a named region (eased unless <paramref name="snap"/>).</summary>
        public void MoveTo(string regionName, bool snap = false) => PlaceInto(regionName, snap);

        /// <summary>Advance to the next region in the move cycle (wall → floor → wall …). Returns the new region, or null.</summary>
        public string MoveToNext()
        {
            string next = NextInCycle(m_CurrentRegion, m_MoveCycle);
            if (next != null)
                PlaceInto(next, false);
            return next;
        }

        void PlaceInto(string regionName, bool snap)
        {
            if (!RegionPlacement.TryWorldSlot(m_RegionMap, regionName, m_RegionRoot, 0, 1, out var pose))
            {
                Debug.LogWarning($"[RegionPanel] Region '{regionName}' not found in the map.", this);
                return;
            }

            m_CurrentRegion = regionName;
            m_Target = pose;
            m_HasTarget = true;
            if (snap)
                transform.SetPositionAndRotation(pose.position, pose.rotation);
        }

        void Update()
        {
            if (!m_HasTarget)
                return;

            float k = 1f - Mathf.Exp(-m_MoveSharpness * Time.deltaTime);   // framerate-independent ease
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, m_Target.position, k),
                Quaternion.Slerp(transform.rotation, m_Target.rotation, k));
        }

        /// <summary>
        /// Next entry in <paramref name="cycle"/> after <paramref name="current"/> (wraps). Returns the
        /// first entry if <paramref name="current"/> isn't in the cycle, or null if the cycle is empty.
        /// Pure, so the wall↔floor toggle is unit-tested without a scene.
        /// </summary>
        public static string NextInCycle(string current, IReadOnlyList<string> cycle)
        {
            if (cycle == null || cycle.Count == 0)
                return null;

            int idx = -1;
            for (int i = 0; i < cycle.Count; i++)
            {
                if (cycle[i] == current)
                {
                    idx = i;
                    break;
                }
            }
            return cycle[(idx + 1) % cycle.Count];
        }
    }
}
