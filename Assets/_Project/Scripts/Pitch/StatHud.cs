using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Stadium3D.Football;

namespace Stadium3D.Pitch
{
    /// <summary>
    /// A surface's stat readout, composed into that surface's render texture rather than hung in the room.
    /// </summary>
    /// <remarks>
    /// See <see cref="HudLayout"/> for why the readout is a child of the pitch camera instead of a canvas at
    /// the wall plane: coplanar by construction beats coplanar by arithmetic somebody has to maintain.
    ///
    /// <para><b>Each HUD is on a layer only its own surface renders.</b> The walls already have one each —
    /// <c>PitchRightWall</c> and <c>PitchLeftWall</c> exist precisely so a wall can carry content nobody
    /// else sees — and the front wall gets <c>PitchHud</c> for the same reason, since every layer in
    /// <c>BroadcastMask</c> is shared with the floor. Without that, a readout parked a few metres in front
    /// of the broadcast camera falls inside the floor camera's 48.9 m ortho box and appears as text lying
    /// across the field.</para>
    ///
    /// <para><b>The layout is applied here, not baked by the builder.</b> Position and size both come from
    /// the camera's frustum, and <see cref="TeamIsoPitchCamera"/> solves its own field of view in
    /// <c>Awake</c> from the framing scale — so anything measured at build time is measured against a
    /// frustum that no longer exists by the time anyone looks. The builder declares fractions; the frustum
    /// is consulted once, on the camera that actually ends up rendering this.</para>
    ///
    /// <para><b>Nothing here computes per frame.</b> Layout runs once; per frame this sets strings, and only
    /// the ones that changed — a TextMeshPro assignment rebuilds the mesh whether or not the value differs,
    /// and a clock reading "8:42" for sixty frames should cost one rebuild, not sixty.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public abstract class StatHud : MonoBehaviour
    {
        /// <summary>One label and where it belongs, in fractions of the surface.</summary>
        [Serializable]
        public struct Element
        {
            public TMP_Text Label;

            [Tooltip("Position in the frame, 0–1 from bottom-left.")]
            public Vector2 Viewport;

            [Tooltip("Cap height as a fraction of the frame height.")]
            public float Height;
        }

        [SerializeField, Tooltip("Whether the readout is drawn. The wand's Button3 toggles this.")]
        bool m_Visible = true;

        [SerializeField, Tooltip("Metres in front of the camera. Cancels out on screen for a perspective " +
            "camera; chosen only to sit clear of everything the camera is looking at.")]
        float m_PlaneDistance = 3f;

        [SerializeField] List<Element> m_Elements = new List<Element>();

        bool m_LaidOut;

        /// <summary>Whether this readout is currently drawn.</summary>
        public bool Visible => m_Visible;

        /// <summary>The labels this readout owns, with their declared places.</summary>
        public IReadOnlyList<Element> Elements => m_Elements;

        void Start() => LayOut();

        void LateUpdate()
        {
            var driver = FootballSimDriver.Instance;
            var sim = driver != null ? driver.Sim : null;

            if (sim != null) Apply(sim);
        }

        /// <summary>
        /// Place every label from its declared fractions, against the camera that renders this.
        /// </summary>
        /// <remarks>
        /// Public and idempotent: the editor capture tooling has no run loop to call <c>Start</c>, and a
        /// camera that re-solves its framing can ask for the layout again.
        /// </remarks>
        public void LayOut()
        {
            var camera = GetComponentInParent<Camera>();
            if (camera == null) return;

            for (int i = 0; i < m_Elements.Count; i++)
            {
                var element = m_Elements[i];
                if (element.Label == null) continue;

                element.Label.transform.localPosition =
                    HudLayout.LocalPosition(camera, element.Viewport, m_PlaneDistance);
                element.Label.transform.localRotation = Quaternion.identity;

                Fit(element.Label, HudLayout.WorldHeight(camera, element.Height, m_PlaneDistance));
            }

            m_LaidOut = true;
        }

        /// <summary>Declare a label's place. Called by the builder; the layout itself happens later.</summary>
        public void Place(TMP_Text label, Vector2 viewport, float height)
        {
            if (label == null) return;
            m_Elements.Add(new Element { Label = label, Viewport = viewport, Height = height });
        }

        /// <summary>
        /// Fill the readout from the sim. Public because the editor capture tooling has no run loop.
        /// </summary>
        public void Apply(FootballSim sim)
        {
            if (sim == null || !m_Visible) return;
            EnsureLaidOut();

            OnApply(sim);
        }

        /// <summary>Show or hide the whole readout.</summary>
        /// <remarks>
        /// Renderers rather than the GameObject, so this component keeps ticking and can bring itself back —
        /// and so a hidden readout costs nothing at all, since <see cref="Apply"/> returns before formatting
        /// a single string.
        /// </remarks>
        public void SetVisible(bool visible)
        {
            m_Visible = visible;

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                renderer.enabled = visible;
        }

        /// <summary>
        /// Lay out if nothing has yet. For subclasses that are filled directly rather than through
        /// <see cref="Apply(FootballSim)"/> — the capture tooling drives the team walls that way, because
        /// their subject is chosen by a camera that has to settle first.
        /// </summary>
        protected void EnsureLaidOut()
        {
            if (!m_LaidOut) LayOut();
        }

        /// <summary>Fill this surface's own fields.</summary>
        protected abstract void OnApply(FootballSim sim);

        /// <summary>Assign only on change, because TextMeshPro rebuilds its mesh either way.</summary>
        protected static void Set(TMP_Text label, string value)
        {
            if (label == null || label.text == value) return;
            label.text = value;
        }

        /// <summary>
        /// Scale the font so a line of text stands <paramref name="worldHeight"/> metres tall.
        /// </summary>
        /// <remarks>
        /// Measured against a reference glyph rather than the live string, so the size is a property of the
        /// slot and not of whatever happens to be in it — otherwise "3RD &amp; 27" and "1ST &amp; 10" would
        /// be set at different sizes, and the readout would breathe as the drive went on.
        /// </remarks>
        static void Fit(TMP_Text label, float worldHeight)
        {
            if (label == null || worldHeight <= 0f) return;

            label.fontSize = 10f;
            var reference = label.GetPreferredValues("0");
            if (reference.y <= 0.0001f) return;

            label.fontSize = 10f * (worldHeight / reference.y);
        }
    }
}
