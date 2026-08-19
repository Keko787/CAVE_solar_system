using System.Collections.Generic;
using UnityEngine;
using Stadium3D.Football;

namespace Stadium3D.Pitch
{
    /// <summary>
    /// The only <c>Update</c> in the system. Builds a <see cref="FootballSim"/> from the scripted drive,
    /// ticks it exactly once a frame, and pushes the result at the marker views.
    /// </summary>
    /// <remarks>
    /// <b>Single instance, enforced.</b> A second ticker — a per-camera callback, a <c>LateUpdate</c> as well
    /// as an <c>Update</c>, a second driver surviving a scene merge — advances the drive twice per frame and
    /// runs every clock at double speed. The symptom is a game clock that looks slightly fast, which reads
    /// like a content problem and is not, so this refuses to start rather than letting it happen quietly.
    ///
    /// <para>The MonoBehaviour deliberately holds no simulation state. It owns a reference to the sim and a
    /// dictionary of views; everything about where a body is lives in the pure layer, which is what keeps the
    /// whole thing testable off the Unity run loop.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class FootballSimDriver : MonoBehaviour
    {
        [SerializeField, Tooltip("Root the markers are parented under. Defaults to this object.")]
        Transform m_MarkerRoot;

        [SerializeField, Tooltip("Start the drive as soon as the scene loads. Clear it to drive the sim " +
            "manually from a test or an attract controller.")]
        bool m_PlayOnAwake = true;

        [SerializeField, Tooltip("Log every bake warning on start. Worth leaving on — a route whose speed " +
            "solve collapsed is invisible until someone notices a play running long.")]
        bool m_LogBakeReport = true;

        [SerializeField, Tooltip("Metres a jersey number rises per metre its player stands behind the group. " +
            "Separates the labels of two players standing in line. Zero draws them all at one height.")]
        float m_NumberDepthLift = JerseyNumbers.DefaultDepthLift;

        [SerializeField, Min(0f), Tooltip("Most a number may be moved from its resting height, metres.")]
        float m_NumberLiftLimit = JerseyNumbers.DefaultLiftLimit;

        static FootballSimDriver s_Instance;

        // Enter-play-mode-without-domain-reload would otherwise carry the previous session's instance into
        // the next one, and the second run would find a stale non-null s_Instance and disable itself. The
        // option is off today, but CaveBox and Operators both guard against it and this has the same shape.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_Instance = null;

        FootballSim m_Sim;
        readonly Dictionary<int, IPlayerMarkerView> m_Views = new Dictionary<int, IPlayerMarkerView>();
        readonly List<PlayerMarker> m_Markers = new List<PlayerMarker>();
        BallMarker m_Ball;

        int m_RightFocus;
        int m_LeftFocus;

        /// <summary>The running sim, or null before <see cref="Awake"/>.</summary>
        public FootballSim Sim => m_Sim;

        /// <summary>The single live driver, or null.</summary>
        public static FootballSimDriver Instance => s_Instance;

        /// <summary>Ticking is suspended (distinct from the transport's own pause).</summary>
        public bool Suspended { get; set; }

        void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Debug.LogError("[Stadium3D] A second FootballSimDriver is in this scene. Two drivers advance " +
                               "the drive twice a frame and every clock runs at double speed — disabling " +
                               "this one.", this);
                enabled = false;
                return;
            }

            s_Instance = this;
            if (m_MarkerRoot == null) m_MarkerRoot = transform;

            m_Sim = new FootballSim(ScriptedDrive.Build());

            if (m_LogBakeReport && m_Sim.Report.HasWarnings)
                foreach (var w in m_Sim.Report.Warnings)
                    Debug.LogWarning($"[Stadium3D] bake: {w}", this);

            CollectViews();
        }

        void OnEnable() => Camera.onPreCull += OrientNumbersFor;

        void OnDisable() => Camera.onPreCull -= OrientNumbersFor;

        void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }

        /// <summary>
        /// Turn every jersey number toward whichever camera is about to draw, and separate the ones that
        /// would otherwise stack. Each wall sees the same bodies from a different direction, so a number
        /// oriented once per frame is wrong for the other — a player running away shows their number
        /// mirrored, which reads as a rendering fault rather than as a back.
        /// </summary>
        void OrientNumbersFor(Camera camera) =>
            JerseyNumbers.Orient(camera, m_Markers, m_NumberDepthLift, m_NumberLiftLimit);

        void Update()
        {
            if (m_Sim == null || Suspended || !m_PlayOnAwake) return;
            Step(Time.deltaTime);
        }

        /// <summary>
        /// Advance and redraw. Exposed so an attract controller or a test can drive the sim on its own
        /// schedule with <see cref="m_PlayOnAwake"/> clear.
        /// </summary>
        public void Step(double dt)
        {
            m_Sim.Tick(dt);
            Redraw();
        }

        /// <summary>Push the sim's current frame at the views without advancing anything.</summary>
        public void Redraw()
        {
            var frames = m_Sim.Frames;
            for (int i = 0; i < frames.Count; i++)
            {
                if (m_Views.TryGetValue(frames[i].PlayerId, out var view))
                    view.Apply(frames[i]);
            }

            if (m_Ball != null)
                m_Ball.Apply(m_Sim.Ball);
        }

        /// <summary>Register a view for a roster id. Builders call this as they spawn markers.</summary>
        public void Register(int playerId, IPlayerMarkerView view)
        {
            if (view != null) m_Views[playerId] = view;
        }

        /// <summary>
        /// Record which player a wall is following, and move that wall's ring to them.
        /// </summary>
        /// <remarks>
        /// Two markers change per wall at most, and only when the answer actually changes — a wall
        /// following the same receiver for a whole route does no work at all. The alternative, sweeping
        /// twenty-two markers twice a frame to set a flag that is nearly always already right, is the kind
        /// of cost that never shows up in a profile as one line and never goes away either.
        /// </remarks>
        public void SetWallFocus(TeamSide wall, int playerId)
        {
            int previous = wall == TeamSide.Home ? m_RightFocus : m_LeftFocus;
            if (previous == playerId) return;

            if (previous != 0 && m_Views.TryGetValue(previous, out var before) && before is PlayerMarker was)
                was.SetWallFocus(wall, false);

            if (playerId != 0 && m_Views.TryGetValue(playerId, out var after) && after is PlayerMarker now)
                now.SetWallFocus(wall, true);

            if (wall == TeamSide.Home) m_RightFocus = playerId;
            else m_LeftFocus = playerId;
        }

        /// <summary>Register the ball's view.</summary>
        public void RegisterBall(BallMarker ball) => m_Ball = ball;

        /// <summary>
        /// Adopt every marker already under the root, so a scene built by the Editor builder works without
        /// the builder also having to wire the registry.
        /// </summary>
        void CollectViews()
        {
            m_Views.Clear();
            m_Markers.Clear();

            var roster = m_Sim != null ? m_Sim.Roster : null;

            var markers = m_MarkerRoot.GetComponentsInChildren<PlayerMarker>(true);
            for (int i = 0; i < markers.Length; i++)
            {
                var identity = markers[i].Identity;

                // A marker loaded from a saved scene arrives with no identity: it is bound at build time and
                // the reference does not serialize. Re-bind from the id, which does, against the roster the
                // sim was built with a moment ago. This also keeps the scene honest across a drive edit —
                // names, numbers and teams come from ScriptedDrive on every run, never from a stale copy
                // baked into the scene file.
                if (identity == null && roster != null)
                {
                    identity = roster.Get(markers[i].PlayerId);
                    if (identity != null) markers[i].Bind(identity);
                }

                if (identity == null) continue;
                m_Views[identity.Id] = markers[i];
                m_Markers.Add(markers[i]);
            }

            if (m_Ball == null)
                m_Ball = m_MarkerRoot.GetComponentInChildren<BallMarker>(true);

            if (m_Views.Count == 0)
                Debug.LogWarning($"[Stadium3D] FootballSimDriver bound no player markers under " +
                                 $"\"{m_MarkerRoot.name}\" ({markers.Length} present). If markers are there, " +
                                 "their roster ids do not match this drive — rebuild with " +
                                 "Stadium3D ▸ Build ▸ Football Pitch Rig.", this);
        }
    }
}
