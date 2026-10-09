using UnityEngine;

namespace Stadium3D.Space
{
    /// <summary>
    /// Puts one body on a circular orbit around another. Pure data plus <see cref="Evaluate"/> — this component
    /// never ticks itself; <see cref="SolarSystemDriver"/> calls it, in an order that guarantees a moon is placed
    /// after the planet it rides on.
    /// </summary>
    /// <remarks>
    /// <para>The position is solved from the clock rather than integrated frame by frame, which is the one thing
    /// the old <c>RotateAround</c> could not do. An incremental <c>transform.RotateAround</c> accumulates float
    /// error, drifts off its radius over a long session, and — because it advances by <c>Time.deltaTime</c> —
    /// makes the whole system's arrangement depend on frame rate. A CAVE runs for hours across several machines
    /// whose frames do not line up, so "where is Mars" has to be a function of time and nothing else.</para>
    ///
    /// <para>Orbits are drawn as gizmos so the layout can be checked in the editor without entering play mode.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Stadium3D/Space/Planet Orbit")]
    public sealed class PlanetOrbit : MonoBehaviour
    {
        [Tooltip("The body this one goes round. Usually the Sun; the Moon's is Earth.")]
        [SerializeField] Transform m_Centre;

        [Tooltip("Distance from the centre, in metres.")]
        [SerializeField] float m_RadiusMeters = 0.5f;

        [Tooltip("Seconds for one full orbit on screen. Zero parks the body at its phase angle.")]
        [SerializeField] float m_PeriodSeconds = 12f;

        [Tooltip("Where on the ring the body sits at t = 0, in degrees.")]
        [SerializeField] float m_PhaseDegrees;

        [Tooltip("Normal of the orbital plane. Up gives the flat ecliptic the room reads best.")]
        [SerializeField] Vector3 m_Axis = Vector3.up;

        [SerializeField] bool m_DrawGizmo = true;

        /// <summary>The body this one orbits. Null falls back to the world origin.</summary>
        public Transform Centre
        {
            get => m_Centre;
            set => m_Centre = value;
        }

        /// <summary>Orbit radius in metres.</summary>
        public float RadiusMeters
        {
            get => m_RadiusMeters;
            set => m_RadiusMeters = value;
        }

        /// <summary>Seconds per revolution. Zero or less parks the body.</summary>
        public float PeriodSeconds
        {
            get => m_PeriodSeconds;
            set => m_PeriodSeconds = value;
        }

        /// <summary>Angle along the ring at t = 0.</summary>
        public float PhaseDegrees
        {
            get => m_PhaseDegrees;
            set => m_PhaseDegrees = value;
        }

        /// <summary>Normal of the orbital plane.</summary>
        public Vector3 Axis
        {
            get => m_Axis;
            set => m_Axis = value;
        }

        /// <summary>Moves the body to where it belongs at <paramref name="seconds"/> on the system clock.</summary>
        public void Evaluate(double seconds)
        {
            transform.position = PositionAt(seconds);
        }

        /// <summary>Where this body sits at <paramref name="seconds"/>, in world space.</summary>
        public Vector3 PositionAt(double seconds)
        {
            Vector3 centre = m_Centre != null ? m_Centre.position : Vector3.zero;

            // A zero period is a legitimate answer, not an error: it parks the body where its phase puts it,
            // which is what a stationary display of the layout wants.
            double revolutions = m_PeriodSeconds > 0f ? seconds / m_PeriodSeconds : 0d;
            double degrees = m_PhaseDegrees + revolutions * 360d;

            Basis(out Vector3 u, out Vector3 v);
            float radians = (float)(degrees * Mathf.Deg2Rad);
            return centre + m_RadiusMeters * (u * Mathf.Cos(radians) + v * Mathf.Sin(radians));
        }

        /// <summary>
        /// Two orthonormal vectors spanning the orbital plane. Phase 0 lands on +X and the body travels
        /// anticlockwise seen from above, matching the real solar system seen from north of the ecliptic.
        /// </summary>
        void Basis(out Vector3 u, out Vector3 v)
        {
            Vector3 axis = m_Axis.sqrMagnitude < 1e-8f ? Vector3.up : m_Axis.normalized;

            // Cross with forward unless the axis is forward, in which case that cross is degenerate.
            u = Vector3.Cross(axis, Vector3.forward);
            if (u.sqrMagnitude < 1e-6f)
                u = Vector3.Cross(axis, Vector3.right);

            u.Normalize();
            v = Vector3.Cross(u, axis);
        }

        void OnDrawGizmos()
        {
            if (!m_DrawGizmo || m_RadiusMeters <= 0f)
                return;

            Vector3 centre = m_Centre != null ? m_Centre.position : Vector3.zero;
            Basis(out Vector3 u, out Vector3 v);

            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.5f);
            const int segments = 96;
            Vector3 previous = centre + m_RadiusMeters * u;
            for (int i = 1; i <= segments; i++)
            {
                float radians = i / (float)segments * Mathf.PI * 2f;
                Vector3 point = centre + m_RadiusMeters * (u * Mathf.Cos(radians) + v * Mathf.Sin(radians));
                Gizmos.DrawLine(previous, point);
                previous = point;
            }
        }
    }
}
