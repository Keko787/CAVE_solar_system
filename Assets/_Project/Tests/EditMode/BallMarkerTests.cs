using NUnit.Framework;
using Stadium3D.Football;
using Stadium3D.Pitch;
using UnityEngine;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// The ball's ground tick, pinned to the ball.
    /// </summary>
    /// <remarks>
    /// This exists because of a bug that survived every render anyone looked at. The tick is a child of the
    /// marker, the marker had already moved to the ball, and the tick was then given the ball's field
    /// position a second time — so it sat at double the offset from midfield. It still slid around the
    /// field near the play, and the error grew smoothly with distance from the middle, which is exactly
    /// what a deliberate lead indicator would do. Nothing about the picture said "wrong".
    ///
    /// <para>Only an assertion about where the tick lands <em>relative to the ball</em> catches that, which
    /// is why the test measures world positions of two objects rather than trusting one number.</para>
    /// </remarks>
    public class BallMarkerTests
    {
        static BallMarker Build(out Transform body, out Transform tick)
        {
            var root = new GameObject("Ball");

            body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);

            tick = new GameObject("GroundTick").transform;
            tick.SetParent(root.transform, false);

            var marker = root.AddComponent<BallMarker>();
            marker.Configure(body, tick, null);
            return marker;
        }

        [Test]
        public void GroundTick_SitsDirectlyUnderTheBall()
        {
            var marker = Build(out _, out var tick);
            try
            {
                // Well off centre in both axes, because a double-application is invisible at the origin.
                var position = new Vector3(18.3f, 4.6f, -9.7f);
                marker.Apply(new BallState(BallCarriage.InFlight, 0, position, Vector3.forward));

                Assert.That(tick.position.x, Is.EqualTo(position.x).Within(1e-3f),
                    "the tick has to share the ball's yard line, or it is marking a spot nobody is at");
                Assert.That(tick.position.z, Is.EqualTo(position.z).Within(1e-3f),
                    "the tick has to share the ball's lateral position");
                Assert.That(tick.position.y, Is.EqualTo(FieldMarkings.OverlayY).Within(1e-3f),
                    "the tick belongs on the turf, not at the height of the ball");
            }
            finally { Object.DestroyImmediate(marker.gameObject); }
        }

        [Test]
        public void GroundTick_TracksTheBallRatherThanScalingWithIt()
        {
            var marker = Build(out _, out var tick);
            try
            {
                marker.Apply(new BallState(BallCarriage.InFlight, 0, new Vector3(5f, 2f, 3f), Vector3.forward));
                float near = tick.position.x;

                marker.Apply(new BallState(BallCarriage.InFlight, 0, new Vector3(20f, 2f, 3f), Vector3.forward));
                float far = tick.position.x;

                // The tick moved exactly as far as the ball did. Doubling passes an "it moves" check.
                Assert.That(far - near, Is.EqualTo(15f).Within(1e-3f));
            }
            finally { Object.DestroyImmediate(marker.gameObject); }
        }

        [Test]
        public void GroundTick_IsHiddenOnceTheBallIsDead()
        {
            var marker = Build(out _, out var tick);
            try
            {
                marker.Apply(new BallState(BallCarriage.Dead, 0, new Vector3(1f, 0f, 1f), Vector3.zero));
                Assert.That(tick.gameObject.activeSelf, Is.False);

                marker.Apply(new BallState(BallCarriage.Held, 12, new Vector3(1f, 1f, 1f), Vector3.zero));
                Assert.That(tick.gameObject.activeSelf, Is.True);
            }
            finally { Object.DestroyImmediate(marker.gameObject); }
        }
    }
}
