using UnityEngine;
using Stadium3D.Football;

namespace Stadium3D.Pitch
{

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
