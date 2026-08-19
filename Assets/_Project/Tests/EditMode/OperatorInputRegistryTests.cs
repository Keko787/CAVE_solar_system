using Stadium3D.Operators;
using NUnit.Framework;

namespace Stadium3D.Operators.Tests
{
    /// <summary>
    /// The two-operator contract. The secondary-operator feature is specifically <b>not</b> a handoff:
    /// the wand and the pad are both registered and both readable on the same frame, and neither shadows
    /// the other. That is the property most at risk of quietly regressing into "last one wins", so it is
    /// asserted directly.
    /// </summary>
    public class OperatorInputRegistryTests
    {
        [Test]
        public void HoldsBothOperatorsAtOnce()
        {
            OperatorInputRegistry.Clear();
            var wand = new OperatorTestInput { DeviceName = "Wand" };
            var pad = new OperatorTestInput { DeviceName = "Pad" };

            OperatorInputRegistry.Register(OperatorRole.Primary, wand);
            OperatorInputRegistry.Register(OperatorRole.Secondary, pad);

            Assert.AreEqual(2, OperatorInputRegistry.Count);
            Assert.AreSame(wand, OperatorInputRegistry.Get(OperatorRole.Primary));
            Assert.AreSame(pad, OperatorInputRegistry.Get(OperatorRole.Secondary));

            // Both act on the same frame — registering the pad must not have displaced the wand.
            wand.Press(0);
            pad.Press(1);
            Assert.IsTrue(OperatorInputRegistry.Get(OperatorRole.Primary).Held(0));
            Assert.IsTrue(OperatorInputRegistry.Get(OperatorRole.Secondary).Held(1));

            OperatorInputRegistry.Clear();
        }

        [Test]
        public void UnclaimedRole_ReadsAsNothing()
        {
            OperatorInputRegistry.Clear();

            Assert.IsNull(OperatorInputRegistry.Get(OperatorRole.Secondary));
            Assert.IsFalse(OperatorInputRegistry.TryGet(OperatorRole.Secondary, out _));
            Assert.IsFalse(OperatorInputRegistry.IsConnected(OperatorRole.Secondary));
        }

        [Test]
        public void Unregister_OnlyReleasesTheRoleItStillHolds()
        {
            OperatorInputRegistry.Clear();
            var first = new OperatorTestInput { DeviceName = "First" };
            var second = new OperatorTestInput { DeviceName = "Second" };
            OperatorInputRegistry.Register(OperatorRole.Secondary, first);

            // A stale component disabling after being replaced must not tear down the live source.
            OperatorInputRegistry.Unregister(OperatorRole.Secondary, second);
            Assert.AreSame(first, OperatorInputRegistry.Get(OperatorRole.Secondary));

            OperatorInputRegistry.Unregister(OperatorRole.Secondary, first);
            Assert.IsNull(OperatorInputRegistry.Get(OperatorRole.Secondary));

            OperatorInputRegistry.Clear();
        }

        [Test]
        public void RegisteringTwiceForOneRole_KeepsTheLatest()
        {
            OperatorInputRegistry.Clear();
            var first = new OperatorTestInput { DeviceName = "First" };
            var second = new OperatorTestInput { DeviceName = "Second" };

            OperatorInputRegistry.Register(OperatorRole.Secondary, first);
            OperatorInputRegistry.Register(OperatorRole.Secondary, second);

            Assert.AreEqual(1, OperatorInputRegistry.Count);
            Assert.AreSame(second, OperatorInputRegistry.Get(OperatorRole.Secondary));

            OperatorInputRegistry.Clear();
        }

        [Test]
        public void ReRegisteringTheSameSource_IsANoOp()
        {
            OperatorInputRegistry.Clear();
            var pad = new OperatorTestInput();

            OperatorInputRegistry.Register(OperatorRole.Secondary, pad);
            OperatorInputRegistry.Register(OperatorRole.Secondary, pad);

            Assert.AreEqual(1, OperatorInputRegistry.Count);
            Assert.AreSame(pad, OperatorInputRegistry.Get(OperatorRole.Secondary));

            OperatorInputRegistry.Clear();
        }

        [Test]
        public void NullSource_IsIgnored()
        {
            OperatorInputRegistry.Clear();

            OperatorInputRegistry.Register(OperatorRole.Primary, null);

            Assert.AreEqual(0, OperatorInputRegistry.Count);
        }

        [Test]
        public void IsConnected_FollowsTheSourcesOwnState()
        {
            OperatorInputRegistry.Clear();
            var pad = new OperatorTestInput { IsConnected = false };
            OperatorInputRegistry.Register(OperatorRole.Secondary, pad);

            Assert.IsFalse(OperatorInputRegistry.IsConnected(OperatorRole.Secondary),
                "a registered source with no pad plugged in is registered, not connected");

            pad.IsConnected = true;
            Assert.IsTrue(OperatorInputRegistry.IsConnected(OperatorRole.Secondary));

            OperatorInputRegistry.Clear();
        }

        [Test]
        public void Clear_ReleasesEveryRole()
        {
            OperatorInputRegistry.Clear();
            OperatorInputRegistry.Register(OperatorRole.Primary, new OperatorTestInput());
            OperatorInputRegistry.Register(OperatorRole.Secondary, new OperatorTestInput());

            OperatorInputRegistry.Clear();

            Assert.AreEqual(0, OperatorInputRegistry.Count);
            Assert.IsNull(OperatorInputRegistry.Get(OperatorRole.Primary));
            Assert.IsNull(OperatorInputRegistry.Get(OperatorRole.Secondary));
        }
    }
}
