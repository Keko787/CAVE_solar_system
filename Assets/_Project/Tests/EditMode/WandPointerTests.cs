using Stadium3D.CaveBox;
using Stadium3D.Operators;
using Stadium3D.Operators.Tests;   // OperatorTestInput — the shared hand-driven input fake
using NUnit.Framework;
using UnityEngine;

namespace Stadium3D.CaveBox.Tests
{
    /// <summary>
    /// How a cursor finds its operator's input. Two properties matter and neither is obvious from reading
    /// the class: an existing scene that predates the operator layer must keep working with no rewiring at
    /// all, and an unclaimed <em>secondary</em> role must leave its cursor inert rather than quietly
    /// falling back to the wand and becoming a second wand cursor.
    /// </summary>
    public class WandPointerTests
    {
        static WandPointer MakePointer(out GameObject go)
        {
            go = new GameObject("cursor", typeof(WandPointer));
            return go.GetComponent<WandPointer>();
        }

        [Test]
        public void PrimaryCursor_FallsBackToTheSharedWandWhenNothingIsRegistered()
        {
            OperatorInputRegistry.Clear();
            var pointer = MakePointer(out var go);

            Assert.AreEqual(OperatorRole.Primary, pointer.Role, "the wand cursor is the default");
            Assert.AreSame(WandInput.Shared, pointer.Input,
                "a scene built before the operator layer existed must still drive the wand cursor");

            Object.DestroyImmediate(go);
            OperatorInputRegistry.Clear();
        }

        [Test]
        public void SecondaryCursor_StaysInertWhenNothingIsRegistered()
        {
            OperatorInputRegistry.Clear();
            var pointer = MakePointer(out var go);
            pointer.SetRole(OperatorRole.Secondary);

            Assert.IsNull(pointer.Input,
                "an unclaimed pad cursor must not fall back to the wand — that would put a second cursor " +
                "on the wand's ray and click things the primary operator did not click");

            Object.DestroyImmediate(go);
            OperatorInputRegistry.Clear();
        }

        [Test]
        public void RegisteredSource_TakesPrecedenceOverTheFallback()
        {
            OperatorInputRegistry.Clear();
            var registered = new OperatorTestInput { DeviceName = "Registered" };
            OperatorInputRegistry.Register(OperatorRole.Primary, registered);
            var pointer = MakePointer(out var go);

            Assert.AreSame(registered, pointer.Input, "an explicitly wired source wins over the fallback");

            Object.DestroyImmediate(go);
            OperatorInputRegistry.Clear();
        }

        [Test]
        public void EachCursorResolvesItsOwnOperator()
        {
            OperatorInputRegistry.Clear();
            var wand = new OperatorTestInput { DeviceName = "Wand" };
            var pad = new OperatorTestInput { DeviceName = "Pad" };
            OperatorInputRegistry.Register(OperatorRole.Primary, wand);
            OperatorInputRegistry.Register(OperatorRole.Secondary, pad);

            var primary = MakePointer(out var primaryGo);
            var secondary = MakePointer(out var secondaryGo);
            secondary.SetRole(OperatorRole.Secondary);

            Assert.AreSame(wand, primary.Input);
            Assert.AreSame(pad, secondary.Input);

            Object.DestroyImmediate(primaryGo);
            Object.DestroyImmediate(secondaryGo);
            OperatorInputRegistry.Clear();
        }

        [Test]
        public void SwitchingRole_RepointsTheCursorAtTheOtherOperator()
        {
            OperatorInputRegistry.Clear();
            var wand = new OperatorTestInput { DeviceName = "Wand" };
            var pad = new OperatorTestInput { DeviceName = "Pad" };
            OperatorInputRegistry.Register(OperatorRole.Primary, wand);
            OperatorInputRegistry.Register(OperatorRole.Secondary, pad);
            var pointer = MakePointer(out var go);

            Assert.AreSame(wand, pointer.Input);

            pointer.SetRole(OperatorRole.Secondary);

            Assert.AreEqual(OperatorRole.Secondary, pointer.Role);
            Assert.AreSame(pad, pointer.Input, "the cursor now reads the pad, not the wand");

            Object.DestroyImmediate(go);
            OperatorInputRegistry.Clear();
        }
    }
}
