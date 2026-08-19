using TMPro;
using UnityEngine;
using Stadium3D.Football;
using Stadium3D.Pitch;

namespace Stadium3D.EditorTools
{
    /// <summary>
    /// Builds the three stat readouts — front wall, and one card per team wall.
    /// </summary>
    /// <remarks>
    /// <b>Everything here is a fraction of a surface.</b> The four surfaces are three pixel sizes and two
    /// aspect ratios, so a layout in metres or pixels is a layout that is right on one wall. §4.3 records
    /// what an implicit ratio costs: the source project's feed window put a 1.333 image in a 2.256 rect and
    /// shipped a 1.69× stretch that looked almost right.
    ///
    /// <para><b>The front wall's middle is not a design preference.</b> Only the central 1.28 m of 3.556 m is
    /// read at a glance, so the three things a viewer needs continuously go there and nothing else does.
    /// Score and quarter sit out at 0.13 and 0.87 — findable on purpose, never competing.</para>
    ///
    /// <para><b>The team walls use a lower third</b>, which is where forty years of broadcast has trained
    /// people to look for a name, and which keeps the card off the players: at framing 2.5 the bodies occupy
    /// the middle band of the frame, and the live telemetry goes above them rather than across them.</para>
    /// </remarks>
    public static class StatPanelBuilder
    {
        /// <summary>Drop shadow, per §3.4 — separation by contrast, not by depth.</summary>
        const string ShadowMaterial = "Fonts & Materials/LiberationSans SDF - Drop Shadow";

        public static void Build(PitchCameraBase front, PitchCameraBase right, PitchCameraBase left)
        {
            if (front != null) BuildBroadcast(front);
            if (right != null) BuildTeamWall(right, TeamSide.Home, PitchLayers.RightWall);
            if (left != null) BuildTeamWall(left, TeamSide.Away, PitchLayers.LeftWall);
        }

        static void BuildBroadcast(PitchCameraBase camera)
        {
            var root = Root(camera, "StatHud", PitchLayers.Hud);
            var hud = root.AddComponent<BroadcastStatHud>();

            // Central band — x is 0.5 for all three, so they cannot drift out of the readable 36%.
            var downDistance = Label(root.transform, "DownDistance", TextAlignmentOptions.Center);
            var ballOn = Label(root.transform, "BallOn", TextAlignmentOptions.Center);
            var clock = Label(root.transform, "Clock", TextAlignmentOptions.Center);

            // Periphery.
            var score = Label(root.transform, "Score", TextAlignmentOptions.Left);
            var quarter = Label(root.transform, "Quarter", TextAlignmentOptions.Right);

            hud.Place(downDistance, new Vector2(0.5f, 0.905f), 0.070f);
            hud.Place(ballOn, new Vector2(0.5f, 0.848f), 0.032f);
            hud.Place(clock, new Vector2(0.5f, 0.806f), 0.032f);
            hud.Place(score, new Vector2(0.13f, 0.925f), 0.028f);
            hud.Place(quarter, new Vector2(0.87f, 0.925f), 0.028f);

            hud.Configure(downDistance, ballOn, clock, score, quarter);
            Stamp(root, PitchLayers.Hud);
        }

        static void BuildTeamWall(PitchCameraBase camera, TeamSide side, string layer)
        {
            var root = Root(camera, "StatHud", layer);
            var hud = root.AddComponent<TeamWallStatHud>();

            var name = Label(root.transform, "Name", TextAlignmentOptions.Center);
            var vitals = Label(root.transform, "Vitals", TextAlignmentOptions.Center);
            var stats = Label(root.transform, "Stats", TextAlignmentOptions.Center);
            var live = Label(root.transform, "Live", TextAlignmentOptions.Center);
            var route = Label(root.transform, "Route", TextAlignmentOptions.Center);

            // Lower third for the card, and one line at the top for what he is doing this instant.
            //
            // The route started up there beside the telemetry, where it reads as live play information —
            // and landed straight on top of a floating jersey number, because M4 parks those above the
            // players' heads and lifts them further to separate them by depth. The top strip belongs to
            // the numbers; the route reads perfectly well as the last line of the card.
            hud.Place(name, new Vector2(0.5f, 0.160f), 0.044f);
            hud.Place(vitals, new Vector2(0.5f, 0.120f), 0.024f);
            hud.Place(stats, new Vector2(0.5f, 0.084f), 0.024f);
            hud.Place(route, new Vector2(0.5f, 0.048f), 0.022f);
            hud.Place(live, new Vector2(0.5f, 0.930f), 0.026f);

            hud.Configure(side, name, vitals, stats, live, route);
            Stamp(root, layer);
        }

        static GameObject Root(Component camera, string name, string layer)
        {
            var existing = camera.transform.Find(name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(name);
            go.transform.SetParent(camera.transform, false);
            go.layer = LayerMask.NameToLayer(layer);
            return go;
        }

        /// <summary>
        /// One label. Size and position are declared by the caller and applied by the HUD against the
        /// camera's real frustum, so nothing here measures anything.
        /// </summary>
        static TMP_Text Label(Transform parent, string name, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(TextMeshPro));
            go.transform.SetParent(parent, false);

            var label = go.GetComponent<TextMeshPro>();
            label.text = string.Empty;
            label.alignment = alignment;
            label.enableWordWrapping = false;

            // A degenerate rect, so the anchor *is* the alignment edge. TextMeshPro aligns within its
            // RectTransform, and a fresh one is 20 units wide — so at the sizes a wall needs, Left and
            // Right alignment put the text half a rect off the side of the frame and it simply is not
            // there. Both centred stacks were unaffected, which is exactly why the failure showed up as
            // two missing labels rather than as a layout that looked wrong.
            label.rectTransform.sizeDelta = Vector2.zero;
            label.color = Color.white;
            label.fontSize = 10f;

            // Text on a wall is read across a lit room at four metres. A little tracking and a shadow do
            // more for that than any size increase, and §3.4 rules out the usual alternative of pushing it
            // forward in depth.
            label.characterSpacing = 4f;

            var shadow = Resources.Load<Material>(ShadowMaterial);
            if (shadow != null) label.fontSharedMaterial = shadow;

            var renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return label;
        }

        static void Stamp(GameObject root, string layer)
        {
            int index = LayerMask.NameToLayer(layer);
            if (index < 0)
            {
                Debug.LogError($"[Stadium3D] No layer called \"{layer}\" — the stat readout would render on " +
                               "every surface that carries layer 0. Add it in Project Settings ▸ Tags and Layers.");
                return;
            }

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = index;
        }
    }
}
