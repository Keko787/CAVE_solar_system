using System;
using System.Collections.Generic;
using UnityEngine;

namespace Stadium3D.CaveBox
{
    /// <summary>
    /// Moves a set of <see cref="RegionPanel"/>s as one surface. The CAVE's front wall carries four separate
    /// panels — the map, the Routes overview, the Fleet manager, and the mission tables — but the operator reads
    /// them as a single display, so sending the map to the floor while the other three stayed on the wall tore a
    /// layout in half that is meant to travel together.
    ///
    /// The group drives by <b>step index</b>, not by each panel's own <see cref="RegionPanel.MoveToNext"/>, and
    /// that is the whole point. A panel nudged on its own — or rebuilt by its rig, which resets it to its starting
    /// region — would otherwise be a step out of phase forever, and the wall would split on the next move. Here
    /// every move puts every member on entry <c>step % cycle.Count</c> of its <i>own</i> cycle, so the set
    /// re-converges whatever happened to any one of them, and members whose cycle has a single entry simply stay
    /// where they are.
    /// </summary>
    public class RegionPanelGroup : MonoBehaviour
    {
        [SerializeField, Tooltip("Panels to move together. Leave EMPTY to adopt by region (below) — that is the " +
            "normal setup, and it survives re-running any one panel's builder.")]
        RegionPanel[] m_Panels;

        [SerializeField, Tooltip("Used when Panels is empty: adopt every RegionPanel whose starting region is " +
            "one of these.")]
        string[] m_AdoptRegions = { "FrontWall", "FrontWallLeft", "FrontWallRight", "FrontWallLower" };

        readonly List<RegionPanel> m_Members = new();
        int m_Step;
        int m_LastMoveFrame = -1;

        /// <summary>The panels this group moves together.</summary>
        public IReadOnlyList<RegionPanel> Members => m_Members;

        /// <summary>The cycle step the group is on. By the builders' convention entry 0 is the wall and entry 1
        /// the floor, for every front-wall panel — that is what lets <see cref="FrontWallPortal"/> clear the wall
        /// by asking for a step rather than knowing any panel's region names.</summary>
        public int Step => m_Step;

        void Awake()
        {
            if (m_Panels != null && m_Panels.Length > 0)
            {
                foreach (var p in m_Panels)
                    if (p != null)
                        m_Members.Add(p);
                return;
            }

            // Adopt by starting region rather than holding a wired list. Each panel is built by its own rig, and
            // every one of those rigs destroys and recreates its panel — so an explicit list goes stale the moment
            // any single rig is re-run, silently dropping that panel out of the group. The region a panel declares
            // survives the rebuild.
            foreach (var p in FindObjectsByType<RegionPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (p != null && Array.IndexOf(m_AdoptRegions, p.CurrentRegion) >= 0)
                    m_Members.Add(p);

            if (m_Members.Count == 0)
                Debug.LogWarning($"[RegionPanelGroup] '{name}' adopted no panels — none start in {Regions()}. " +
                    "Panels will move one at a time.", this);
        }

        /// <summary>True if this group owns <paramref name="panel"/> — i.e. it must not be moved on its own.</summary>
        public bool Contains(RegionPanel panel) => panel != null && m_Members.Contains(panel);

        /// <summary>
        /// Advance every member one step. Returns the region the first member landed in (for logging), or null if
        /// the move was suppressed.
        /// </summary>
        public string MoveToNext()
        {
            // More than one rig can build a RegionPanelMover, and they share a wand button, so
            // one press can reach here twice in a frame. On a two-entry cycle that lands right back where it
            // started, which reads as a dead button rather than a double move.
            if (Time.frameCount == m_LastMoveFrame)
                return null;
            m_LastMoveFrame = Time.frameCount;
            return MoveToStep(m_Step + 1);
        }

        /// <summary>
        /// Put every member on step <paramref name="step"/> of its own cycle — the addressed move, for callers
        /// that want a specific state rather than "the next one" (the front-wall portal clearing the wall, and
        /// putting it back). Idempotent, and deliberately not subject to <see cref="MoveToNext"/>'s same-frame
        /// guard: that guard exists for two wand movers racing on one button, not for a programmatic move.
        /// Returns the region the first member landed in, or null if the group has no members.
        /// </summary>
        public string MoveToStep(int step)
        {
            m_Step = step;
            string first = null;
            foreach (var p in m_Members)
            {
                if (p == null)
                    continue;
                var cycle = p.MoveCycle;
                if (cycle == null || cycle.Count == 0)
                    continue;
                string to = cycle[((m_Step % cycle.Count) + cycle.Count) % cycle.Count];
                p.MoveTo(to);
                first ??= to;
            }
            return first;
        }

        string Regions() => m_AdoptRegions == null ? "(none)" : string.Join(" / ", m_AdoptRegions);
    }
}
