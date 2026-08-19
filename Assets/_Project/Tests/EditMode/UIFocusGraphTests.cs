using System.Collections.Generic;
using Stadium3D.CaveBox;
using NUnit.Framework;
using UnityEngine;

namespace Stadium3D.CaveBox.Tests
{
    /// <summary>
    /// What focus can currently reach. The rule that matters: a chip on a hidden panel must be unreachable,
    /// or the secondary operator can walk focus onto something nobody can see and click it.
    ///
    /// Two mechanisms enforce that, and only one of them is testable here. Chips join and leave the graph
    /// with <c>WandButton</c>'s OnEnable/OnDisable, which Unity does not call in edit mode for a component
    /// without <c>[ExecuteAlways]</c> — and adding that attribute is not worth it, because <c>Awake</c> would
    /// then add colliders to prefabs at authoring time. So these tests register chips explicitly and exercise
    /// the second mechanism: every read filters on <c>isActiveAndEnabled</c>, which holds whether or not the
    /// callbacks ran. The callback wiring itself is a play-mode observation.
    /// </summary>
    public class UIFocusGraphTests
    {
        static WandButton MakeChip(string name, out GameObject go, Transform parent = null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(WandButton));
            if (parent != null)
                go.transform.SetParent(parent, false);
            var chip = go.GetComponent<WandButton>();
            UIFocusGraph.Register(chip);   // stands in for OnEnable, which edit mode does not run
            return chip;
        }

        static GameObject MakePanel(string name)
        {
            // Canvas is the panel boundary — it is what a CAVE wall's world-space UI is built on.
            return new GameObject(name, typeof(RectTransform), typeof(Canvas));
        }

        static List<WandButton> CollectAll()
        {
            var collected = new List<WandButton>();
            UIFocusGraph.Collect(collected, null, anyGroup: true);
            return collected;
        }

        [Test]
        public void ARegisteredChipIsReachable()
        {
            UIFocusGraph.Clear();

            var chip = MakeChip("chip", out var go);

            Assert.AreEqual(1, UIFocusGraph.Count);
            Assert.AreSame(chip, CollectAll()[0]);

            Object.DestroyImmediate(go);
            UIFocusGraph.Clear();
        }

        [Test]
        public void RegisteringTwice_DoesNotDuplicate()
        {
            UIFocusGraph.Clear();
            var chip = MakeChip("chip", out var go);

            UIFocusGraph.Register(chip);

            Assert.AreEqual(1, UIFocusGraph.Count, "a chip re-enabled must not appear twice in the walk");

            Object.DestroyImmediate(go);
            UIFocusGraph.Clear();
        }

        [Test]
        public void AHiddenPanelsChipsAreUnreachable()
        {
            UIFocusGraph.Clear();
            var visible = MakeChip("visible", out var visibleGo);
            MakeChip("hidden", out var hiddenGo);

            hiddenGo.SetActive(false);   // exactly what PanelModeSwitcher does to a mode's content

            var collected = CollectAll();

            Assert.AreEqual(1, collected.Count, "focus must not be able to walk onto a hidden panel");
            Assert.AreSame(visible, collected[0]);

            Object.DestroyImmediate(visibleGo);
            Object.DestroyImmediate(hiddenGo);
            UIFocusGraph.Clear();
        }

        [Test]
        public void ADisabledComponentIsUnreachableEvenWhileItsObjectIsActive()
        {
            UIFocusGraph.Clear();
            var chip = MakeChip("chip", out var go);

            chip.enabled = false;

            Assert.AreEqual(0, CollectAll().Count);

            Object.DestroyImmediate(go);
            UIFocusGraph.Clear();
        }

        [Test]
        public void ReEnablingAPanel_MakesItsChipsReachableAgain()
        {
            UIFocusGraph.Clear();
            var chip = MakeChip("chip", out var go);
            go.SetActive(false);
            Assert.AreEqual(0, CollectAll().Count);

            go.SetActive(true);

            var collected = CollectAll();
            Assert.AreEqual(1, collected.Count, "switching a panel mode back on restores its chips");
            Assert.AreSame(chip, collected[0]);

            Object.DestroyImmediate(go);
            UIFocusGraph.Clear();
        }

        [Test]
        public void Unregister_RemovesAChip()
        {
            UIFocusGraph.Clear();
            var chip = MakeChip("chip", out var go);

            UIFocusGraph.Unregister(chip);

            Assert.AreEqual(0, UIFocusGraph.Count);

            Object.DestroyImmediate(go);
            UIFocusGraph.Clear();
        }

        [Test]
        public void DestroyedChips_AreDroppedRatherThanStrandingFocus()
        {
            UIFocusGraph.Clear();
            var keeper = MakeChip("keeper", out var keeperGo);
            MakeChip("doomed", out var doomedGo);

            Object.DestroyImmediate(doomedGo);

            var collected = CollectAll();

            Assert.AreEqual(1, collected.Count, "a torn-down panel must not leave a null in the walk");
            Assert.AreSame(keeper, collected[0]);
            Assert.AreEqual(1, UIFocusGraph.Count, "and the dead entry is pruned, not merely skipped");

            Object.DestroyImmediate(keeperGo);
            UIFocusGraph.Clear();
        }

        [Test]
        public void CollectFiltersToOnePanel()
        {
            UIFocusGraph.Clear();
            var frontWall = MakePanel("FrontWall");
            var floor = MakePanel("Floor");
            MakeChip("front-a", out _, frontWall.transform);
            MakeChip("front-b", out _, frontWall.transform);
            MakeChip("floor-a", out _, floor.transform);

            var collected = new List<WandButton>();
            UIFocusGraph.Collect(collected, frontWall.transform);

            Assert.AreEqual(2, collected.Count,
                "a directional step stays on one panel — the front wall and the floor share no 'up'");

            Object.DestroyImmediate(frontWall);
            Object.DestroyImmediate(floor);
            UIFocusGraph.Clear();
        }

        [Test]
        public void CollectReturnsChipsInRegistrationOrder()
        {
            UIFocusGraph.Clear();
            var first = MakeChip("first", out var firstGo);
            var second = MakeChip("second", out var secondGo);
            var third = MakeChip("third", out var thirdGo);

            var collected = CollectAll();

            Assert.AreEqual(3, collected.Count);
            Assert.AreSame(first, collected[0], "order must be stable, or the same layout would walk " +
                "differently between runs");
            Assert.AreSame(second, collected[1]);
            Assert.AreSame(third, collected[2]);

            Object.DestroyImmediate(firstGo);
            Object.DestroyImmediate(secondGo);
            Object.DestroyImmediate(thirdGo);
            UIFocusGraph.Clear();
        }

        [Test]
        public void Groups_ListsEachPanelOnceInRegistrationOrder()
        {
            UIFocusGraph.Clear();
            var frontWall = MakePanel("FrontWall");
            var floor = MakePanel("Floor");
            MakeChip("front-a", out _, frontWall.transform);
            MakeChip("floor-a", out _, floor.transform);
            MakeChip("front-b", out _, frontWall.transform);

            var groups = new List<Transform>();
            UIFocusGraph.Groups(groups);

            Assert.AreEqual(2, groups.Count, "two panels, however many chips each has");
            Assert.AreSame(frontWall.transform, groups[0]);
            Assert.AreSame(floor.transform, groups[1]);

            Object.DestroyImmediate(frontWall);
            Object.DestroyImmediate(floor);
            UIFocusGraph.Clear();
        }

        [Test]
        public void Groups_SkipsPanelsWithNothingReachable()
        {
            UIFocusGraph.Clear();
            var frontWall = MakePanel("FrontWall");
            var floor = MakePanel("Floor");
            MakeChip("front-a", out _, frontWall.transform);
            MakeChip("floor-a", out var floorChipGo, floor.transform);

            floorChipGo.SetActive(false);

            var groups = new List<Transform>();
            UIFocusGraph.Groups(groups);

            Assert.AreEqual(1, groups.Count, "the bumpers must not cycle onto an empty panel");
            Assert.AreSame(frontWall.transform, groups[0]);

            Object.DestroyImmediate(frontWall);
            Object.DestroyImmediate(floor);
            UIFocusGraph.Clear();
        }

        [Test]
        public void FocusGroup_IsTheOwningCanvas()
        {
            UIFocusGraph.Clear();
            var panel = MakePanel("Panel");
            var chip = MakeChip("chip", out _, panel.transform);

            Assert.AreSame(panel.transform, chip.FocusGroup);

            Object.DestroyImmediate(panel);
            UIFocusGraph.Clear();
        }

        [Test]
        public void FocusGroup_ResolvesThroughAnInactivePanel()
        {
            UIFocusGraph.Clear();
            var panel = MakePanel("Panel");
            var chip = MakeChip("chip", out _, panel.transform);
            panel.SetActive(false);

            // Resolved for the first time while hidden: a chip on a hidden panel still belongs to it, and
            // caching null here would put it in the wrong group for the rest of the session.
            Assert.AreSame(panel.transform, chip.FocusGroup);

            Object.DestroyImmediate(panel);
            UIFocusGraph.Clear();
        }

        [Test]
        public void AChipWithNoCanvas_HasNoGroup()
        {
            UIFocusGraph.Clear();
            var chip = MakeChip("loose", out var go);

            Assert.IsNull(chip.FocusGroup);

            Object.DestroyImmediate(go);
            UIFocusGraph.Clear();
        }

        [Test]
        public void CollectIntoANullList_IsHarmless()
        {
            UIFocusGraph.Clear();

            Assert.DoesNotThrow(() => UIFocusGraph.Collect(null, null, anyGroup: true));
            Assert.DoesNotThrow(() => UIFocusGraph.Groups(null));
        }
    }
}
