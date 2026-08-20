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
    /// <para><b>The layout is applied here, and re-applied whenever the frustum moves under it.</b>
    /// Position and size both come from the camera's frustum, and a team wall's field of view is not a
    /// constant: <see cref="TeamIsoPitchCamera"/> re-solves it continuously, because holding a subject at a
    /// fixed rendered height whatever the distance is exactly what the framing scale means. Laying out once
    /// therefore fails the same way baking in the builder does, one level further down — the first frame is
    /// right and every frame after it is measured against a frustum that no longer exists. It showed up as
    /// a lower third that had shrunk to nothing and drifted into the middle of the wall. So the frustum is
    /// remembered, and the layout redone when it changes.</para>
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

        [SerializeField, Min(1.05f), Tooltip("Where the readout sits, as a multiple of the camera's near " +
            "clip plane. Cancels out on screen; it only decides what can get in front of the text.")]
        float m_NearPlaneMargin = 1.5f;

        [SerializeField] List<Element> m_Elements = new List<Element>();

        bool m_LaidOut;
        bool m_LaidOutOrthographic;
        float m_LaidOutSize;
        float m_LaidOutAspect;

        /// <summary>Whether this readout is currently drawn.</summary>
        public bool Visible => m_Visible;

        /// <summary>The labels this readout owns, with their declared places.</summary>
        public IReadOnlyList<Element> Elements => m_Elements;

        void Start() => LayOut();

        // No per-frame layout call: EnsureLaidOut runs inside Apply, which is where the frame's work is.

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

            // Just past the near plane, not a comfortable-looking few metres out.
            //
            // A fixed distance is fine until the camera tilts down, and a team wall tilts down constantly —
            // it is booming around a player. The turf at the bottom of the frame is then nearer than the
            // readout, and the ground draws straight over the lower third. Measured, the card was at full
            // alpha with the right text at the right place, and simply behind the field. For a perspective
            // camera the distance cancels out on screen, so there is nothing to trade away by moving the
            // plane as close as it can legally sit.
            float distance = camera.nearClipPlane * Mathf.Max(1.05f, m_NearPlaneMargin);

            for (int i = 0; i < m_Elements.Count; i++)
            {
                var element = m_Elements[i];
                if (element.Label == null) continue;

                element.Label.transform.localPosition =
                    HudLayout.LocalPosition(camera, element.Viewport, distance);
                element.Label.transform.localRotation = Quaternion.identity;

                Fit(element.Label, HudLayout.WorldHeight(camera, element.Height, distance));
            }

            m_LaidOut = true;
            m_LaidOutOrthographic = camera.orthographic;
            m_LaidOutSize = camera.orthographic ? camera.orthographicSize : camera.fieldOfView;
            m_LaidOutAspect = camera.aspect;
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
        /// Lay out if nothing has yet, or if the frustum has moved since the last time.
        /// </summary>
        /// <remarks>
        /// The comparison is against the three things <see cref="HudLayout"/> actually reads, so a camera
        /// that only pans or dollies costs a check and nothing else. A wall that is booming in on a
        /// receiver re-measures, which is a handful of transform writes and one text measurement per label
        /// — the alternative is a readout that is correct on exactly one frame.
        /// </remarks>
        protected void EnsureLaidOut()
        {
            var camera = GetComponentInParent<Camera>();
            if (camera == null) return;

            float size = camera.orthographic ? camera.orthographicSize : camera.fieldOfView;

            if (m_LaidOut &&
                camera.orthographic == m_LaidOutOrthographic &&
                Mathf.Approximately(size, m_LaidOutSize) &&
                Mathf.Approximately(camera.aspect, m_LaidOutAspect))
                return;

            LayOut();
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
