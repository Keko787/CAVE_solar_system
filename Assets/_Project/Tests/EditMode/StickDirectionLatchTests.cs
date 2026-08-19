using Stadium3D.CaveBox;
using NUnit.Framework;
using UnityEngine;

namespace Stadium3D.CaveBox.Tests
{
    /// <summary>
    /// Stick-to-step timing. A held stick that stepped once per frame would cross a panel in a blink, so the
    /// feel here — one step on push, a pause, then a measured repeat — is the difference between navigation
    /// that works and navigation that is unusable. Delta time is injected so it is pinned exactly rather
    /// than eyeballed.
    /// </summary>
    public class StickDirectionLatchTests
    {
        static readonly Vector2 Right = new(1f, 0f);

        [Test]
        public void PushingTheStick_StepsOnceThenWaits()
        {
            var latch = new StickDirectionLatch();

            Assert.IsTrue(latch.Tick(Right, 0.016f, out var first));
            Assert.AreEqual(FocusDirection.Right, first);

            Assert.IsFalse(latch.Tick(Right, 0.2f, out _), "still inside the hold delay");
            Assert.IsFalse(latch.Tick(Right, 0.2f, out _), "0.4s held, delay is 0.45s");
        }

        [Test]
        public void HoldingPastTheDelay_RepeatsAtTheInterval()
        {
            var latch = new StickDirectionLatch();
            latch.Tick(Right, 0.016f, out _);

            Assert.IsFalse(latch.Tick(Right, 0.3f, out _));
            Assert.IsTrue(latch.Tick(Right, 0.2f, out _), "0.5s held clears the 0.45s delay");

            Assert.IsFalse(latch.Tick(Right, 0.1f, out _), "interval is 0.14s");
            Assert.IsTrue(latch.Tick(Right, 0.1f, out _), "0.2s since the last repeat");
        }

        [Test]
        public void ReleasingToCentre_ClearsSoTheNextPushStepsImmediately()
        {
            var latch = new StickDirectionLatch();
            latch.Tick(Right, 0.016f, out _);

            Assert.IsFalse(latch.Tick(Vector2.zero, 0.016f, out _));
            Assert.IsNull(latch.Current);

            Assert.IsTrue(latch.Tick(Right, 0.016f, out _),
                "a fresh push steps at once — it does not have to wait out the previous hold");
        }

        [Test]
        public void ChangingDirection_StepsImmediatelyWithoutWaiting()
        {
            var latch = new StickDirectionLatch();
            latch.Tick(Right, 0.016f, out _);
            latch.Tick(Right, 0.1f, out _);

            Assert.IsTrue(latch.Tick(new Vector2(0f, 1f), 0.016f, out var direction),
                "flicking from right to up must not be swallowed by the right-hand hold clock");
            Assert.AreEqual(FocusDirection.Up, direction);
        }

        [Test]
        public void TheDominantAxisWins_SoADiagonalStepsOneWay()
        {
            var latch = new StickDirectionLatch();

            Assert.IsTrue(latch.Tick(new Vector2(0.9f, 0.8f), 0.016f, out var mostlyRight));
            Assert.AreEqual(FocusDirection.Right, mostlyRight);

            latch.Reset();
            Assert.IsTrue(latch.Tick(new Vector2(0.8f, 0.9f), 0.016f, out var mostlyUp));
            Assert.AreEqual(FocusDirection.Up, mostlyUp);
        }

        [Test]
        public void EachDirectionIsReported()
        {
            var latch = new StickDirectionLatch();

            latch.Tick(new Vector2(-1f, 0f), 0.016f, out var left);
            Assert.AreEqual(FocusDirection.Left, left);

            latch.Reset();
            latch.Tick(new Vector2(0f, -1f), 0.016f, out var down);
            Assert.AreEqual(FocusDirection.Down, down);
        }

        [Test]
        public void BetweenTheRearmBandAndTheThreshold_TheHeldDirectionSticks()
        {
            var latch = new StickDirectionLatch();
            latch.Tick(Right, 0.016f, out _);

            // Drifting back through the middle band must not chatter between "right" and "nothing", which
            // would fire a step every time the stick wobbled.
            Assert.IsFalse(latch.Tick(new Vector2(0.45f, 0f), 0.016f, out _));
            Assert.AreEqual(FocusDirection.Right, latch.Current);

            Assert.IsFalse(latch.Tick(new Vector2(0.1f, 0f), 0.016f, out _));
            Assert.IsNull(latch.Current, "past the re-arm band it does clear");
        }

        [Test]
        public void ARestingStickInsideTheDeadZone_NeverSteps()
        {
            var latch = new StickDirectionLatch();

            Assert.IsFalse(latch.Tick(new Vector2(0.2f, 0.15f), 0.016f, out _));
            Assert.IsFalse(latch.Tick(new Vector2(0.2f, 0.15f), 1f, out _),
                "a stick sitting off-centre must not start repeating");
            Assert.IsNull(latch.Current);
        }

        [Test]
        public void Reset_ClearsTheHeldDirection()
        {
            var latch = new StickDirectionLatch();
            latch.Tick(Right, 0.016f, out _);

            latch.Reset();

            Assert.IsNull(latch.Current);
            Assert.IsTrue(latch.Tick(Right, 0.016f, out _), "after a reset the same push counts again");
        }
    }
}
