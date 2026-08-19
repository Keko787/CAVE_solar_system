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

            // Sample count follows the active pipeline rather than a constant. A render target whose
            // MSAA disagrees with the renderer is not a quality problem but a correctness one: the
            // pass fails to open, its clear never runs, and the wall accumulates every frame it has
            // ever drawn - which reads as geometry spawning, not as a missing clear.
            int msaa = Mathf.Max(1, QualitySettings.antiAliasing);

            m_View = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32)
            {
                name = $"{name}_view",
                antiAliasing = msaa,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
            };
            m_View.Create();

            m_Camera.targetTexture = m_View;      // also what stops MiddleVR disabling it at startup
            m_Camera.cullingMask = CullingMask();
            m_Camera.clearFlags = CameraClearFlags.SolidColor;
            // Night sky, not void. Near-black was fine while nothing sat above the horizon; once the bowl
            // arrived, the gap between its rim and the top of frame wanted to read as sky rather than as
            // the absence of geometry. The floor never sees any of it - it looks straight down.
            m_Camera.backgroundColor = new Color(0.095f, 0.115f, 0.165f);
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
            KeepAliveUnderMiddleVR();

            var sim = Sim;
            if (sim != null) Track(sim);
        }

        /// <summary>
        /// Undo MiddleVR's startup camera cull, which can catch these cameras before they own a target.
        /// </summary>
        /// <remarks>
        /// <c>MVRManagerScript</c> disables every scene camera whose <c>targetTexture</c> is null at startup,
        /// so that the room is drawn only by its own display cameras. Ours do have a target — but it is
        /// assigned in <see cref="Awake"/>, and script execution order between MiddleVR's Awake and ours is
        /// undefined. Lose that race and the camera is switched off before it can prove it belongs, then
        /// never renders again: the panel is wired, the render target exists, the culling mask is right, and
        /// the wall stays blank.
        ///
        /// <para>The source project learned this the same way and carried the identical guard in
        /// <c>TopDownMapCamera</c> and <c>SimDrone</c>. That knowledge lived in the files M0 dropped, so it
        /// had to be rediscovered — worth recording here rather than in a commit message.</para>
        /// </remarks>
        void KeepAliveUnderMiddleVR()
        {
            if (m_Camera != null && !m_Camera.enabled && m_Camera.targetTexture != null)
                m_Camera.enabled = true;
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
}
