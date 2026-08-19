using NUnit.Framework;
using Stadium3D.Football;
using Stadium3D.Pitch;
using UnityEngine;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// Per-wall focus, pinned to the wall that asked for it.
    /// </summary>
    /// <remarks>
    /// The two team walls follow different players at the same time, so "is this player focused" has two
    /// answers at once and each belongs to one wall. The failure this guards is quiet: a ring that leaks
    /// to the other wall highlights a player that wall is not watching, which looks like a deliberate
    /// call-out rather than a bug — and once the wand drives focus in M6, it would look like the wand had
    /// moved the wrong wall.
    /// </remarks>
    public class PlayerMarkerFocusTests
    {
        GameObject m_Root;
        PlayerMarker m_Marker;
        Renderer m_Right, m_Left, m_Shared;

        [SetUp]
        public void SetUp()
        {
            m_Root = new GameObject("#12");

            m_Shared = Ring("FocusRing");
            m_Right = Ring("FocusRingRight");
            m_Left = Ring("FocusRingLeft");

            m_Marker = m_Root.AddComponent<PlayerMarker>();
            m_Marker.Configure(null, null, null, m_Shared, null);
            m_Marker.ConfigureWallRings(m_Right, m_Left);
            m_Marker.Bind(new PlayerIdentity { Number = 12, Side = TeamSide.Home });
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Root != null) Object.DestroyImmediate(m_Root);
        }

        Renderer Ring(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(m_Root.transform, false);
            Object.DestroyImmediate(go.GetComponent<Collider>());

            var r = go.GetComponent<Renderer>();
            r.enabled = false;
            return r;
        }

        [Test]
        public void OneWallsFocus_DoesNotLightTheOtherWallsRing()
        {
            m_Marker.SetWallFocus(TeamSide.Home, true);

            Assert.That(m_Right.enabled, Is.True, "the home wall asked, so the home wall's ring lights");
            Assert.That(m_Left.enabled, Is.False,
                "the away wall is following someone else and must not be told this player is theirs");
        }

        [Test]
        public void EachWallClearsOnlyItsOwn()
        {
            m_Marker.SetWallFocus(TeamSide.Home, true);
            m_Marker.SetWallFocus(TeamSide.Away, true);

            m_Marker.SetWallFocus(TeamSide.Home, false);

            Assert.That(m_Right.enabled, Is.False);
            Assert.That(m_Left.enabled, Is.True, "clearing one wall must not drop the other wall's ring");
        }

        [Test]
        public void WallFocus_IsIndependentOfEmphasis()
        {
            // Emphasis is what everyone thinks of this player; wall focus is what one viewer is watching.
            // Folding them together is what makes "focused" mean two things and lets one silently win.
            m_Marker.SetWallFocus(TeamSide.Home, true);
            m_Marker.SetEmphasis(MarkerEmphasis.Ghosted);

            Assert.That(m_Right.enabled, Is.True, "a wall's own ring is not the broadcast wall's business");
            Assert.That(m_Shared.enabled, Is.False, "and the shared ring still answers to emphasis alone");
        }
    }
}
