using NUnit.Framework;
using UnityEngine;
using Stadium3D.Pitch;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// What each CAVE surface is allowed to see.
    /// </summary>
    /// <remarks>
    /// These are worth pinning because a culling mask fails <em>silently</em> and in the most expensive way
    /// available: the first time the four walls were rendered, both team walls came out pure black, because
    /// their masks named the per-player dressing layers that M4 will populate while every marker actually
    /// lived on the shared player layer. Nothing threw, no test failed, and the only symptom was two blank
    /// surfaces. A mask is a claim about content, so it deserves an assertion.
    ///
    /// <para>The other half of the job is exclusion. Jersey numbers on the floor render as white slabs seen
    /// from directly overhead, obscuring the bodies they label; on the broadcast wall eleven of them inside
    /// eight yards collapse into a pile. Both were visible immediately in a still, and both are prevented
    /// here rather than by remembering.</para>
    /// </remarks>
    public class PitchLayersTests
    {
        static bool Has(int mask, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            Assert.That(layer, Is.GreaterThanOrEqualTo(0), $"layer \"{layerName}\" is not defined in Tags and Layers");
            return (mask & (1 << layer)) != 0;
        }

        [Test]
        public void EveryPitchLayer_IsDefinedInTagManager()
        {
            // A missing layer degrades to "that content is invisible", which is exactly the failure mode the
            // blank team walls demonstrated. Better to fail here than to render nothing in the room.
            foreach (var name in new[]
                     {
                         PitchLayers.Shared, PitchLayers.Players, PitchLayers.RightWall, PitchLayers.LeftWall,
                         PitchLayers.Tactical, PitchLayers.Stadium, PitchLayers.CaveScreen,
                         PitchLayers.Numbers, PitchLayers.Miniature,
                     })
                Assert.That(LayerMask.NameToLayer(name), Is.GreaterThanOrEqualTo(0), name);
        }

        [Test]
        public void EveryWallCamera_SeesTheFieldAndSomeBodies()
        {
            // The assertion that would have caught the black team walls.
            foreach (var (label, mask) in new[]
                     {
                         ("broadcast", PitchLayers.BroadcastMask),
                         ("right wall", PitchLayers.RightWallMask),
                         ("left wall", PitchLayers.LeftWallMask),
                         ("floor", PitchLayers.FloorMask),
                     })
            {
                Assert.That(Has(mask, PitchLayers.Shared), Is.True, $"{label} must see the field");
                Assert.That(Has(mask, PitchLayers.Players), Is.True, $"{label} must see players");
            }
        }

        [Test]
        public void JerseyNumbers_AppearOnlyOnTheTeamWalls()
        {
            Assert.That(Has(PitchLayers.RightWallMask, PitchLayers.Numbers), Is.True);
            Assert.That(Has(PitchLayers.LeftWallMask, PitchLayers.Numbers), Is.True);

            Assert.That(Has(PitchLayers.BroadcastMask, PitchLayers.Numbers), Is.False,
                "eleven numbers inside eight yards of scrimmage are an unreadable pile on the front wall");
            Assert.That(Has(PitchLayers.FloorMask, PitchLayers.Numbers), Is.False,
                "a vertical billboard seen from overhead is a white slab covering the body it labels");
        }

        [Test]
        public void TacticalOverlays_AppearOnlyOnTheFloor()
        {
            Assert.That(Has(PitchLayers.FloorMask, PitchLayers.Tactical), Is.True);
            Assert.That(Has(PitchLayers.BroadcastMask, PitchLayers.Tactical), Is.False,
                "routes and coverage shading must never appear in a broadcast shot");
            Assert.That(Has(PitchLayers.RightWallMask, PitchLayers.Tactical), Is.False);
            Assert.That(Has(PitchLayers.LeftWallMask, PitchLayers.Tactical), Is.False);
        }

        [Test]
        public void EachTeamWall_SeesOnlyItsOwnPerPlayerDressing()
        {
            Assert.That(Has(PitchLayers.RightWallMask, PitchLayers.RightWall), Is.True);
            Assert.That(Has(PitchLayers.RightWallMask, PitchLayers.LeftWall), Is.False,
                "the home wall must not pick up the away wall's dressing");

            Assert.That(Has(PitchLayers.LeftWallMask, PitchLayers.LeftWall), Is.True);
            Assert.That(Has(PitchLayers.LeftWallMask, PitchLayers.RightWall), Is.False);
        }

        [Test]
        public void NoWallCamera_SeesAnyWallPanel()
        {
            // The one that would be unmissable and unexplainable: a pitch camera that can see the panels
            // shows every wall a picture-in-picture of its neighbours, and its own panel becomes a feedback
            // loop. Structural, not remembered.
            foreach (var (label, mask) in new[]
                     {
                         ("broadcast", PitchLayers.BroadcastMask),
                         ("right wall", PitchLayers.RightWallMask),
                         ("left wall", PitchLayers.LeftWallMask),
                         ("floor", PitchLayers.FloorMask),
                     })
                Assert.That(Has(mask, PitchLayers.CaveScreen), Is.False, $"{label} must not see the wall panels");
        }

        [Test]
        public void PitchMask_CoversEveryLayerTheDisplayCamerasMustStrip()
        {
            // What PitchIsolation removes from the MiddleVR display cameras. Anything pitch-side missing
            // here renders 110 m of stadium inside a 3.5 m room.
            int mask = PitchLayers.PitchMask;

            foreach (var name in new[]
                     {
                         PitchLayers.Shared, PitchLayers.Players, PitchLayers.RightWall,
                         PitchLayers.LeftWall, PitchLayers.Tactical, PitchLayers.Stadium, PitchLayers.Numbers,
                     })
                Assert.That(Has(mask, name), Is.True, $"{name} must be stripped from the display cameras");

            Assert.That(Has(mask, PitchLayers.CaveScreen), Is.False,
                "the panels are the one thing the display cameras exist to show");
            Assert.That(Has(mask, PitchLayers.Miniature), Is.False,
                "M8's miniature is real geometry in the room and must survive the strip");
        }
    }
}
