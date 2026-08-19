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

        /// <summary>
        /// How many wall-heights of world the side walls frame at the subject. <b>Not 1.</b>
        /// </summary>
        /// <remarks>
        /// A scale of 1 is true life-size and it was built that way first, because that is the claim the
        /// side walls exist to make. Seen on the wall it delivers exactly what it promises and is still the
        /// wrong default: one torso fills the surface, there is no field behind it, the jersey numbers sit
        /// above the frame, and the per-wall dressing is invisible because no second player is ever in shot.
        /// A wall that cannot show a receiver and the man covering him is not showing a play.
        ///
        /// <para>At 2.5 the rendered height is <c>1.88 / 2.5 = 0.75 m</c> — the arithmetic falls out as
        /// <c>subjectHeight / framingScale</c> and does not depend on distance, so this holds wherever the
        /// player is. Three quarters of a metre is unmistakably a person rather than a marker, and it buys
        /// roughly a five-player group and brings the numbers into frame, which is the whole reason
        /// <see cref="PitchLayers.Numbers"/> exists.</para>
        /// </remarks>
        public const float DefaultFramingScale = 2.5f;

        [SerializeField, Range(0.5f, 4f)]
        [Tooltip("Wall-heights of world framed at the subject. 1 is true life-size and too tight to read a " +
            "play; 2.5 keeps the player at 0.75 m on the wall and shows the people around them.")]
        float m_FramingScale = DefaultFramingScale;

        [SerializeField] float m_MoveSharpness = 3.2f;
        [SerializeField] float m_AimSharpness = 6f;

        Vector3 m_Aim;

        /// <summary>The player this wall is following. Set by the wand in M6; the ball carrier until then.</summary>
        public int FocusedPlayerId { get; set; }

        /// <summary>Which team this wall reads as "ours".</summary>
        public TeamSide Side => m_Side;

        /// <summary>
        /// The player this wall is actually watching this frame — the focused one, or whoever it fell back
        /// to. Distinct from <see cref="FocusedPlayerId"/>, which is a request and is zero until something
        /// makes one.
        /// </summary>
        public int SubjectPlayerId { get; private set; }

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

            // The ring follows whoever the framing settled on, including the fallback. A wall that frames
            // one player and rings another is worse than a wall with no ring at all.
            var driver = FootballSimDriver.Instance;
            if (driver != null) driver.SetWallFocus(m_Side, SubjectPlayerId);

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
                    {
                        SubjectPlayerId = FocusedPlayerId;
                        return frames[i].Position;
                    }
            }

            // No focus yet: follow whoever on this team is nearest the ball, which is almost always the
            // player worth watching and never leaves the wall showing an empty patch of grass.
            Vector3 ball = sim.Ball.Position;
            float best = float.MaxValue;
            Vector3 chosen = ball;
            int chosenId = 0;

            for (int i = 0; i < frames.Count; i++)
            {
                if (frames[i].Side != m_Side) continue;
                float d = (frames[i].Position - ball).sqrMagnitude;
                if (d < best) { best = d; chosen = frames[i].Position; chosenId = frames[i].PlayerId; }
            }

            SubjectPlayerId = chosenId;
            return chosen;
        }
    }
}
