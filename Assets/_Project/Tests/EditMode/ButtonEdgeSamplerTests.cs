using System;
using Stadium3D.Operators;
using NUnit.Framework;

namespace Stadium3D.Operators.Tests
{
    /// <summary>
    /// Edge semantics for devices that only report level state. Every gamepad consumer depends on these
    /// four lines behaving exactly right, and they are cheap to get subtly wrong (a press that repeats
    /// every frame, a release that never fires), so they are pinned here rather than in a CAVE session.
    /// </summary>
    public class ButtonEdgeSamplerTests
    {
        static Func<int, bool> Probe(bool[] state) => i => i >= 0 && i < state.Length && state[i];

        [Test]
        public void Press_IsReportedOnlyOnTheTransitionFrame()
        {
            var state = new bool[3];
            var sampler = new ButtonEdgeSampler();
            var probe = Probe(state);

            sampler.Sample(probe, 3);
            Assert.IsFalse(sampler.PressedThisFrame(1), "nothing was pressed yet");

            state[1] = true;
            sampler.Sample(probe, 3);
            Assert.IsTrue(sampler.PressedThisFrame(1), "the press frame reports a press");
            Assert.IsTrue(sampler.Held(1));
            Assert.IsFalse(sampler.ReleasedThisFrame(1));

            sampler.Sample(probe, 3);
            Assert.IsFalse(sampler.PressedThisFrame(1), "holding is not pressing again");
            Assert.IsTrue(sampler.Held(1));
        }

        [Test]
        public void Release_IsReportedOnlyOnTheTransitionFrame()
        {
            var state = new bool[3] { false, true, false };
            var sampler = new ButtonEdgeSampler();
            var probe = Probe(state);

            sampler.Sample(probe, 3);
            state[1] = false;
            sampler.Sample(probe, 3);

            Assert.IsTrue(sampler.ReleasedThisFrame(1));
            Assert.IsFalse(sampler.Held(1));

            sampler.Sample(probe, 3);
            Assert.IsFalse(sampler.ReleasedThisFrame(1), "staying up is not releasing again");
        }

        [Test]
        public void DeviceVanishing_ReleasesEverythingRatherThanLatchingItDown()
        {
            var state = new bool[2] { true, true };
            var sampler = new ButtonEdgeSampler();
            sampler.Sample(Probe(state), 2);
            Assert.IsTrue(sampler.Held(0));

            // A pad unplugged mid-session: the source has no probe to offer any more.
            sampler.Sample(null, 2);

            Assert.IsFalse(sampler.Held(0));
            Assert.IsTrue(sampler.ReleasedThisFrame(0), "an unplugged pad's held buttons must come up");
        }

        [Test]
        public void OutOfRangeIndices_ReadFalseInsteadOfThrowing()
        {
            var sampler = new ButtonEdgeSampler();
            sampler.Sample(Probe(new bool[2] { true, true }), 2);

            Assert.IsFalse(sampler.Held(-1));
            Assert.IsFalse(sampler.Held(99));
            Assert.IsFalse(sampler.PressedThisFrame(99));
            Assert.IsFalse(sampler.ReleasedThisFrame(99));
        }

        [Test]
        public void Reset_ClearsLatchedStateSoTheNextSampleReadsAsAFreshPress()
        {
            var state = new bool[1] { true };
            var sampler = new ButtonEdgeSampler();
            var probe = Probe(state);

            sampler.Sample(probe, 1);
            sampler.Sample(probe, 1);
            Assert.IsFalse(sampler.PressedThisFrame(0), "held, not newly pressed");

            sampler.Reset();
            sampler.Sample(probe, 1);
            Assert.IsTrue(sampler.PressedThisFrame(0), "after a reset the still-held button is a new press");
        }

        [Test]
        public void GrowingButtonCount_KeepsExistingStateAndReadsNewSlotsAsUp()
        {
            var small = new bool[1] { true };
            var sampler = new ButtonEdgeSampler();
            sampler.Sample(Probe(small), 1);

            var large = new bool[4] { true, false, false, false };
            sampler.Sample(Probe(large), 4);

            Assert.IsTrue(sampler.Held(0));
            Assert.IsFalse(sampler.PressedThisFrame(0), "button 0 was already down before the count grew");
            Assert.IsFalse(sampler.Held(3));
            Assert.AreEqual(4, sampler.Count);
        }
    }
}
