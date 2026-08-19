using UnityEngine;
using Stadium3D.Football;

namespace Stadium3D.Pitch
{

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
}
