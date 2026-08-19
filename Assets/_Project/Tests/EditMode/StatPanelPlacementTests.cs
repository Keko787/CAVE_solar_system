using System.Collections.Generic;
using NUnit.Framework;
using Stadium3D.EditorTools;
using Stadium3D.Pitch;
using UnityEngine;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// Where the built readouts actually sit, against §8's readable band.
    /// </summary>
    /// <remarks>
    /// The whole point of §8 is that most of the front wall cannot be read at a glance, and the whole
    /// problem with checking it by eye is that a 2560-pixel capture on a desk monitor makes every part of
    /// that wall look equally legible. So it is asserted here instead: the three continuous readouts inside
    /// the central 36%, and the two reference ones outside it on purpose.
    /// </remarks>
    public class StatPanelPlacementTests
    {
        readonly List<GameObject> m_Spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in m_Spawned)
                if (go != null) Object.DestroyImmediate(go);

            m_Spawned.Clear();
        }

        T Camera<T>(string name) where T : PitchCameraBase
        {
            var go = new GameObject(name);
            m_Spawned.Add(go);
            go.AddComponent<Camera>();
            return go.AddComponent<T>();
        }

        static StatHud.Element Find(StatHud hud, string label)
        {
            foreach (var element in hud.Elements)
                if (element.Label != null && element.Label.name == label) return element;

            Assert.Fail($"the readout has no label called \"{label}\"");
            return default;
        }

        [Test]
        public void TheFrontWallKeepsItsContinuousReadoutsInsideTheReadableBand()
        {
            var front = Camera<BroadcastPitchCamera>("BroadcastCamera");
            StatPanelBuilder.Build(front, null, null);

            var hud = front.GetComponentInChildren<BroadcastStatHud>(true);
            Assert.That(hud, Is.Not.Null);

            foreach (string label in new[] { "DownDistance", "BallOn", "Clock" })
            {
                var element = Find(hud, label);
                Assert.That(HudLayout.IsReadable(element.Viewport.x), Is.True,
                    $"{label} is what a viewer reads continuously, so it cannot sit in the 64% of the " +
                    "wall that is peripheral");
            }
        }

        [Test]
        public void TheFrontWallPutsScoreAndQuarterOutInThePeriphery()
        {
            var front = Camera<BroadcastPitchCamera>("BroadcastCamera");
            StatPanelBuilder.Build(front, null, null);

            var hud = front.GetComponentInChildren<BroadcastStatHud>(true);

            Assert.That(HudLayout.IsReadable(Find(hud, "Score").Viewport.x), Is.False);
            Assert.That(HudLayout.IsReadable(Find(hud, "Quarter").Viewport.x), Is.False);
        }

        [Test]
        public void DownAndDistanceIsTheLargestThingOnTheFrontWall()
        {
            var front = Camera<BroadcastPitchCamera>("BroadcastCamera");
            StatPanelBuilder.Build(front, null, null);

            var hud = front.GetComponentInChildren<BroadcastStatHud>(true);
            float primary = Find(hud, "DownDistance").Height;

            foreach (var element in hud.Elements)
            {
                if (element.Label.name == "DownDistance") continue;

                Assert.That(element.Height, Is.LessThan(primary),
                    $"{element.Label.name} competing with down and distance for size defeats the point of " +
                    "having a central band at all");
            }
        }

        [Test]
        public void EachReadoutIsOnALayerOnlyItsOwnSurfaceRenders()
        {
            var front = Camera<BroadcastPitchCamera>("BroadcastCamera");
            var right = Camera<TeamIsoPitchCamera>("HomeIsoCamera");
            var left = Camera<TeamIsoPitchCamera>("AwayIsoCamera");

            StatPanelBuilder.Build(front, right, left);

            int hud = LayerMask.NameToLayer(PitchLayers.Hud);
            int rightWall = LayerMask.NameToLayer(PitchLayers.RightWall);
            int leftWall = LayerMask.NameToLayer(PitchLayers.LeftWall);

            Assert.That(hud, Is.GreaterThanOrEqualTo(0), "PitchHud has to exist in Tags and Layers");

            Assert.That(front.GetComponentInChildren<BroadcastStatHud>(true).gameObject.layer, Is.EqualTo(hud));
            Assert.That(right.GetComponentInChildren<TeamWallStatHud>(true).gameObject.layer, Is.EqualTo(rightWall));
            Assert.That(left.GetComponentInChildren<TeamWallStatHud>(true).gameObject.layer, Is.EqualTo(leftWall));

            // The front wall's readout has to be visible to the front wall and to nobody else — the failure
            // it guards against is text lying across the middle of the floor's top-down view.
            Assert.That(PitchLayers.BroadcastMask & (1 << hud), Is.Not.Zero);
            Assert.That(PitchLayers.FloorMask & (1 << hud), Is.Zero);
            Assert.That(PitchLayers.RightWallMask & (1 << hud), Is.Zero);
            Assert.That(PitchLayers.LeftWallMask & (1 << hud), Is.Zero);

            // And it must be inside the set PitchIsolation strips, or it draws in the room itself.
            Assert.That(PitchLayers.PitchMask & (1 << hud), Is.Not.Zero);
        }

        /// <summary>
        /// The one that catches a label that is present, correct, and off the side of the wall.
        /// </summary>
        /// <remarks>
        /// TextMeshPro aligns inside its RectTransform, and a new one is 20 units wide. At the font sizes a
        /// 2.4 m wall needs, an anchor with Left or Right alignment then lands half a rect outside the
        /// frame — the label exists, holds the right string, reports a sensible position, and never appears.
        /// A degenerate rect makes the anchor the alignment edge, so every declared fraction means what it
        /// says whichever way the text is aligned.
        /// </remarks>
        [Test]
        public void EveryLabelAnchorsOnItsOwnPositionRatherThanOnARectAroundIt()
        {
            var front = Camera<BroadcastPitchCamera>("BroadcastCamera");
            var right = Camera<TeamIsoPitchCamera>("HomeIsoCamera");
            StatPanelBuilder.Build(front, right, null);

            foreach (var hud in new StatHud[]
            {
                front.GetComponentInChildren<BroadcastStatHud>(true),
                right.GetComponentInChildren<TeamWallStatHud>(true),
            })
            {
                foreach (var element in hud.Elements)
                {
                    Assert.That(element.Label.rectTransform.sizeDelta, Is.EqualTo(Vector2.zero),
                        $"{element.Label.name} would be aligned against a rect instead of against the " +
                        "position it was given");
                }
            }
        }

        /// <summary>
        /// Hiding the readout has to actually stop it drawing, and has to be reversible.
        /// </summary>
        /// <remarks>
        /// Renderers rather than the GameObject, because deactivating the object would stop the component
        /// that has to bring it back — a toggle that only works in one direction.
        /// </remarks>
        [Test]
        public void HidingTheReadoutStopsEveryLabelDrawingAndCanBeUndone()
        {
            var front = Camera<BroadcastPitchCamera>("BroadcastCamera");
            StatPanelBuilder.Build(front, null, null);

            var hud = front.GetComponentInChildren<BroadcastStatHud>(true);
            Assert.That(hud.Visible, Is.True, "the readout starts on");

            hud.SetVisible(false);
            Assert.That(hud.Visible, Is.False);
            foreach (var element in hud.Elements)
                Assert.That(element.Label.GetComponent<Renderer>().enabled, Is.False, element.Label.name);

            hud.SetVisible(true);
            Assert.That(hud.Visible, Is.True);
            foreach (var element in hud.Elements)
                Assert.That(element.Label.GetComponent<Renderer>().enabled, Is.True, element.Label.name);
        }

        [Test]
        public void ATeamWallsLabelsAreStampedAllTheWayDown()
        {
            var right = Camera<TeamIsoPitchCamera>("HomeIsoCamera");
            StatPanelBuilder.Build(null, right, null);

            int rightWall = LayerMask.NameToLayer(PitchLayers.RightWall);
            var hud = right.GetComponentInChildren<TeamWallStatHud>(true);

            foreach (var element in hud.Elements)
            {
                Assert.That(element.Label.gameObject.layer, Is.EqualTo(rightWall),
                    $"{element.Label.name} left on layer 0 would draw on every surface at once");
            }
        }
    }
}
