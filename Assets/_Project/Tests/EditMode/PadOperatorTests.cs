using Stadium3D.Operators;
using NUnit.Framework;

namespace Stadium3D.Operators.Tests
{
    /// <summary>
    /// The semantic pad read. The promise being tested is that a consumer cannot tell how a control is
    /// wired: a hat direction and an analog trigger must produce press and release edges exactly like a
    /// plain button, because from G3 onward the focus cursor and the command bindings treat all sixteen
    /// canonical controls the same way.
    /// </summary>
    public class PadOperatorTests
    {
        // EditMode tests never advance Time.frameCount, so the operator's frame source is injected.
        sealed class FrameClock
        {
            public int Frame;
            public int Read() => Frame;
            public void Advance() => Frame++;
        }

        static PadOperator Make(OperatorTestInput device, PadMapping mapping, FrameClock clock) =>
            new(device, mapping, clock.Read);

        [Test]
        public void Press_IsReportedOnceThenReadsAsHeld()
        {
            var clock = new FrameClock();
            var device = new OperatorTestInput();
            var pad = Make(device, PadMapping.UnityDefault(), clock);

            clock.Advance();
            Assert.IsFalse(pad.PressedThisFrame(PadControl.A));

            device.Press((int)PadControl.A);
            clock.Advance();
            Assert.IsTrue(pad.PressedThisFrame(PadControl.A));
            Assert.IsTrue(pad.Held(PadControl.A));

            clock.Advance();
            Assert.IsFalse(pad.PressedThisFrame(PadControl.A), "holding A is not pressing it again");
            Assert.IsTrue(pad.Held(PadControl.A));

            device.Release((int)PadControl.A);
            clock.Advance();
            Assert.IsTrue(pad.ReleasedThisFrame(PadControl.A));
            Assert.IsFalse(pad.Held(PadControl.A));
        }

        [Test]
        public void RepeatedReadsWithinOneFrame_ReturnTheSameEdge()
        {
            var clock = new FrameClock();
            var device = new OperatorTestInput();
            var pad = Make(device, PadMapping.UnityDefault(), clock);

            clock.Advance();
            pad.Held(PadControl.A);          // establishes the baseline sample for this frame
            device.Press((int)PadControl.A);
            clock.Advance();

            // Two consumers reading the same press in the same frame must both see it — the sample is
            // per-frame, not per-read, or whichever consumer asked second would silently miss the click.
            Assert.IsTrue(pad.PressedThisFrame(PadControl.A));
            Assert.IsTrue(pad.PressedThisFrame(PadControl.A));
        }

        [Test]
        public void HatDirection_ProducesEdgesJustLikeAButton()
        {
            var clock = new FrameClock();
            var device = new OperatorTestInput();
            var mapping = PadMapping.UnityDefault();
            mapping.SetHat(5);
            var pad = Make(device, mapping, clock);

            device.SetAxis(5, -1f);   // centred
            clock.Advance();
            Assert.IsFalse(pad.Held(PadControl.DpadUp));

            device.SetAxis(5, 0f);    // pushed up
            clock.Advance();
            Assert.IsTrue(pad.PressedThisFrame(PadControl.DpadUp), "a hat push is a press");

            device.SetAxis(5, -1f);   // released
            clock.Advance();
            Assert.IsTrue(pad.ReleasedThisFrame(PadControl.DpadUp), "letting the hat centre is a release");
        }

        [Test]
        public void AnalogTrigger_ProducesEdgesAtThePressPoint()
        {
            var clock = new FrameClock();
            var device = new OperatorTestInput();
            var mapping = PadMapping.UnityDefault();
            mapping.SetButton(PadControl.RightTrigger, -1);
            mapping.SetAxis(PadAxis.RightTrigger, PadMapping.AxisBinding.At(5, 1f, 0f));
            var pad = Make(device, mapping, clock);

            device.SetAxis(5, 0.2f);
            clock.Advance();
            Assert.IsFalse(pad.Held(PadControl.RightTrigger));

            device.SetAxis(5, 0.9f);
            clock.Advance();
            Assert.IsTrue(pad.PressedThisFrame(PadControl.RightTrigger));

            device.SetAxis(5, 0.1f);
            clock.Advance();
            Assert.IsTrue(pad.ReleasedThisFrame(PadControl.RightTrigger));
        }

        [Test]
        public void Sticks_ReadThroughTheMappingWithYUpPositive()
        {
            var clock = new FrameClock();
            var device = new OperatorTestInput();
            var mapping = PadMapping.UnityDefault();
            mapping.SetAxis(PadAxis.LeftStickX, PadMapping.AxisBinding.At(0, 1f, 0f));
            mapping.SetAxis(PadAxis.LeftStickY, PadMapping.AxisBinding.At(1, -1f, 0f));
            var pad = Make(device, mapping, clock);

            device.SetAxis(0, 0.5f).SetAxis(1, -0.25f);

            Assert.That(pad.LeftStick.x, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(pad.LeftStick.y, Is.EqualTo(0.25f).Within(1e-4f),
                "a device reporting down-positive is flipped so up is positive, like the wand's vertical axis");
        }

        [Test]
        public void NullDevice_IsSafeAndReportsDisconnected()
        {
            var clock = new FrameClock();
            var pad = new PadOperator(null, PadMapping.UnityDefault(), clock.Read);

            clock.Advance();
            Assert.IsFalse(pad.IsConnected);
            Assert.IsFalse(pad.Held(PadControl.A));
            Assert.IsFalse(pad.PressedThisFrame(PadControl.A));
            Assert.AreEqual(0f, pad.Axis(PadAxis.LeftStickX));
        }

        [Test]
        public void DisconnectedDevice_ReportsDisconnected()
        {
            var clock = new FrameClock();
            var device = new OperatorTestInput { IsConnected = false };
            var pad = Make(device, PadMapping.UnityDefault(), clock);

            Assert.IsFalse(pad.IsConnected);
        }

        [Test]
        public void SwappingDevice_ClearsStaleEdgesSoTheNewPadStartsClean()
        {
            var clock = new FrameClock();
            var first = new OperatorTestInput().Press((int)PadControl.A);
            var pad = Make(first, PadMapping.UnityDefault(), clock);

            clock.Advance();
            Assert.IsTrue(pad.Held(PadControl.A));

            var second = new OperatorTestInput().Press((int)PadControl.A);
            pad.SetDevice(second);
            clock.Advance();

            Assert.IsTrue(pad.PressedThisFrame(PadControl.A),
                "a freshly bound pad holding A reads as a new press, not as state carried over from the old one");
        }

        [Test]
        public void SwappingMapping_ReinterpretsTheSameDevice()
        {
            var clock = new FrameClock();
            var device = new OperatorTestInput().Press(7);
            var pad = Make(device, PadMapping.UnityDefault(), clock);

            clock.Advance();
            Assert.IsFalse(pad.Held(PadControl.A), "raw 7 is not A under the identity mapping");

            var remapped = PadMapping.UnityDefault();
            remapped.SetButton(PadControl.A, 7);
            pad.SetMapping(remapped);
            clock.Advance();

            Assert.IsTrue(pad.Held(PadControl.A));
        }

        [Test]
        public void TriggersAreExclusive_FollowsTheMapping()
        {
            var clock = new FrameClock();
            var pad = Make(new OperatorTestInput(), PadMapping.DirectInputXboxDefault(), clock);

            Assert.IsTrue(pad.TriggersAreExclusive);
        }
    }
}
