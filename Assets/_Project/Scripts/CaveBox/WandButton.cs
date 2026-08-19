using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Stadium3D.CaveBox
{
    /// <summary>
    /// A world-space UGUI element a cursor can point at and click (see <see cref="WandPointer"/>).
    /// It is <b>not</b> driven by the Unity EventSystem — instead a <see cref="BoxCollider"/> sized to its
    /// <see cref="RectTransform"/> (on a "WandUI" layer) makes it a <c>Physics.RaycastAll</c> target, matching
    /// the project's established wand-ray idiom (<c>FeedFocusController</c> / <c>RegionPanelMover</c>). The
    /// pointer calls <see cref="SetHover(object, bool)"/> as its ray enters/leaves and <see cref="Click"/> on
    /// the click button; state is reflected by tinting <see cref="m_Background"/>. Works as a toggle chip via
    /// <see cref="SetSelected"/> or a momentary button (just subscribe to <see cref="Clicked"/>).
    ///
    /// Hover is tracked <b>per cursor</b>, because the CAVE now has two operators pointing at the same UI at
    /// the same time. With a single flag, whichever operator looked away last would clear the other one's
    /// highlight — the chip would go dark while someone was still aiming at it. Each cursor announces itself,
    /// and the chip stays hovered until every cursor has left.
    /// </summary>
    /// <remarks>
    /// <see cref="SetFocused"/> is a separate visual from hover: hover means "a ray is on me", focus means
    /// "the gamepad's focus cursor has selected me". They are deliberately different marks — a ring versus a
    /// tint — so that with both operators working at once you can tell which is which. Focus is what the G3
    /// focus navigation will drive; nothing sets it yet.
    /// </remarks>
    [RequireComponent(typeof(RectTransform))]
    public class WandButton : MonoBehaviour
    {
        [SerializeField, Tooltip("Chip background tinted by state. Auto-found on this object if empty.")]
        Image m_Background;
        [SerializeField] Color m_NormalColor = new(0.16f, 0.18f, 0.22f, 1f);
        [SerializeField] Color m_HoverColor = new(0.26f, 0.30f, 0.36f, 1f);
        [SerializeField] Color m_SelectedColor = new(0.15f, 0.45f, 0.75f, 1f);
        [SerializeField, Tooltip("Collider depth (canvas units) so the wand ray reliably intersects the chip.")]
        float m_ColliderDepth = 24f;

        [Header("Focus ring (gamepad focus cursor)")]
        [SerializeField, Tooltip("Ring tint. Amber by default so the secondary operator's focus reads as " +
            "distinct from the primary operator's cyan reticle at a glance.")]
        Color m_FocusColor = new(1f, 0.72f, 0.2f, 1f);
        [SerializeField, Min(0f), Tooltip("Ring thickness in canvas units. Drawn OUTSIDE the chip, so it " +
            "never covers the label.")]
        float m_FocusRingThickness = 4f;

        /// <summary>Raised when a cursor clicks this button.</summary>
        public event Action Clicked;

        // Which cursors are currently on this chip. A set rather than a count, so a cursor that is disabled
        // or destroyed mid-hover can be removed by identity instead of leaking a count that never returns.
        readonly HashSet<object> m_Hovering = new();

        // Stands in for the cursor-less SetHover(bool) overload, so both overloads share one state model.
        static readonly object s_AnonymousCursor = new();

        bool m_Selected;
        bool m_Focused;
        RectTransform[] m_FocusRing;
        Transform m_FocusGroup;
        bool m_FocusGroupResolved;

        /// <summary>Toggle state.</summary>
        public bool IsSelected => m_Selected;

        /// <summary>True while at least one cursor is pointing at this chip.</summary>
        public bool IsHovered => m_Hovering.Count > 0;

        /// <summary>How many cursors are currently on this chip.</summary>
        public int HoverCount => m_Hovering.Count;

        /// <summary>True while the focus cursor has selected this chip.</summary>
        public bool IsFocused => m_Focused;

        /// <summary>
        /// The panel this chip belongs to, for focus navigation — its owning canvas. Directional steps stay
        /// inside one group, because chips on the front wall and chips on the floor share no sensible "up";
        /// moving between panels is the bumpers' job. Null when the chip has no canvas above it.
        /// </summary>
        public Transform FocusGroup
        {
            get
            {
                if (!m_FocusGroupResolved)
                {
                    // Include inactive parents: a chip on a hidden panel still belongs to that panel, and
                    // resolving to null here would cache the wrong group for the rest of the session.
                    var canvas = GetComponentInParent<Canvas>(true);
                    m_FocusGroup = canvas != null ? canvas.transform : null;
                    m_FocusGroupResolved = true;
                }
                return m_FocusGroup;
            }
        }

        void OnEnable() => UIFocusGraph.Register(this);

        // Leaving the graph on disable is what makes a hidden panel's chips unreachable — PanelModeSwitcher
        // already deactivates them, so focus cannot walk onto something nobody can see.
        void OnDisable()
        {
            UIFocusGraph.Unregister(this);
            m_Hovering.Clear();   // a chip going away mid-hover must not come back still lit
            SetFocused(false);
            Refresh();
        }

        void Awake()
        {
            if (m_Background == null)
                m_Background = GetComponent<Image>();
            EnsureCollider();
            Refresh();
        }

        // A trigger BoxCollider sized to the RectTransform (pivot-independent), so the wand ray can hit it.
        void EnsureCollider()
        {
            var r = ((RectTransform)transform).rect;
            var box = GetComponent<BoxCollider>();
            if (box == null)
                box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(r.center.x, r.center.y, 0f);
            box.size = new Vector3(Mathf.Max(1f, r.width), Mathf.Max(1f, r.height), m_ColliderDepth);
        }

        /// <summary>Override the selected-state tint (e.g. green stop-type chips vs blue view chips).</summary>
        public void SetSelectedColor(Color c)
        {
            m_SelectedColor = c;
            Refresh();
        }

        /// <summary>Override the normal (unselected, unhovered) tint — e.g. a table row keeping its zebra /
        /// highlight background while still hover-brightening and being clickable.</summary>
        public void SetNormalColor(Color c)
        {
            m_NormalColor = c;
            Refresh();
        }

        /// <summary>Override the focus-ring tint — one colour per operator, should a third cursor appear.</summary>
        public void SetFocusColor(Color c)
        {
            m_FocusColor = c;
            if (m_FocusRing == null)
                return;
            for (int i = 0; i < m_FocusRing.Length; i++)
                if (m_FocusRing[i] != null && m_FocusRing[i].TryGetComponent(out Image image))
                    image.color = m_FocusColor;
        }

        /// <summary>Toggle-state, reflected in the tint. Does not raise <see cref="Clicked"/>.</summary>
        public void SetSelected(bool on)
        {
            if (m_Selected == on)
                return;
            m_Selected = on;
            Refresh();
        }

        /// <summary>
        /// Called by a cursor as its ray enters (true) or leaves (false) this chip. Pass the cursor itself
        /// as <paramref name="cursor"/> — the chip stays hovered until <em>every</em> cursor has left, so
        /// one operator looking away cannot clear another operator's highlight.
        /// </summary>
        public void SetHover(object cursor, bool on)
        {
            var key = cursor ?? s_AnonymousCursor;
            bool changed = on ? m_Hovering.Add(key) : m_Hovering.Remove(key);
            if (changed)
                Refresh();
        }

        /// <summary>
        /// Single-cursor convenience for callers with no cursor identity (tests, editor tools). Shares one
        /// state model with <see cref="SetHover(object, bool)"/> under an anonymous cursor, so mixing the
        /// two cannot desynchronise the chip.
        /// </summary>
        public void SetHover(bool on) => SetHover(s_AnonymousCursor, on);

        /// <summary>Clear one cursor's hover — called from a cursor's OnDisable, so a cursor that goes away
        /// mid-hover cannot leave the chip stuck lit.</summary>
        public void ClearHover(object cursor) => SetHover(cursor, false);

        /// <summary>
        /// Mark this chip as the focus cursor's current selection. Drawn as a ring rather than a tint, so it
        /// stays legible at the same time as another operator's hover.
        /// </summary>
        public void SetFocused(bool on)
        {
            if (m_Focused == on)
                return;
            m_Focused = on;
            ApplyFocusRing();
        }

        /// <summary>Invoked by a cursor on its click button while this chip is hovered.</summary>
        public void Click() => Clicked?.Invoke();

        void Refresh()
        {
            if (m_Background == null)
                return;
            Color baseColor = m_Selected ? m_SelectedColor : m_NormalColor;
            m_Background.color = IsHovered ? Color.Lerp(baseColor, Color.white, 0.18f) : baseColor;
        }

        void ApplyFocusRing()
        {
            if (m_Focused)
                EnsureFocusRing();
            if (m_FocusRing == null)
                return;
            for (int i = 0; i < m_FocusRing.Length; i++)
                if (m_FocusRing[i] != null)
                    m_FocusRing[i].gameObject.SetActive(m_Focused);
        }

        // Four bars hugging the chip's outer edge. Built from plain stretched RectTransforms, so the ring
        // needs no sprite and no shader, and it sits OUTSIDE the chip rect so it never covers the label —
        // children always draw over their parent in UGUI, so an inset ring would.
        void EnsureFocusRing()
        {
            if (m_FocusRing != null)
                return;

            float t = Mathf.Max(0f, m_FocusRingThickness);
            m_FocusRing = new RectTransform[4];
            // Top and bottom span the full width plus both corners; left and right fill the gap between them.
            m_FocusRing[0] = MakeBar("FocusRing_Top", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 0f), new Vector2(0f, t), new Vector2(2f * t, t));
            m_FocusRing[1] = MakeBar("FocusRing_Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 1f), new Vector2(0f, -t), new Vector2(2f * t, t));
            m_FocusRing[2] = MakeBar("FocusRing_Left", new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(1f, 0.5f), new Vector2(-t, 0f), new Vector2(t, 0f));
            m_FocusRing[3] = MakeBar("FocusRing_Right", new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(0f, 0.5f), new Vector2(t, 0f), new Vector2(t, 0f));
        }

        RectTransform MakeBar(string barName, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(barName, typeof(RectTransform), typeof(Image));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;

            var image = go.GetComponent<Image>();
            image.color = m_FocusColor;
            image.raycastTarget = false;   // cursors hit the chip's BoxCollider, not this
            go.SetActive(false);
            return rect;
        }
    }
}
