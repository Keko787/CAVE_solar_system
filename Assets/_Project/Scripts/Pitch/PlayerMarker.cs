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

        [SerializeField, Tooltip("Focus ring the home wall sees. On PitchRightWall.")]
        Renderer m_FocusRingRight;

        [SerializeField, Tooltip("Focus ring the away wall sees. On PitchLeftWall.")]
        Renderer m_FocusRingLeft;

        [SerializeField, Tooltip("Jersey number, billboarded toward the camera that is looking.")]
        TextMesh m_Number;

        [Header("Shared materials — never MaterialPropertyBlocks, which break the SRP batcher")]
        [SerializeField] Material m_HomeNormal;
        [SerializeField] Material m_HomeDimmed;
        [SerializeField] Material m_HomeGhosted;
        [SerializeField] Material m_AwayNormal;
        [SerializeField] Material m_AwayDimmed;
        [SerializeField] Material m_AwayGhosted;

        [SerializeField, Tooltip("Roster id this marker draws. Only the key is stored — the identity itself " +
            "is resolved from the live roster at load, so a roster edit never leaves a stale name on a body.")]
        int m_PlayerId;

        PlayerIdentity m_Identity;
        MarkerEmphasis m_Emphasis = MarkerEmphasis.Normal;

        /// <summary>The roster entry this marker draws, or null before it is bound.</summary>
        /// <remarks>
        /// Deliberately not serialized, and the consequence is worth recording. A marker is bound at build
        /// time, and a plain object reference lives only until the scene is saved — so every marker came back
        /// from a reload with a null identity, the driver registered none of them, and twenty-two correctly
        /// built bodies stood on the field while nothing ever moved them. <see cref="PlayerId"/> is the part
        /// that survives; the driver re-binds from it on Awake.
        /// </remarks>
        public PlayerIdentity Identity => m_Identity;

        /// <summary>The roster id this marker was built for. Survives the scene save; the identity does not.</summary>
        public int PlayerId => m_PlayerId;

        /// <summary>
        /// Where a jersey number sits when nothing is pushing it, metres above the player's feet.
        /// </summary>
        /// <remarks>
        /// Clear of a 1.85 m body with a little air, so the number reads against turf rather than against
        /// the shoulders. A constant because the builder places it and <see cref="FaceNumberAt"/> moves it,
        /// and two copies of 2.15 in different files is one edit away from numbers that drift apart.
        /// </remarks>
        public const float NumberRestHeight = 2.15f;

        /// <inheritdoc/>
        public void Bind(PlayerIdentity identity)
        {
            m_Identity = identity;
            m_PlayerId = identity == null ? 0 : identity.Id;
            name = identity == null ? "Marker" : $"#{identity.Number} {identity.Name}";

            if (m_Number != null)
                m_Number.text = identity == null ? "" : identity.Number.ToString();

            SetEmphasis(MarkerEmphasis.Normal);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <b>Local</b> space, not world. The sim knows nothing about where the pitch has been parked — it
        /// works entirely in field coordinates with the 50-yard line at the origin — and in the CAVE scene
        /// the pitch root sits 1500 m below the room so the field stays outside the display cameras' far
        /// plane. Writing a world position here would drag every body back to the room and leave the turf
        /// behind, which is exactly what it did the first time.
        /// </remarks>
        public void Apply(in PlayerFrame frame)
        {
            transform.SetLocalPositionAndRotation(frame.Position, frame.Rotation);

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
        public void FaceNumberAt(Camera camera, float lift = 0f)
        {
            if (m_Number == null || camera == null) return;

            // Height is set here rather than left where the builder put it, because it is per camera: two
            // walls looking at the same player from different distances want the number in different
            // places. See JerseyNumbers for why the lift is measured against a reference depth.
            m_Number.transform.localPosition = new Vector3(0f, NumberRestHeight + lift, 0f);

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

        /// <summary>
        /// Light or clear this player's ring on <b>one wall</b>, without touching the other.
        /// </summary>
        /// <remarks>
        /// Focus is per wall, not per player: the two team walls follow different people at the same time,
        /// and in M6 the wand will move each independently. One ring shared between them could only ever
        /// show one of those answers, and would show it to the wall that did not ask.
        ///
        /// <para>Deliberately separate from <see cref="SetEmphasis"/>. Emphasis is a property of the player
        /// — what everyone thinks of them — while this is a property of a viewer. Folding them together is
        /// what makes "focused" mean two different things and one of them silently win.</para>
        /// </remarks>
        public void SetWallFocus(TeamSide wall, bool focused)
        {
            var ring = wall == TeamSide.Home ? m_FocusRingRight : m_FocusRingLeft;
            if (ring != null && ring.enabled != focused) ring.enabled = focused;
        }

        /// <summary>Wire the two per-wall rings from a builder.</summary>
        public void ConfigureWallRings(Renderer right, Renderer left)
        {
            m_FocusRingRight = right;
            m_FocusRingLeft = left;
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
