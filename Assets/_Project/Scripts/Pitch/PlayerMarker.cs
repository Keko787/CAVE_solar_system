using UnityEngine;
using Stadium3D.Football;

namespace Stadium3D.Pitch
{
    /// <summary>
    /// How a player is drawn. The swap seam the brief asked for: stylized markers now, rigged humanoids
    /// later, with the driver only ever calling <see cref="Apply"/> and <see cref="SetEmphasis"/>.
    /// </summary>
    public interface IPlayerMarkerView
    {
        /// <summary>Bind this view to a roster entry, once, at build time.</summary>
        void Bind(PlayerIdentity identity);

        /// <summary>Place the marker for this frame.</summary>
        void Apply(in PlayerFrame frame);

        /// <summary>
        /// How prominently this player reads on the wall that is looking at them.
        /// </summary>
        void SetEmphasis(MarkerEmphasis emphasis);
    }

    /// <summary>What a wall wants to say about a player right now.</summary>
    public enum MarkerEmphasis
    {
        /// <summary>Ordinary. Team colour, full opacity.</summary>
        Normal = 0,

        /// <summary>The player this wall is following. Ring on, full colour.</summary>
        Focused = 1,

        /// <summary>A teammate of the focused player. Present but quieter.</summary>
        Dimmed = 2,

        /// <summary>An opponent. Present enough to read the play, not enough to compete for attention.</summary>
        Ghosted = 3,
    }

    /// <summary>
    /// The stylized body: a team-tinted capsule, a facing chevron, a jersey-number billboard, a blob shadow,
    /// and a highlight ring when a wall is following this player.
    /// </summary>
    /// <remarks>
    /// Markers rather than humanoids because a rendered figure buys nothing at CAVE projection resolution
    /// and costs a great deal across four simultaneous camera views — and because the demo's own arithmetic
    /// says the floor cannot carry jersey identity at any workable scale, so identity has to read from the
    /// side walls where a player is life-size.
    ///
    /// <para><b>No <see cref="MaterialPropertyBlock"/>.</b> The project has <c>m_UseSRPBatcher: 1</c> and
    /// <c>m_SupportsDynamicBatching: 0</c>, and an MPB breaks the SRP batcher — which with 22 bodies across
    /// 12 camera renders a frame is exactly the wrong trade. Emphasis is expressed by swapping between a
    /// small set of <em>shared</em> materials instead, so every marker in a given state batches together.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class PlayerMarker : MonoBehaviour, IPlayerMarkerView
    {
        [Header("Parts")]
        [SerializeField, Tooltip("The body capsule. Tinted by team and by emphasis.")]
        Renderer m_Body;

        [SerializeField, Tooltip("Flat chevron on the turf showing which way the player faces.")]
        Renderer m_Chevron;

        [SerializeField, Tooltip("Soft quad under the body. Stands in for a real cast shadow, which the " +
            "shared URP asset cannot provide over a 110 m field.")]
        Renderer m_BlobShadow;

        [SerializeField, Tooltip("Ring drawn when a wall is following this player.")]
        Renderer m_FocusRing;

        [SerializeField, Tooltip("Jersey number, billboarded toward the camera that is looking.")]
        TextMesh m_Number;

        [Header("Shared materials — never MaterialPropertyBlocks, which break the SRP batcher")]
        [SerializeField] Material m_HomeNormal;
        [SerializeField] Material m_HomeDimmed;
        [SerializeField] Material m_HomeGhosted;
        [SerializeField] Material m_AwayNormal;
        [SerializeField] Material m_AwayDimmed;
        [SerializeField] Material m_AwayGhosted;

        PlayerIdentity m_Identity;
        MarkerEmphasis m_Emphasis = MarkerEmphasis.Normal;

        /// <summary>The roster entry this marker draws, or null before it is bound.</summary>
        public PlayerIdentity Identity => m_Identity;

        /// <inheritdoc/>
        public void Bind(PlayerIdentity identity)
        {
            m_Identity = identity;
            name = identity == null ? "Marker" : $"#{identity.Number} {identity.Name}";

            if (m_Number != null)
                m_Number.text = identity == null ? "" : identity.Number.ToString();

            SetEmphasis(MarkerEmphasis.Normal);
        }

        /// <inheritdoc/>
        public void Apply(in PlayerFrame frame)
        {
            transform.SetPositionAndRotation(frame.Position, frame.Rotation);

            // The chevron reads as a direction only while there is a direction to read. A body standing in
            // its stance with a chevron still blazing looks like it is about to move and never does.
            if (m_Chevron != null)
            {
                bool moving = frame.SpeedMps > 0.6f;
                if (m_Chevron.enabled != moving)
                    m_Chevron.enabled = moving;
            }
        }

        /// <summary>
        /// Turn the jersey number to face a camera. A number parented to a body inherits that body's facing,
        /// so any player running away from a camera shows their number mirrored — which is illegible and
        /// reads as a rendering fault rather than as a player's back.
        /// </summary>
        /// <remarks>
        /// Called per-camera rather than once in <c>LateUpdate</c>, because the four wall views look at the
        /// same 22 bodies from four different directions and a single orientation cannot be right for more
        /// than one of them. <see cref="Camera.onPreCull"/> gives each camera its own chance to orient the
        /// numbers before it draws.
        /// </remarks>
        public void FaceNumberAt(Camera camera)
        {
            if (m_Number == null || camera == null) return;

            Vector3 toCamera = camera.transform.position - m_Number.transform.position;
            toCamera.y = 0f;                                    // upright: never tilt a jersey number
            if (toCamera.sqrMagnitude < 1e-4f) return;

            m_Number.transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
        }

        /// <inheritdoc/>
        public void SetEmphasis(MarkerEmphasis emphasis)
        {
            m_Emphasis = emphasis;

            if (m_Body != null)
            {
                var mat = MaterialFor(emphasis);
                if (mat != null && m_Body.sharedMaterial != mat)
                    m_Body.sharedMaterial = mat;
            }

            if (m_FocusRing != null)
                m_FocusRing.enabled = emphasis == MarkerEmphasis.Focused;

            // A ghosted opponent keeps its blob shadow — losing it makes the body read as floating, which is
            // more distracting than the opponent was.
            if (m_Number != null)
                m_Number.gameObject.SetActive(emphasis != MarkerEmphasis.Ghosted);
        }

        Material MaterialFor(MarkerEmphasis emphasis)
        {
            bool home = m_Identity == null || m_Identity.Side == TeamSide.Home;
            switch (emphasis)
            {
                case MarkerEmphasis.Ghosted: return home ? m_HomeGhosted : m_AwayGhosted;
                case MarkerEmphasis.Dimmed:  return home ? m_HomeDimmed : m_AwayDimmed;
                default:                     return home ? m_HomeNormal : m_AwayNormal;
            }
        }

        /// <summary>Wire the parts up from a builder, so the prefab does not have to be hand-assembled.</summary>
        public void Configure(Renderer body, Renderer chevron, Renderer blobShadow, Renderer focusRing, TextMesh number)
        {
            m_Body = body;
            m_Chevron = chevron;
            m_BlobShadow = blobShadow;
            m_FocusRing = focusRing;
            m_Number = number;
        }

        /// <summary>Hand the marker the shared material set it switches between.</summary>
        public void ConfigureMaterials(Material homeNormal, Material homeDimmed, Material homeGhosted,
                                       Material awayNormal, Material awayDimmed, Material awayGhosted)
        {
            m_HomeNormal = homeNormal;
            m_HomeDimmed = homeDimmed;
            m_HomeGhosted = homeGhosted;
            m_AwayNormal = awayNormal;
            m_AwayDimmed = awayDimmed;
            m_AwayGhosted = awayGhosted;
        }
    }
}
