using System.Collections.Generic;
using UnityEngine;

namespace Stadium3D.Space
{
    /// <summary>
    /// The one clock every <see cref="PlanetOrbit"/> in the scene reads. Owns the time, the pause, and the order
    /// the bodies are placed in.
    /// </summary>
    /// <remarks>
    /// <para>Ordering is the reason this exists rather than each orbit ticking itself. The Moon's centre is Earth,
    /// so the Moon has to be placed after Earth has moved — otherwise it trails a frame behind and wobbles. Unity
    /// gives no ordering guarantee between instances of the same component, so the driver sorts by how deep each
    /// body sits in the chain of centres and walks that order in <c>LateUpdate</c>, after any camera rig or wand
    /// interaction has finished moving things.</para>
    ///
    /// <para>Out of play mode the clock does not advance, but the bodies are still placed. That makes the layout
    /// visible and scrubbable in the editor — drag <see cref="Seconds"/> and the whole system sweeps — without a
    /// running scene quietly rewriting transforms the moment someone opens it.</para>
    /// </remarks>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Stadium3D/Space/Solar System Driver")]
    public sealed class SolarSystemDriver : MonoBehaviour
    {
        [Tooltip("Seconds on the system clock. Scrub this in the editor to sweep the whole system.")]
        [SerializeField] double m_Seconds;

        [Tooltip("Multiplies real time. 0 freezes, negative runs the system backwards.")]
        [SerializeField] float m_TimeScale = 1f;

        [Tooltip("Leave empty to drive every orbit in the scene; set it to drive only one subtree.")]
        [SerializeField] Transform m_Scope;

        readonly List<PlanetOrbit> m_Ordered = new List<PlanetOrbit>();
        bool m_NeedsRebuild = true;

        /// <summary>Current position on the system clock, in seconds.</summary>
        public double Seconds
        {
            get => m_Seconds;
            set => m_Seconds = value;
        }

        /// <summary>Multiplier on real time. Zero freezes the system where it stands.</summary>
        public float TimeScale
        {
            get => m_TimeScale;
            set => m_TimeScale = value;
        }

        /// <summary>Forces the driver to re-collect and re-sort its orbits on the next tick.</summary>
        public void SetDirty() => m_NeedsRebuild = true;

        void OnEnable() => SetDirty();

        void OnValidate() => SetDirty();

        void OnTransformChildrenChanged() => SetDirty();

        void LateUpdate()
        {
            if (m_NeedsRebuild)
                Rebuild();

            if (Application.isPlaying)
                m_Seconds += Time.deltaTime * m_TimeScale;

            for (int i = 0; i < m_Ordered.Count; i++)
            {
                var orbit = m_Ordered[i];

                // Something in the scene may have been deleted since the last rebuild.
                if (orbit == null)
                {
                    SetDirty();
                    continue;
                }

                orbit.Evaluate(m_Seconds);
            }
        }

        /// <summary>Collects the orbits in scope and sorts them so every centre is placed before its dependants.</summary>
        void Rebuild()
        {
            m_NeedsRebuild = false;
            m_Ordered.Clear();

            if (m_Scope != null)
                m_Scope.GetComponentsInChildren(true, m_Ordered);
            else
                m_Ordered.AddRange(FindObjectsByType<PlanetOrbit>(FindObjectsInactive.Include, FindObjectsSortMode.None));

            m_Ordered.Sort((a, b) => CentreDepth(a).CompareTo(CentreDepth(b)));
        }

        /// <summary>
        /// How many orbiting bodies sit between this one and something that does not orbit. The Sun's planets
        /// score 0, their moons 1. Capped so a centre chain accidentally wired into a loop cannot hang the room.
        /// </summary>
        static int CentreDepth(PlanetOrbit orbit)
        {
            const int maxHops = 16;

            int depth = 0;
            Transform centre = orbit != null ? orbit.Centre : null;

            for (int hop = 0; hop < maxHops && centre != null; hop++)
            {
                var parent = centre.GetComponent<PlanetOrbit>();
                if (parent == null)
                    break;

                depth++;
                centre = parent.Centre;
            }

            return depth;
        }
    }
}
