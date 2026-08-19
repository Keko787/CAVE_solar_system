using UnityEngine;
using Stadium3D.Football;

namespace Stadium3D.Pitch
{
    /// <summary>
    /// Shared behaviour for the four cameras that render the pitch into the CAVE's wall panels: they all
    /// live out at the field, all render to a <see cref="RenderTexture"/>, and none of them is ever seen
    /// directly by anybody.
    /// </summary>
    /// <remarks>
    /// Every one of these has to be an ordinary Unity camera rather than a MiddleVR one. MiddleVR's four
    /// display cameras are co-located on the tracked head and it rewrites their transform and projection
    /// every frame, so they cannot be given different viewpoints — <c>cullingMask</c> is the only per-wall
    /// lever it leaves alone. Distinct views therefore mean offscreen cameras and flat panels, and the price
    /// is that the football is monoscopic. That price is not recoverable while the walls show different
    /// things; it is bought back in M8, where the miniature is real geometry in the room.
    /// </remarks>
    [RequireComponent(typeof(Camera))]
    public abstract class PitchCameraBase : MonoBehaviour
    {
        [SerializeField, Tooltip("Resolution multiplier on the projector-matched default. Drop it if the " +
            "frame budget bites; anything below 1 softens yard lines first.")]
        [Range(0.25f, 2f)]
        protected float m_ResolutionScale = 1f;

        [SerializeField, Tooltip("Set for the front and floor surfaces (3.556 m wide), clear for the two " +
            "side surfaces (2.3876 m). Ignored by rigs that pin it — a side wall is always narrow.")]
        protected bool m_WideSurface = true;

        /// <summary>
        /// Which physical surface this rig feeds, and therefore the shape of its render target.
        /// </summary>
        /// <remarks>
        /// A property rather than the serialized field alone, because the render target is created in
        /// <see cref="Awake"/> and <see cref="Configure"/> runs after it — a rig that set the field there was
        /// too late, and the side walls silently rendered at the wide surface's 2560x1600 instead of their
        /// own 1719x1600. The panel would then letterbox a correct image into the wrong shape, which is a
        /// subtle enough wrongness to survive a glance.
        /// </remarks>
        protected virtual bool WideSurface => m_WideSurface;

        Camera m_Camera;
        RenderTexture m_View;

        /// <summary>The render target the wall panel samples. Null before Awake.</summary>
        public RenderTexture View => m_View;

        /// <summary>The camera this rig drives.</summary>
        public Camera Camera => m_Camera;

        /// <summary>The sim being watched, resolved from the single driver.</summary>
        protected FootballSim Sim => FootballSimDriver.Instance != null ? FootballSimDriver.Instance.Sim : null;

        /// <summary>
        /// True once this rig has framed at least one tick. Until then it places itself exactly rather than
        /// easing, so the demo does not open with four cameras swinging in from wherever the builder left
        /// them — which is both ugly and, on the side walls, a scale change the viewer reads as a zoom.
        /// </summary>
        protected bool Warm { get; private set; }

        /// <summary>
        /// Ease toward a pose, or snap to it on the first tick. Also the seam the editor preview uses to
        /// place a camera without a running game loop, where <c>Time.deltaTime</c> is zero and easing would
        /// never converge.
        /// </summary>
        /// <remarks>
        /// Poses are in the pitch root's <b>local</b> space. Everything the sim reports is a field
        /// coordinate, and in the CAVE scene the pitch is parked 1500 m below the room to stay outside the
        /// display cameras' far plane — so a camera that placed itself in world space would sit up in the
        /// room looking at nothing.
        /// </remarks>
        protected void Place(Vector3 localPosition, Quaternion localRotation, float moveSharpness)
        {
            if (!Warm || Time.deltaTime <= 0f)
            {
                transform.SetLocalPositionAndRotation(localPosition, localRotation);
                Warm = true;
                return;
            }

            transform.SetLocalPositionAndRotation(
                Vector3.Lerp(transform.localPosition, localPosition, Ease(moveSharpness)),
                localRotation);
        }

        /// <summary>
        /// Set the rig up outside play mode, for the editor capture tooling. Mirrors <see cref="Awake"/>,
        /// which does not run on a scene object the editor merely loaded.
        /// </summary>
        public void InitializeForPreview()
        {
            if (m_Camera == null) Awake();
        }

        /// <summary>Frame one tick against a supplied sim, for the editor capture tooling.</summary>
        public void TrackForPreview(FootballSim sim)
        {
            if (sim != null) Track(sim);
        }

        protected virtual void Awake()
        {
            m_Camera = GetComponent<Camera>();

            var size = WallFraming.RenderTextureSize(WideSurface, m_ResolutionScale);
            m_View = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32)
            {
                name = $"{name}_view",
                antiAliasing = 4,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
            };
            m_View.Create();

            m_Camera.targetTexture = m_View;      // also what stops MiddleVR disabling it at startup
            m_Camera.cullingMask = CullingMask();
            m_Camera.clearFlags = CameraClearFlags.SolidColor;
            m_Camera.backgroundColor = new Color(0.04f, 0.05f, 0.06f);
            m_Camera.allowHDR = false;            // the shipping URP asset has HDR off
            m_Camera.nearClipPlane = 0.3f;
            m_Camera.farClipPlane = 400f;

            Configure();
        }

        protected virtual void OnDestroy()
        {
            if (m_Camera != null) m_Camera.targetTexture = null;
            if (m_View != null) { m_View.Release(); Destroy(m_View); }
        }

        void LateUpdate()
        {
            var sim = Sim;
            if (sim != null) Track(sim);
        }

        /// <summary>Which layers this wall's camera is allowed to see. Always an allow-list.</summary>
        protected abstract int CullingMask();

        /// <summary>One-time camera setup after the render target exists.</summary>
        protected virtual void Configure() { }

        /// <summary>Move and frame for this tick.</summary>
        protected abstract void Track(FootballSim sim);

        /// <summary>Framerate-independent easing, the idiom the CAVE panels already use.</summary>
        protected static float Ease(float sharpness) => 1f - Mathf.Exp(-sharpness * Time.deltaTime);
    }

    /// <summary>
    /// The front wall: the overall view, following the ball. A boom on the near sideline, the shot a viewer
    /// already knows from television — which is the point, because it is the one wall that has to be
    /// instantly readable to someone who has never stood in a CAVE.
    /// </summary>
    public class BroadcastPitchCamera : PitchCameraBase
    {
        [SerializeField, Tooltip("How far off the sideline the boom sits, metres.")]
        float m_SidelineOffset = 34f;

        [SerializeField, Tooltip("Boom height, metres.")]
        float m_Height = 15f;

        [SerializeField, Tooltip("How far ahead of the ball's motion to lead the shot, seconds. A camera " +
            "that only follows is always behind the play.")]
        float m_LookaheadSeconds = 0.45f;

        [SerializeField] float m_MoveSharpness = 2.6f;
        [SerializeField] float m_AimSharpness = 4.5f;
        [SerializeField] float m_VerticalFov = 30f;

        Vector3 m_Aim;

        protected override int CullingMask() => PitchLayers.BroadcastMask;

        protected override void Configure()
        {
            Camera.orthographic = false;
            Camera.fieldOfView = m_VerticalFov;
        }

        protected override void Track(FootballSim sim)
        {
            Vector3 target = sim.Ball.Position + sim.Ball.Velocity * m_LookaheadSeconds;
            target.y = 0f;

            m_Aim = Warm && Time.deltaTime > 0f
                ? Vector3.Lerp(m_Aim, target, Ease(m_AimSharpness))
                : target;

            // The boom stays on one side of the field for the whole demo. Crossing the line mid-drive is
            // the classic way to make an audience lose track of which way a team is going.
            Vector3 desired = new Vector3(m_Aim.x, m_Height, -m_SidelineOffset);
            Quaternion aim = Quaternion.LookRotation((m_Aim + Vector3.up * 1.2f) - desired, Vector3.up);
            Place(desired, aim, m_MoveSharpness);
        }
    }

    /// <summary>
    /// A side wall: one team's isolation camera, solved so the focused player renders at <b>true physical
    /// scale</b> on the wall.
    /// </summary>
    /// <remarks>
    /// This is the single most differentiated image the room can produce. A 1.88 m person at the 1.778 m
    /// viewing distance subtends 55.8 degrees vertically, and the side walls offer 64 — so a life-size
    /// athlete fits, comfortably, with room to spare. Turn your head and someone is standing there at the
    /// size they would actually be. No television does that, and it is the reason the side walls' nearly
    /// square 1.074 aspect goes from a liability to a requirement: square is wrong for a wide sideline shot
    /// and exactly right for a standing human.
    ///
    /// <para>Achieving it means the camera's job is not "point at the player" but "keep exactly one
    /// wall-height of world in frame at the player's distance" — see <see cref="WallFraming"/>. The field of
    /// view therefore changes every frame as the player moves toward or away from the boom.</para>
    /// </remarks>
    public class TeamIsoPitchCamera : PitchCameraBase
    {
        [SerializeField, Tooltip("Which team this wall belongs to. Home is the right wall, away the left.")]
        TeamSide m_Side = TeamSide.Home;

        [SerializeField, Tooltip("How far off the sideline this team's boom sits, metres. Also the distance " +
            "the life-size solve is anchored to.")]
        float m_SidelineOffset = 16f;

        [SerializeField] float m_Height = 3.4f;

        [SerializeField, Range(0.5f, 4f)]
        [Tooltip("1 renders the focused player life-size. Raise it to show more of the play around them at " +
            "correspondingly smaller scale — the knob to reach for if the tight shot reads claustrophobic " +
            "in the room.")]
        float m_FramingScale = 1f;

        [SerializeField] float m_MoveSharpness = 3.2f;
        [SerializeField] float m_AimSharpness = 6f;

        Vector3 m_Aim;

        /// <summary>The player this wall is following. Set by the wand in M6; the ball carrier until then.</summary>
        public int FocusedPlayerId { get; set; }

        /// <summary>Which team this wall reads as "ours".</summary>
        public TeamSide Side => m_Side;

        /// <summary>A side wall is always the narrow surface, whatever the Inspector says.</summary>
        protected override bool WideSurface => false;

        protected override int CullingMask() =>
            m_Side == TeamSide.Home ? PitchLayers.RightWallMask : PitchLayers.LeftWallMask;

        protected override void Configure() => Camera.orthographic = false;

        protected override void Track(FootballSim sim)
        {
            Vector3 subject = ResolveSubject(sim);

            m_Aim = Warm && Time.deltaTime > 0f
                ? Vector3.Lerp(m_Aim, subject, Ease(m_AimSharpness))
                : subject;

            float sidelineZ = m_Side == TeamSide.Home ? -m_SidelineOffset : m_SidelineOffset;
            Vector3 desired = new Vector3(m_Aim.x, m_Height, sidelineZ);
            Vector3 look = m_Aim + Vector3.up * (WallFraming.PlayerHeightMeters * 0.5f);
            Place(desired, Quaternion.LookRotation(look - desired, Vector3.up), m_MoveSharpness);

            // Solve the field of view against the ACTUAL distance, every frame. Anchoring it to the nominal
            // sideline offset instead would make the player grow and shrink as they crossed the field, which
            // is precisely the thing life-size framing exists to prevent. Local space on both sides, so the
            // pitch root's offset cancels out.
            float distance = Vector3.Distance(transform.localPosition, look);
            Camera.fieldOfView = WallFraming.LifeSizeVerticalFov(
                distance, WallFraming.WallHeightMeters, WallFraming.PlayerHeightMeters, m_FramingScale);
        }

        Vector3 ResolveSubject(FootballSim sim)
        {
            var frames = sim.Frames;

            if (FocusedPlayerId != 0)
            {
                for (int i = 0; i < frames.Count; i++)
                    if (frames[i].PlayerId == FocusedPlayerId)
                        return frames[i].Position;
            }

            // No focus yet: follow whoever on this team is nearest the ball, which is almost always the
            // player worth watching and never leaves the wall showing an empty patch of grass.
            Vector3 ball = sim.Ball.Position;
            float best = float.MaxValue;
            Vector3 chosen = ball;

            for (int i = 0; i < frames.Count; i++)
            {
                if (frames[i].Side != m_Side) continue;
                float d = (frames[i].Position - ball).sqrMagnitude;
                if (d < best) { best = d; chosen = frames[i].Position; }
            }
            return chosen;
        }
    }

    /// <summary>
    /// The floor: the tactical top-down, orthographic, at 1:22 with the field's full width across the room's
    /// short axis and a window that slides along its length with the ball.
    /// </summary>
    /// <remarks>
    /// The scale is forced by arithmetic rather than taste. Fitting all 120 yards onto the floor needs about
    /// 1:31, where a jersey number subtends 0.2 degrees against the roughly 0.4 needed to read two digits,
    /// and seven linemen collapse into a 3.7 cm smear at every snap. At 1:22 the full 48.768 m width fits
    /// the 2.2225 m axis exactly, bodies separate, and formations read — at the cost of showing about 85 of
    /// the 120 yards. That trade is the right way round: the floor's job is position, formation and motion.
    /// <b>Identity belongs on the side walls</b>, where a player is life-size.
    ///
    /// <para>The window is also biased toward the far half of the floor, because the operator is standing on
    /// the near half and their own body occludes it.</para>
    /// </remarks>
    public class FloorTopDownCamera : PitchCameraBase
    {
        [SerializeField, Tooltip("Metres of field per metre of floor. 22 fits the full field width exactly.")]
        float m_Scale = WallFraming.FloorScale;

        [SerializeField] float m_FollowSharpness = 2.2f;

        [SerializeField, Range(0f, 0.4f)]
        [Tooltip("Push the window toward the far half of the floor, as a fraction of its length — the near " +
            "half is where the operator is standing and occluding.")]
        float m_ForwardBias = 0.12f;

        float m_CenterX;

        protected override int CullingMask() => PitchLayers.FloorMask;

        protected override void Configure()
        {
            Camera.orthographic = true;
            Camera.orthographicSize = WallFraming.FloorOrthographicSize(m_Scale);
            Camera.nearClipPlane = 1f;
            Camera.farClipPlane = 200f;

            // Straight down. The yaw matters: at 0 the camera's right is the field's +X, so the field's
            // LENGTH runs across the render target's long axis — which is the floor's 3.556 m side — and its
            // WIDTH runs across the 2.2225 m side, where 1:22 fits it exactly. A 90 degree yaw here swaps
            // them, and the floor then shows the full width across the room's long axis at half the useful
            // scale with apron down both sides.
            transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        protected override void Track(FootballSim sim)
        {
            var coverage = WallFraming.FloorCoverageMeters(m_Scale);
            float halfExtentX = FieldGeometry.YardsToMeters(
                FieldGeometry.FieldLengthYards * 0.5f + FieldGeometry.EndZoneDepthYards);

            float target = WallFraming.FloorWindowCenterX(sim.Ball.Position.x, coverage.x, halfExtentX);
            target += coverage.x * m_ForwardBias;

            m_CenterX = Warm && Time.deltaTime > 0f
                ? Mathf.Lerp(m_CenterX, target, Ease(m_FollowSharpness))
                : target;

            Place(new Vector3(m_CenterX, 60f, 0f), Quaternion.Euler(90f, 0f, 0f), m_FollowSharpness);
        }
    }
}
