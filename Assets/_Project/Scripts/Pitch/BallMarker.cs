using UnityEngine;
using Stadium3D.Football;

namespace Stadium3D.Pitch
{
    /// <summary>
    /// The ball. A small body plus a ground tick, because at any camera distance that reads a whole play the
    /// ball itself is a few pixels across and the eye needs help finding it.
    /// </summary>
    /// <remarks>
    /// The ground tick is the part that actually matters. On the floor's top-down view the ball is the one
    /// thing a viewer is tracking, and a 28 cm object at 1:22 is about a centimetre — so a marker under it
    /// carries the position and the ball itself carries the height.
    /// </remarks>
    [DisallowMultipleComponent]
    public class BallMarker : MonoBehaviour
    {
        [SerializeField, Tooltip("The ball body.")]
        Transform m_Body;

        [SerializeField, Tooltip("Flat tick on the turf directly below the ball, so its ground position " +
            "reads even when it is in the air.")]
        Transform m_GroundTick;

        [SerializeField, Tooltip("Shown only while the ball is in flight — a dead or carried ball does not " +
            "need one, and a permanent trail turns every handoff into a smear.")]
        TrailRenderer m_Trail;

        [SerializeField, Min(0f), Tooltip("Revolutions per second per metre/second of flight speed.")]
        float m_SpinPerSpeed = 0.35f;

        float m_Spin;

        /// <summary>Place the ball for this frame.</summary>
        public void Apply(in BallState ball)
        {
            transform.position = ball.Position;

            if (m_GroundTick != null)
            {
                // Sits on the turf under the ball, never on it: at the paint's Y a tick would z-fight the
                // yard lines it is most often directly over.
                m_GroundTick.position = new Vector3(ball.Position.x, FieldMarkings.OverlayY, ball.Position.z);
                m_GroundTick.gameObject.SetActive(ball.Carriage != BallCarriage.Dead);
            }

            bool inFlight = ball.Carriage == BallCarriage.InFlight;

            if (m_Trail != null && m_Trail.emitting != inFlight)
                m_Trail.emitting = inFlight;

            if (m_Body != null)
            {
                if (inFlight)
                {
                    // Spin about the flight axis so the ball reads as thrown rather than translated.
                    m_Spin += ball.Velocity.magnitude * m_SpinPerSpeed * 360f * Time.deltaTime;
                    Vector3 axis = ball.Velocity.sqrMagnitude > 1e-4f ? ball.Velocity.normalized : Vector3.forward;
                    m_Body.rotation = Quaternion.AngleAxis(m_Spin, axis) * Quaternion.LookRotation(axis, Vector3.up);
                }
                else if (ball.Carriage == BallCarriage.Held && ball.Velocity.sqrMagnitude > 1e-4f)
                {
                    m_Body.rotation = Quaternion.LookRotation(ball.Velocity.normalized, Vector3.up);
                }
            }
        }

        /// <summary>Wire the parts from a builder.</summary>
        public void Configure(Transform body, Transform groundTick, TrailRenderer trail)
        {
            m_Body = body;
            m_GroundTick = groundTick;
            m_Trail = trail;
        }
    }
}
