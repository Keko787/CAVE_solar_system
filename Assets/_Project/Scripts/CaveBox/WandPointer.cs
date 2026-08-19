using Stadium3D.Operators;
using UnityEngine;

namespace Stadium3D.CaveBox
{
    /// <summary>
    /// A reusable ray-driven UI cursor. Each frame it raycasts from its ray origin (same idiom as
    /// <c>FeedFocusController</c>) against the WandUI layer, hover-highlights the nearest <see cref="WandButton"/>,
    /// parks a small reticle at the hit point, and clicks the hovered button when its operator's click button is
    /// pressed. Purely geometric (physics colliders, not the UGUI EventSystem), so it is robust across the CAVE's
    /// multi-camera MiddleVR rig. No-op before the ray origin resolves.
    ///
    /// <b>One instance per operator.</b> Which operator drives it is a serialized <see cref="OperatorRole"/>, and
    /// hover is announced under this component's own identity, so the primary operator's wand cursor and the
    /// secondary operator's pad cursor can sit on the same chip without either clearing the other's highlight.
    /// Give each instance its own <see cref="m_ReticleColor"/> so the two are distinguishable in the room.
    /// </summary>
    /// <remarks>
    /// When the primary role has no registered source — every scene built before the operator layer existed —
    /// this falls back to <see cref="WandInput.Shared"/>, so the wand cursor keeps working with no rewiring.
    /// The fallback is deliberately primary-only: an unregistered <em>secondary</em> cursor must stay inert
    /// rather than quietly becoming a second wand cursor.
    /// </remarks>
    public class WandPointer : MonoBehaviour
    {
        [SerializeField, Tooltip("Which operator drives this cursor. Primary = the tracked MiddleVR wand; " +
            "Secondary = the gamepad (its ray origin is driven by the virtual-ray driver).")]
        OperatorRole m_Role = OperatorRole.Primary;

        [SerializeField, Tooltip("Transform the cursor ray is cast from — the wand. Attach to the MiddleVR " +
            "HandNode (MVRAttachToNode), like FeedFocusController. Falls back to Camera.main if empty.")]
        Transform m_WandRayOrigin;

        [SerializeField, Tooltip("Button that clicks the hovered chip, in this operator's own index space. " +
            "0 = the wand trigger — contextual, since it only fires when a chip is under the cursor, so it " +
            "coexists with wall selection (which acts on the wall panels behind the chips).")]
        int m_ClickButton;

        [SerializeField, Min(0f)] float m_MaxDistance = 20f;

        [SerializeField, Tooltip("Layers the cursor ray considers — the WandUI layer the chips live on.")]
        LayerMask m_Mask = ~0;

        [SerializeField, Tooltip("Reticle tint parked at the hovered chip. Give each operator's cursor its " +
            "own colour — cyan for the wand, amber for the pad — so two cursors never read as one.")]
        Color m_ReticleColor = new(0.3f, 0.85f, 1f, 1f);
        [SerializeField, Min(0f)] float m_ReticleSize = 0.014f;

        [SerializeField, Tooltip("Go inert while this operator's device is absent. Leave OFF for the wand " +
            "(its cursor works in the editor with no MiddleVR kernel); turn ON for the pad, so an unplugged " +
            "controller doesn't leave a dead reticle parked in the room.")]
        bool m_HideWhenDisconnected;

        readonly RaycastHit[] m_Hits = new RaycastHit[16];
        WandButton m_Hover;
        Transform m_Reticle;

        /// <summary>Which operator drives this cursor.</summary>
        public OperatorRole Role => m_Role;

        /// <summary>
        /// Reassign this cursor's operator. For rig builders spawning a second cursor; in a hand-built
        /// scene set it in the inspector instead. Releases any chip held under the old role first, so the
        /// switch can't strand a highlight.
        /// </summary>
        public void SetRole(OperatorRole role)
        {
            if (m_Role == role)
                return;
            if (m_Hover != null)
            {
                m_Hover.SetHover(this, false);
                m_Hover = null;
            }
            m_Role = role;
        }

        /// <summary>The chip currently under this cursor, or null.</summary>
        public WandButton Hovered => m_Hover;

        /// <summary>
        /// This operator's input: whatever claimed the role, or the shared wand read when the primary role
        /// is unclaimed. Null for an unclaimed secondary role, which keeps that cursor inert.
        /// </summary>
        public IOperatorInput Input
        {
            get
            {
                var input = OperatorInputRegistry.Get(m_Role);
                if (input != null)
                    return input;
                return m_Role == OperatorRole.Primary ? WandInput.Shared : null;
            }
        }

        void Update()
        {
            var input = Input;
            var origin = m_WandRayOrigin != null ? m_WandRayOrigin : ResolveCamera();
            if (origin == null || (m_HideWhenDisconnected && (input == null || !input.IsConnected)))
            {
                SetHover(null, Vector3.zero, hit: false);
                return;
            }

            var ray = new Ray(origin.position, origin.forward);
            var target = Pick(ray, out Vector3 point);
            SetHover(target, point, target != null);

            if (target != null && input != null && input.PressedThisFrame(m_ClickButton))
                target.Click();
        }

        // Let go of the chip when this cursor stops running, so a disabled or destroyed cursor can't leave
        // a chip stuck lit for the other operator. Deliberately does not go through SetHover: that would
        // build the reticle on the way out if it didn't exist yet, during teardown.
        void OnDisable()
        {
            if (m_Hover != null)
            {
                m_Hover.SetHover(this, false);
                m_Hover = null;
            }
            if (m_Reticle != null)
                m_Reticle.gameObject.SetActive(false);
        }

        // Nearest WandButton the ray hits (walls/other colliders on other layers are excluded by the mask).
        WandButton Pick(Ray ray, out Vector3 point)
        {
            point = Vector3.zero;
            int count = Physics.RaycastNonAlloc(ray, m_Hits, m_MaxDistance, m_Mask, QueryTriggerInteraction.Collide);
            WandButton best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var btn = m_Hits[i].collider.GetComponentInParent<WandButton>();
                if (btn == null || m_Hits[i].distance >= bestDist)
                    continue;
                best = btn;
                bestDist = m_Hits[i].distance;
                point = m_Hits[i].point;
            }
            return best;
        }

        void SetHover(WandButton next, Vector3 point, bool hit)
        {
            if (next != m_Hover)
            {
                // Announced under this component's identity: the chip stays lit while the other operator's
                // cursor is still on it.
                if (m_Hover != null) m_Hover.SetHover(this, false);
                if (next != null) next.SetHover(this, true);
                m_Hover = next;
            }

            EnsureReticle();
            if (m_Reticle == null)
                return;
            if (hit)
            {
                var origin = m_WandRayOrigin != null ? m_WandRayOrigin : ResolveCamera();
                Vector3 toward = origin != null ? (origin.position - point).normalized : Vector3.up;
                m_Reticle.gameObject.SetActive(true);
                m_Reticle.position = point + toward * 0.006f;   // nudge off the panel so it doesn't z-fight
            }
            else
            {
                m_Reticle.gameObject.SetActive(false);
            }
        }

        void EnsureReticle()
        {
            if (m_Reticle != null)
                return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"{m_Role}Reticle";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.localScale = Vector3.one * Mathf.Max(0.001f, m_ReticleSize);

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader) { name = $"{m_Role}Reticle" };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", m_ReticleColor);
            else mat.color = m_ReticleColor;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.SetActive(false);
            m_Reticle = go.transform;
        }

        void OnDestroy()
        {
            if (m_Reticle != null)
                Destroy(m_Reticle.gameObject);
        }

        static Transform ResolveCamera()
        {
            var cam = Camera.main;   // may be null/untagged under MiddleVR — guarded by the caller
            return cam != null ? cam.transform : null;
        }
    }
}
