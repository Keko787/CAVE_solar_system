using UnityEngine;
using Stadium3D.Football;

namespace Stadium3D.Pitch
{

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

        /// <summary>
        /// Assign the wall this rig serves. Builder-only, and it has to run before <see cref="Awake"/>.
        /// </summary>
        /// <remarks>
        /// The side is not a label. It picks the culling mask and which sideline the boom sits on, and both
        /// are read exactly once at startup. Left at its default, both side walls came up as Home: two
        /// cameras at the same z, showing the same team, on the two walls whose whole purpose is to show
        /// different ones.
        /// </remarks>
        public void SetSide(TeamSide side) => m_Side = side;

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
}
