using Stadium3D.CaveBox;
using NUnit.Framework;
using UnityEngine;

namespace Stadium3D.CaveBox.Tests
{
    /// <summary>
    /// The multi-cursor hover contract. With two operators pointing at the same chips at once, a single
    /// hover flag would let whichever cursor left last switch off a highlight the other cursor is still
    /// holding — the chip goes dark while someone is still aiming at it. These tests pin the per-cursor
    /// behaviour that prevents it, and keep focus (a ring) independent of hover (a tint) so both operators
    /// stay legible at the same time.
    /// </summary>
    public class WandButtonTests
    {
        static WandButton MakeButton(out GameObject go)
        {
            go = new GameObject("chip", typeof(RectTransform), typeof(WandButton));
            return go.GetComponent<WandButton>();
        }

        [Test]
        public void TwoCursorsOnOneChip_StayHoveredUntilBothLeave()
        {
            var button = MakeButton(out var go);
            var wandCursor = new object();
            var padCursor = new object();

            button.SetHover(wandCursor, true);
            button.SetHover(padCursor, true);
            Assert.AreEqual(2, button.HoverCount);

            button.SetHover(wandCursor, false);
            Assert.IsTrue(button.IsHovered,
                "the pad cursor is still on the chip — the wand looking away must not clear its highlight");

            button.SetHover(padCursor, false);
            Assert.IsFalse(button.IsHovered);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void SameCursorHoveringTwice_CountsOnce()
        {
            var button = MakeButton(out var go);
            var cursor = new object();

            button.SetHover(cursor, true);
            button.SetHover(cursor, true);
            Assert.AreEqual(1, button.HoverCount, "a cursor re-announcing itself must not double-count");

            button.SetHover(cursor, false);
            Assert.IsFalse(button.IsHovered, "one leave from one cursor is enough");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void UnhoveringACursorThatWasNeverThere_IsHarmless()
        {
            var button = MakeButton(out var go);
            var present = new object();
            var stranger = new object();

            button.SetHover(present, true);
            button.SetHover(stranger, false);

            Assert.IsTrue(button.IsHovered, "a stray leave must not drop somebody else's hover");
            Assert.AreEqual(1, button.HoverCount);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void LegacySingleCursorOverload_SharesStateWithTheKeyedOne()
        {
            var button = MakeButton(out var go);

            button.SetHover(true);
            Assert.IsTrue(button.IsHovered);

            button.SetHover(false);
            Assert.IsFalse(button.IsHovered);
            Assert.AreEqual(0, button.HoverCount, "the two overloads must not leave separate leaked state");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void LegacyOverloadAndAKeyedCursor_AreIndependentHolders()
        {
            var button = MakeButton(out var go);
            var cursor = new object();

            button.SetHover(true);            // anonymous holder
            button.SetHover(cursor, true);    // identified holder
            Assert.AreEqual(2, button.HoverCount);

            button.SetHover(false);
            Assert.IsTrue(button.IsHovered, "the identified cursor is still on the chip");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ClearHover_ReleasesJustThatCursor()
        {
            var button = MakeButton(out var go);
            var leaving = new object();
            var staying = new object();
            button.SetHover(leaving, true);
            button.SetHover(staying, true);

            button.ClearHover(leaving);

            Assert.AreEqual(1, button.HoverCount);
            Assert.IsTrue(button.IsHovered);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Focus_IsIndependentOfHover()
        {
            var button = MakeButton(out var go);
            var cursor = new object();

            button.SetFocused(true);
            Assert.IsTrue(button.IsFocused);
            Assert.IsFalse(button.IsHovered, "focusing must not fake a hover");

            button.SetHover(cursor, true);
            Assert.IsTrue(button.IsFocused, "the other operator hovering must not steal focus");

            button.SetHover(cursor, false);
            Assert.IsTrue(button.IsFocused, "and their leaving must not drop it either");

            button.SetFocused(false);
            Assert.IsFalse(button.IsFocused);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void FocusRing_IsBuiltOutsideTheChipSoItCannotCoverTheLabel()
        {
            var button = MakeButton(out var go);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(200f, 60f);

            button.SetFocused(true);

            Assert.AreEqual(4, go.transform.childCount, "the ring is four edge bars");
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var bar = (RectTransform)go.transform.GetChild(i);
                Assert.IsTrue(bar.gameObject.activeSelf, "a focused chip shows every bar");
                Assert.AreEqual(go.layer, bar.gameObject.layer, "bars share the chip's layer");
            }

            button.SetFocused(false);
            for (int i = 0; i < go.transform.childCount; i++)
                Assert.IsFalse(go.transform.GetChild(i).gameObject.activeSelf,
                    "unfocusing hides the ring rather than destroying and rebuilding it");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void UnfocusedChip_BuildsNoRingAtAll()
        {
            var button = MakeButton(out var go);

            button.SetFocused(false);

            Assert.AreEqual(0, go.transform.childCount,
                "chips are numerous; a chip that is never focused must not pay for a ring");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Click_RaisesTheEventRegardlessOfWhichCursorIsHovering()
        {
            var button = MakeButton(out var go);
            int clicks = 0;
            button.Clicked += () => clicks++;

            button.SetHover(new object(), true);
            button.Click();
            button.Click();

            Assert.AreEqual(2, clicks);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void SelectedState_IsUnaffectedByHover()
        {
            var button = MakeButton(out var go);
            var cursor = new object();

            button.SetSelected(true);
            button.SetHover(cursor, true);
            button.SetHover(cursor, false);

            Assert.IsTrue(button.IsSelected, "hovering is not selecting");

            Object.DestroyImmediate(go);
        }
    }
}
