using UnityEngine;
using Stadium3D.Vr;   // MiddleVRWand — shared wand-button helper

namespace Stadium3D.CaveBox
{
    /// <summary>
    /// Wand-driven mover for a <see cref="RegionPanel"/>: on the move button it cycles the panel to its
    /// next region (e.g. a stat panel from the front wall to the floor and back). Either moves a specific
    /// assigned panel, or the panel the wand is pointing at. Kept separate from wall focus so "move a
    /// panel" and "claim a wall" stay distinct gestures on distinct buttons.
    /// </summary>
    public class RegionPanelMover : MonoBehaviour
    {
        [SerializeField, Tooltip("Panel to move. If empty, the panel under the wand ray is moved.")]
        RegionPanel m_TargetPanel;

        [SerializeField, Tooltip("Wand ray origin (attach to the MiddleVR HandNode) for pointing at a panel.")]
        Transform m_WandRayOrigin;

        [SerializeField, Tooltip("Wand button that moves the panel. Per-.vrx; keep it off the trigger " +
            "(the trigger is feed-focus select).")]
        int m_MoveButton = 1;

        [SerializeField, Min(0f)] float m_MaxDistance = 20f;

        [SerializeField, Tooltip("Layers the panel-pick ray considers.")]
        LayerMask m_PanelMask = ~0;

        void Update()
        {
            if (!MiddleVRWand.PressedThisFrame(m_MoveButton))
                return;

            var panel = m_TargetPanel != null ? m_TargetPanel : PickPanel();
            if (panel == null)
                return;

            // A panel that belongs to a group never moves alone — the front wall travels as one surface, so
            // pointing at any of its panels (or at none, via an assigned target) moves the whole set.
            var group = FindGroup(panel);
            if (group != null)
            {
                string moved = group.MoveToNext();
                if (moved != null)
                    Debug.Log($"[RegionPanelMover] Moved the '{group.name}' group " +
                        $"({group.Members.Count} panels) → {moved}.", group);
                return;
            }

            string to = panel.MoveToNext();
            if (to != null)
                Debug.Log($"[RegionPanelMover] Moved '{panel.name}' → {to}.", panel);
        }

        // Groups are few and this runs only on a button press, so a scan beats caching a reference that would go
        // stale every time a panel rig is rebuilt.
        static RegionPanelGroup FindGroup(RegionPanel panel)
        {
            foreach (var g in FindObjectsByType<RegionPanelGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (g != null && g.Contains(panel))
                    return g;
            return null;
        }

        RegionPanel PickPanel()
        {
            if (m_WandRayOrigin == null)
                return null;

            var hits = Physics.RaycastAll(new Ray(m_WandRayOrigin.position, m_WandRayOrigin.forward),
                m_MaxDistance, m_PanelMask, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0)
                return null;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var panel = hit.collider.GetComponentInParent<RegionPanel>();
                if (panel != null)
                    return panel;
            }
            return null;
        }
    }
}
