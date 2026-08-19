using NUnit.Framework;
using Stadium3D.Football;
using Stadium3D.Pitch;
using UnityEngine;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// The route trail, checked against the route it claims to be tracing.
    /// </summary>
    /// <remarks>
    /// Run against the shipped drive rather than a fabricated track, because the failure worth catching is
    /// not "the line has points" but "the line is somewhere the player never was". A trail built by
    /// appending a position every frame would pass any structural assertion and still be wrong after a
    /// seek — so these tests seek, and then compare the ends of the line against what the bake says.
    /// </remarks>
    public class RouteTrailTests
    {
        GameObject m_Root;
        RouteTrail m_Trail;
        LineRenderer m_Line;
        FootballSim m_Sim;

        [SetUp]
        public void SetUp()
        {
            m_Root = new GameObject("Trail");
            m_Line = m_Root.AddComponent<LineRenderer>();
            m_Line.useWorldSpace = false;
            m_Trail = m_Root.AddComponent<RouteTrail>();

            m_Sim = new FootballSim(ScriptedDrive.Build());
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Root != null) Object.DestroyImmediate(m_Root);
        }

        /// <summary>A player who is on the field for the play we seek to.</summary>
        int AnyPlayer()
        {
            var frames = m_Sim.Frames;
            for (int i = 0; i < frames.Count; i++)
                if (m_Sim.CurrentBakedPlay.Has(frames[i].PlayerId))
                    return frames[i].PlayerId;
            return 0;
        }

        [Test]
        public void TheTrailRunsFromTheSnapToWhereThePlayerIsNow()
        {
            m_Sim.Director.SeekToPlay(0, 2.0f);
            m_Sim.Tick(0.0);

            int id = AnyPlayer();
            Assert.That(id, Is.Not.Zero, "the drive has to put somebody on the field for this to mean anything");

            m_Trail.Apply(m_Sim, id);

            Assert.That(m_Line.enabled, Is.True);
            Assert.That(m_Line.positionCount, Is.EqualTo(RouteTrail.Samples));

            var play = m_Sim.CurrentBakedPlay;
            Vector3 atSnap = play.Sample(id, 0f).Position;
            Vector3 atNow = play.Sample(id, 2.0f).Position;

            Vector3 first = m_Line.GetPosition(0);
            Vector3 last = m_Line.GetPosition(RouteTrail.Samples - 1);

            Assert.That(first.x, Is.EqualTo(atSnap.x).Within(0.01f));
            Assert.That(first.z, Is.EqualTo(atSnap.z).Within(0.01f));
            Assert.That(last.x, Is.EqualTo(atNow.x).Within(0.01f));
            Assert.That(last.z, Is.EqualTo(atNow.z).Within(0.01f));
        }

        /// <summary>
        /// The one an accumulated trail fails. Scrub backwards and the line has to shorten to match, not
        /// keep a tail through places the player has not reached yet.
        /// </summary>
        [Test]
        public void SeekingBackwardsShortensTheTrailRatherThanLeavingATail()
        {
            int id;

            m_Sim.Director.SeekToPlay(0, 2.5f);
            m_Sim.Tick(0.0);
            id = AnyPlayer();
            m_Trail.Apply(m_Sim, id);
            Vector3 lateHead = m_Line.GetPosition(RouteTrail.Samples - 1);

            m_Sim.Director.SeekToPlay(0, 0.8f);
            m_Sim.Tick(0.0);
            m_Trail.Apply(m_Sim, id);
            Vector3 earlyHead = m_Line.GetPosition(RouteTrail.Samples - 1);

            Assert.That(earlyHead, Is.Not.EqualTo(lateHead));
            Assert.That(earlyHead.x, Is.EqualTo(m_Sim.CurrentBakedPlay.Sample(id, 0.8f).Position.x).Within(0.01f),
                "the head of the trail is wherever the player is now, not the furthest they have ever been");
        }

        [Test]
        public void NoFocusDrawsNothing()
        {
            m_Sim.Director.SeekToPlay(0, 2.0f);
            m_Sim.Tick(0.0);

            m_Trail.Apply(m_Sim, 0);

            Assert.That(m_Line.enabled, Is.False,
                "a wall that has not settled on anybody has no route to draw");
        }

        [Test]
        public void TheTrailSitsOnTheTurfRatherThanFollowingThePlayerUp()
        {
            m_Sim.Director.SeekToPlay(0, 2.0f);
            m_Sim.Tick(0.0);

            m_Trail.Apply(m_Sim, AnyPlayer());

            for (int i = 0; i < RouteTrail.Samples; i++)
            {
                Assert.That(m_Line.GetPosition(i).y, Is.LessThan(0.1f),
                    "a route is a mark on the ground, and one drawn at hip height would z-fight the bodies");
            }
        }
    }
}
