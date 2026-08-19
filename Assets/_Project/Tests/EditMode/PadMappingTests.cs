using Stadium3D.Operators;
using NUnit.Framework;
using UnityEngine;

namespace Stadium3D.Operators.Tests
{
    /// <summary>
    /// The raw-index → canonical-control translation, verified without a pad. This is where the two
    /// awkward shapes a real Xbox pad arrives in are pinned down: a D-pad that is one POV-hat axis rather
    /// than four buttons, and two triggers summed onto a single axis so they cancel each other. Both come
    /// from reading the pad through DirectInput, and both are invisible until something reads wrong in
    /// the CAVE — so they are settled here instead.
    /// </summary>
    public class PadMappingTests
    {
        [Test]
        public void UnityDefault_IsTheIdentityMapping()
        {
            var map = PadMapping.UnityDefault();

            for (int i = 0; i < PadControls.ButtonCount; i++)
                Assert.AreEqual(i, map.Button((PadControl)i), $"canonical control {i} must be raw button {i}");

            Assert.AreEqual(PadDpadSource.Buttons, map.DpadSource);
            Assert.AreEqual(PadTriggerSource.SeparateAxes, map.TriggerSource);
            Assert.IsFalse(map.TriggersAreExclusive, "Input System reports both triggers independently");
        }

        [Test]
        public void Button_ReadsThroughTheMappedRawIndex()
        {
            var map = PadMapping.UnityDefault();
            map.SetButton(PadControl.A, 7);
            var device = new OperatorTestInput();

            device.Press(7);
            Assert.IsTrue(map.ReadHeld(device, PadControl.A));

            device.Release(7).Press(0);
            Assert.IsFalse(map.ReadHeld(device, PadControl.A), "raw button 0 is no longer A on this pad");
        }

        [Test]
        public void UnboundControl_ReadsFalse()
        {
            var map = PadMapping.UnityDefault();
            map.SetButton(PadControl.Y, -1);
            var device = new OperatorTestInput().Press(3);

            Assert.IsFalse(map.ReadHeld(device, PadControl.Y));
            Assert.IsFalse(map.IsButtonBacked(PadControl.Y));
        }

        [Test]
        public void DeadZone_ZeroesSmallInputAndRampsTheRestFromZero()
        {
            var map = PadMapping.UnityDefault();
            map.SetAxis(PadAxis.LeftStickX, PadMapping.AxisBinding.At(0, 1f, 0.3f));
            var device = new OperatorTestInput();

            device.SetAxis(0, 0.2f);
            Assert.AreEqual(0f, map.ReadAxis(device, PadAxis.LeftStickX), "inside the dead zone reads as centred");

            // 0.65 sits 0.35 past a 0.3 dead zone, across a 0.7 remaining range — half travel, not 0.65,
            // so the stick doesn't jump to a third of its range the instant it leaves centre.
            device.SetAxis(0, 0.65f);
            Assert.That(map.ReadAxis(device, PadAxis.LeftStickX), Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void DeadZone_KeepsTheSignWhenTheStickIsPushedTheOtherWay()
        {
            var map = PadMapping.UnityDefault();
            map.SetAxis(PadAxis.LeftStickX, PadMapping.AxisBinding.At(0, 1f, 0.3f));
            var device = new OperatorTestInput().SetAxis(0, -0.65f);

            // The ramp works on magnitude and restores the sign afterwards; every stick in both shipped
            // mappings takes this path, and half of every stick's travel is negative.
            Assert.That(map.ReadAxis(device, PadAxis.LeftStickX), Is.EqualTo(-0.5f).Within(1e-4f));
        }

        [Test]
        public void NegativeScale_FlipsAnAxisThatReportsDownPositive()
        {
            var map = PadMapping.UnityDefault();
            map.SetAxis(PadAxis.LeftStickY, PadMapping.AxisBinding.At(1, -1f, 0f));
            var device = new OperatorTestInput().SetAxis(1, 0.5f);

            Assert.That(map.ReadAxis(device, PadAxis.LeftStickY), Is.EqualTo(-0.5f).Within(1e-4f));
        }

        [Test]
        public void CombinedTriggerAxis_SplitsOneAxisIntoTwoTriggers()
        {
            var map = PadMapping.UnityDefault();
            map.SetCombinedTriggerAxis(2);
            var device = new OperatorTestInput();

            device.SetAxis(2, 0.7f);
            Assert.That(map.ReadAxis(device, PadAxis.LeftTrigger), Is.EqualTo(0.7f).Within(1e-4f));
            Assert.AreEqual(0f, map.ReadAxis(device, PadAxis.RightTrigger));

            device.SetAxis(2, -0.7f);
            Assert.That(map.ReadAxis(device, PadAxis.RightTrigger), Is.EqualTo(0.7f).Within(1e-4f));
            Assert.AreEqual(0f, map.ReadAxis(device, PadAxis.LeftTrigger));
        }

        [Test]
        public void CombinedTriggerAxis_InvertedSwapsWhichTriggerOwnsWhichDirection()
        {
            var map = PadMapping.UnityDefault();
            map.SetCombinedTriggerAxis(2, inverted: true);
            var device = new OperatorTestInput().SetAxis(2, 0.7f);

            // Which trigger drives the axis positive differs between pads, so the flip has to work.
            Assert.That(map.ReadAxis(device, PadAxis.RightTrigger), Is.EqualTo(0.7f).Within(1e-4f));
            Assert.AreEqual(0f, map.ReadAxis(device, PadAxis.LeftTrigger));
        }

        [Test]
        public void CombinedTriggerAxis_IsFlaggedExclusiveSoGesturesCanAvoidIt()
        {
            var map = PadMapping.UnityDefault();
            Assert.IsFalse(map.TriggersAreExclusive);

            map.SetCombinedTriggerAxis(2);
            Assert.IsTrue(map.TriggersAreExclusive,
                "a control scheme holding LT while clicking RT is impossible on this pad and must be able to know");
        }

        [Test]
        public void TriggerWithNoButtonBound_ActsAsADigitalPressPastThePressPoint()
        {
            var map = PadMapping.UnityDefault();
            map.SetButton(PadControl.LeftTrigger, -1);
            map.SetAxis(PadAxis.LeftTrigger, PadMapping.AxisBinding.At(4, 1f, 0f));
            var device = new OperatorTestInput();

            device.SetAxis(4, 0.4f);
            Assert.IsFalse(map.ReadHeld(device, PadControl.LeftTrigger), "a feathered trigger is not a press");

            device.SetAxis(4, 0.6f);
            Assert.IsTrue(map.ReadHeld(device, PadControl.LeftTrigger));
        }

        [Test]
        public void HatAxis_LightsEachCardinalDirection()
        {
            var map = PadMapping.UnityDefault();
            map.SetHat(5);
            var device = new OperatorTestInput();

            device.SetAxis(5, 0f);       // 0 degrees
            Assert.IsTrue(map.ReadHeld(device, PadControl.DpadUp));
            Assert.IsFalse(map.ReadHeld(device, PadControl.DpadDown));

            device.SetAxis(5, 0.25f);    // 90 degrees
            Assert.IsTrue(map.ReadHeld(device, PadControl.DpadRight));
            Assert.IsFalse(map.ReadHeld(device, PadControl.DpadLeft));

            device.SetAxis(5, 0.5f);     // 180 degrees
            Assert.IsTrue(map.ReadHeld(device, PadControl.DpadDown));
            Assert.IsFalse(map.ReadHeld(device, PadControl.DpadUp));

            device.SetAxis(5, 0.75f);    // 270 degrees
            Assert.IsTrue(map.ReadHeld(device, PadControl.DpadLeft));
            Assert.IsFalse(map.ReadHeld(device, PadControl.DpadRight));
        }

        [Test]
        public void HatAxis_DiagonalLightsBothNeighboursLikeARealDpad()
        {
            var map = PadMapping.UnityDefault();
            map.SetHat(5);
            var device = new OperatorTestInput().SetAxis(5, 0.125f);   // 45 degrees, up-right

            Assert.IsTrue(map.ReadHeld(device, PadControl.DpadUp));
            Assert.IsTrue(map.ReadHeld(device, PadControl.DpadRight));
            Assert.IsFalse(map.ReadHeld(device, PadControl.DpadDown));
            Assert.IsFalse(map.ReadHeld(device, PadControl.DpadLeft));
        }

        [Test]
        public void HatAxis_CentredSentinelReadsNoDirection()
        {
            var map = PadMapping.UnityDefault();
            map.SetHat(5);
            var device = new OperatorTestInput().SetAxis(5, -1f);

            Assert.IsFalse(map.ReadHeld(device, PadControl.DpadUp),
                "the resting hat must not read as up — the sentinel has to sit outside the angular range");
            Assert.IsFalse(map.ReadHeld(device, PadControl.DpadRight));
            Assert.IsFalse(map.ReadHeld(device, PadControl.DpadDown));
            Assert.IsFalse(map.ReadHeld(device, PadControl.DpadLeft));
        }

        [Test]
        public void HatDrivenDpad_IsNotButtonBacked()
        {
            var map = PadMapping.UnityDefault();
            map.SetHat(5);

            Assert.IsFalse(map.IsButtonBacked(PadControl.DpadUp),
                "even with a button index still set, the hat is what drives the direction");
            Assert.IsTrue(map.IsButtonBacked(PadControl.A));
        }

        [Test]
        public void DirectInputXboxDefault_UsesAHatAndSharedTriggers()
        {
            var map = PadMapping.DirectInputXboxDefault();

            Assert.AreEqual(0, map.Button(PadControl.A));
            Assert.AreEqual(3, map.Button(PadControl.Y));
            Assert.AreEqual(PadDpadSource.HatAxis, map.DpadSource);
            Assert.IsTrue(map.TriggersAreExclusive);
            Assert.AreEqual(-1, map.Button(PadControl.LeftTrigger), "triggers come off the shared axis, not a button");
        }

        [Test]
        public void DirectInputXboxDefault_ActuallyReadsTheAxesItClaimsToBind()
        {
            // Pins the axis indices behaviourally rather than through accessors: these are the numbers the
            // CAVE probe has to confirm or correct, and a silent edit to them would otherwise go unnoticed
            // until a pad in the CAVE did the wrong thing.
            var map = PadMapping.DirectInputXboxDefault();
            var device = new OperatorTestInput();

            device.SetAxis(5, 0.25f);   // POV hat, 90 degrees
            Assert.IsTrue(map.ReadHeld(device, PadControl.DpadRight), "the hat is axis 5");

            device.SetAxis(2, 0.8f);    // shared trigger axis
            Assert.That(map.ReadAxis(device, PadAxis.LeftTrigger), Is.EqualTo(0.8f).Within(1e-4f),
                "positive travel on the shared axis is the left trigger");
            Assert.AreEqual(0f, map.ReadAxis(device, PadAxis.RightTrigger));

            device.SetAxis(1, 0.5f);    // left stick Y, which DirectInput reports down-positive
            Assert.Less(map.ReadAxis(device, PadAxis.LeftStickY), 0f,
                "pushing a DirectInput stick down must read as negative once flipped to up-positive");
        }

        [Test]
        public void NullDevice_ReadsNothingRatherThanThrowing()
        {
            var map = PadMapping.UnityDefault();

            Assert.IsFalse(map.ReadHeld(null, PadControl.A));
            Assert.AreEqual(0f, map.ReadAxis(null, PadAxis.LeftStickX));
        }

        [Test]
        public void GamepadMapAsset_CarriesThePopulatedMapping()
        {
            var asset = ScriptableObject.CreateInstance<GamepadMap>();

            GamepadMap.PopulateDirectInputXboxDefault(asset);
            Assert.IsTrue(asset.Mapping.TriggersAreExclusive);

            GamepadMap.PopulateUnityDefault(asset);
            Assert.IsFalse(asset.Mapping.TriggersAreExclusive);
            Assert.AreEqual((int)PadControl.Menu, asset.Mapping.Button(PadControl.Menu));

            Object.DestroyImmediate(asset);
        }
    }
}
