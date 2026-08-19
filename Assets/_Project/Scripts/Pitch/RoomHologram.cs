using UnityEngine;
using Stadium3D.Football;

namespace Stadium3D.Pitch
{
    /// <summary>
    /// One player, at true physical scale, as real geometry standing on the CAVE floor.
    /// </summary>
    /// <remarks>
    /// <b>This is the milestone that answers whether any of this needs a CAVE.</b> Everything on the four
    /// walls is a flat picture — correct, well-framed, and reproducible on four televisions bolted to the
    /// same walls for a fraction of the cost. A skeptic standing in the room is entitled to ask what the
    /// projectors and the tracking are buying, and until something is genuinely stereoscopic the honest
    /// answer is "nothing yet".
    ///
    /// <para>So: a person, at the size a person actually is, that you can walk around and look behind. The
    /// side walls already render a player life-size, which is striking — but flat. Turning from that wall to
    /// this figure is the comparison the whole demo rests on, and it is deliberately scheduled before stats
    /// and overlays so a disappointing answer arrives while there is still time to act on it.</para>
    ///
    /// <para><b>Translation is dropped on purpose.</b> A football field is 110 m and the room is 3.5 m, so a
    /// sprinting player at 1:1 crosses the CAVE in under half a second and spends the rest of the play
    /// somewhere behind a wall. The figure therefore holds a fixed anchor and takes only its <em>facing</em>
    /// and its motion state from the sim. What is being tested is presence and scale, not choreography.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class RoomHologram : MonoBehaviour
    {
        [SerializeField, Tooltip("The figure. Its root is placed on the floor and scaled to life-size.")]
        Transform m_Figure;

        [SerializeField, Tooltip("Ring on the floor beneath the figure, so its footing reads as contact " +
            "with the room rather than a hover.")]
        Transform m_GroundRing;

        [SerializeField, Tooltip("Where in the room the figure stands, in the CAVE's local frame. Slightly " +
            "forward of centre and to one side, so the operator can walk around it without backing into a wall.")]
        Vector3 m_Anchor = new Vector3(0.85f, 0f, 0.15f);

        [SerializeField, Tooltip("Which player to show. 0 follows whoever has the ball, which keeps the " +
            "figure meaningful without any input wired up.")]
        int m_PlayerId;

        [SerializeField, Range(0.25f, 2f)]
        [Tooltip("1 is true life-size and is the whole point. Lower it only if the figure proves " +
            "overwhelming at arm's length in the real room.")]
        float m_Scale = 1f;

        [SerializeField] float m_TurnSharpness = 4f;

        FootballSim m_Sim;
        float m_Heading;
        bool m_Warm;

        /// <summary>The roster id currently shown, or 0 while following the ball.</summary>
        public int PlayerId { get => m_PlayerId; set => m_PlayerId = value; }

        /// <summary>Where the figure stands, in the room's local frame.</summary>
        public Vector3 Anchor { get => m_Anchor; set => m_Anchor = value; }

        /// <summary>Height of the figure in metres, as actually built. Should be a person's height.</summary>
        public float HeightMeters => WallFraming.PlayerHeightMeters * m_Scale;

        void LateUpdate()
        {
            m_Sim = FootballSimDriver.Instance != null ? FootballSimDriver.Instance.Sim : null;
            if (m_Sim == null) return;

            Apply(m_Sim);
        }

        /// <summary>
        /// Place and orient the figure for this tick. Public so the editor capture tooling can drive it
        /// without a running game loop.
        /// </summary>
        public void Apply(FootballSim sim)
        {
            if (sim == null) return;

            transform.localPosition = m_Anchor;

            if (m_Figure != null)
            {
                m_Figure.localScale = Vector3.one * m_Scale;
                m_Figure.localPosition = Vector3.zero;
            }

            if (!TryResolve(sim, out var frame)) return;

            // Facing only. The sim's heading is a field bearing; the room has its own orientation, and the
            // figure is standing on a spot rather than running a route, so the bearing is applied directly
            // and read as "which way this player is looking".
            float target = frame.Rotation.eulerAngles.y;
            m_Heading = m_Warm && Time.deltaTime > 0f
                ? Mathf.LerpAngle(m_Heading, target, 1f - Mathf.Exp(-m_TurnSharpness * Time.deltaTime))
                : target;
            m_Warm = true;

            transform.localRotation = Quaternion.Euler(0f, m_Heading, 0f);

            if (m_GroundRing != null)
                m_GroundRing.localScale = Vector3.one * (0.9f * m_Scale);
        }

        bool TryResolve(FootballSim sim, out PlayerFrame frame)
        {
            var frames = sim.Frames;

            int wanted = m_PlayerId != 0
                ? m_PlayerId
                : (sim.Ball.Carriage == BallCarriage.Held ? sim.Ball.CarrierId : 0);

            if (wanted != 0)
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    if (frames[i].PlayerId != wanted) continue;
                    frame = frames[i];
                    return true;
                }
            }

            // Nobody has the ball — a throw is in the air. Hold whoever is nearest to it, so the figure
            // never blanks out mid-play.
            float best = float.MaxValue;
            frame = default;
            bool found = false;

            for (int i = 0; i < frames.Count; i++)
            {
                float d = (frames[i].Position - sim.Ball.Position).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                frame = frames[i];
                found = true;
            }
            return found;
        }

        /// <summary>Wire the parts from a builder.</summary>
        public void Configure(Transform figure, Transform groundRing)
        {
            m_Figure = figure;
            m_GroundRing = groundRing;
        }
    }
}
