using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Stadium3D.CaveBox;
using Stadium3D.Football;
using Stadium3D.Pitch;

namespace Stadium3D.EditorTools
{
    /// <summary>
    /// Builds the CAVE scene: the room, the pitch 1500 m below it, four cameras out at the field, and four
    /// panels on the walls showing what they see.
    /// </summary>
    /// <remarks>
    /// <b>One scene, not two.</b> The four pitch cameras and the eight MiddleVR display cameras all render
    /// the same scene, so the room and the field have to coexist in it — the same shape the source project
    /// used, where a 40 m sim sandbox sat beside the room and was culled from the display cameras. A football
    /// field needs rather more clearance than 40 m, and putting it <em>below</em> rather than beside buys a
    /// second guarantee for free: every display camera has <c>Far="1000"</c> from the <c>.vrx</c>, so at
    /// −1500 m the pitch is outside the far plane as well as outside the culling mask.
    ///
    /// <para><b>One thing this builder cannot verify.</b> The side-wall panel poses come from
    /// <c>CaveRegionMap</c>, which is authored, not measured — and the <c>.vrx</c>'s own side-screen entries
    /// are self-inconsistent (a rotation about Z cannot produce a side wall, and the y-centre −0.08255 is
    /// exactly <c>1.11125 − 2.3876/2</c>, i.e. derived from the screen's width rather than its height). Until
    /// a test card is walked in the room, treat the side regions below as a starting point that will need
    /// nudging, and expect the front and floor to be right.</para>
    /// </remarks>
    public static class StadiumCaveBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/CAVE/StadiumCave.unity";
        const string RegionMapPath = "Assets/_Project/Config/CaveRegionMap.asset";

        /// <summary>
        /// How far below the room the pitch sits. Beyond every display camera's 1000 m far plane, so the
        /// field is hidden twice over — by the culling mask and by distance.
        /// </summary>
        public static readonly Vector3 PitchOrigin = new Vector3(0f, -1500f, 0f);

        [MenuItem("Stadium3D/Build/CAVE Football Scene", priority = 20)]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            EnsureFootballRegions();

            var room = InstantiatePrefab("Assets/_Project/Prefabs/Environment/CAVE_Room.prefab", "CAVE_Room");
            InstantiatePrefab("Assets/_Project/Prefabs/Rig/MVRManager Variant.prefab", "MVRManager");
            InstantiatePrefab("Assets/_Project/Prefabs/Rig/MVR_URP Variant.prefab", "MVR_URP");

            // The pitch, well below the room.
            PitchRigBuilder.BuildRig();
            var world = GameObject.Find("PitchWorld");
            if (world != null) world.transform.position = PitchOrigin;

            var key = new GameObject("PitchKeyLight");
            var light = key.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.97f, 0.91f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.None;      // main-light shadows are off in the URP asset anyway
            key.transform.rotation = Quaternion.Euler(52f, 145f, 0f);

            // Ambient is per-SCENE, not per-object, and this scene had none — so with a single directional
            // key and additional lights disabled at the shipping quality level, every surface facing away
            // from that key rendered black. On the team walls that meant a life-size player was a silhouette,
            // which is a striking way to lose the one image the room exists to produce. A flat ambient term
            // is the cheapest fill available and costs nothing per frame.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.46f, 0.52f);
            RenderSettings.ambientEquatorColor = new Color(0.34f, 0.36f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.24f, 0.20f);
            RenderSettings.fog = false;

            var cameras = BuildCameras(world != null ? world.transform : null);
            BuildPanels(room != null ? room.transform : null, cameras);

            var isolation = new GameObject("PitchIsolation");
            isolation.AddComponent<PitchIsolation>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[Stadium3D] Built {ScenePath}: room + pitch at {PitchOrigin}, four cameras, four " +
                      "panels. The side-wall regions are unverified until a test card is walked in the room.");
        }

        // ── regions ────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Add the four football regions to the shared region map, non-destructively.
        /// </summary>
        /// <remarks>
        /// Near full-bleed rather than the inherited half-wall regions: the Sit-Rep map claimed 3.0 x 1.2 of
        /// a 3.556 x 2.2225 front surface, which is a lot of unlit wall for a demo whose premise is a
        /// wraparound stadium. The small inset that remains is deliberate — a panel sized exactly to the
        /// surface would show a seam of its own edge on any calibration error.
        /// </remarks>
        public static void EnsureFootballRegions()
        {
            var map = AssetDatabase.LoadAssetAtPath<CaveRegionMap>(RegionMapPath);
            if (map == null)
            {
                Debug.LogError($"[Stadium3D] No region map at {RegionMapPath}.");
                return;
            }

            int added = 0;
            if (map.EnsureRegion("FootballFront", new Vector3(0f, 1.111f, 1.10f), Vector3.zero,
                                 new Vector2(3.50f, 2.19f))) added++;
            if (map.EnsureRegion("FootballLeft", new Vector3(-1.75f, 1.111f, 0f), new Vector3(0f, -90f, 0f),
                                 new Vector2(2.35f, 2.19f))) added++;
            if (map.EnsureRegion("FootballRight", new Vector3(1.75f, 1.111f, 0f), new Vector3(0f, 90f, 0f),
                                 new Vector2(2.35f, 2.19f))) added++;
            if (map.EnsureRegion("FootballFloor", new Vector3(0f, 0.02f, 0f), new Vector3(90f, 0f, 0f),
                                 new Vector2(3.50f, 2.19f))) added++;

            if (added > 0)
            {
                EditorUtility.SetDirty(map);
                AssetDatabase.SaveAssets();
            }
            Debug.Log($"[Stadium3D] Region map: {added} football regions added ({map.Regions.Count} total).");
        }

        // ── cameras and panels ─────────────────────────────────────────────────────────────────────

        struct WallRig
        {
            public PitchCameraBase Front, Left, Right, Floor;
        }

        static WallRig BuildCameras(Transform pitchWorld)
        {
            var root = new GameObject("PitchCameras");
            if (pitchWorld != null) root.transform.SetParent(pitchWorld, false);

            return new WallRig
            {
                Front = Cam<BroadcastPitchCamera>(root.transform, "BroadcastCamera"),
                Right = Cam<TeamIsoPitchCamera>(root.transform, "HomeIsoCamera"),
                Left  = Cam<TeamIsoPitchCamera>(root.transform, "AwayIsoCamera"),
                Floor = Cam<FloorTopDownCamera>(root.transform, "FloorTopDownCamera"),
            };
        }

        static T Cam<T>(Transform parent, string name) where T : PitchCameraBase
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var cam = go.AddComponent<Camera>();
            cam.enabled = true;
            return go.AddComponent<T>();
        }

        static void BuildPanels(Transform regionRoot, WallRig rig)
        {
            var map = AssetDatabase.LoadAssetAtPath<CaveRegionMap>(RegionMapPath);
            if (map == null) return;

            var root = new GameObject("WallPanels");
            if (regionRoot != null) root.transform.SetParent(regionRoot, false);

            Panel(root.transform, map, regionRoot, "FootballFront", rig.Front, new Vector2(3.50f, 2.19f));
            Panel(root.transform, map, regionRoot, "FootballRight", rig.Right, new Vector2(2.35f, 2.19f));
            Panel(root.transform, map, regionRoot, "FootballLeft",  rig.Left,  new Vector2(2.35f, 2.19f));
            Panel(root.transform, map, regionRoot, "FootballFloor", rig.Floor, new Vector2(3.50f, 2.19f));

            PitchLayers.ApplyRecursively(root, PitchLayers.CaveScreen);
        }

        static void Panel(Transform parent, CaveRegionMap map, Transform regionRoot,
                          string regionName, PitchCameraBase source, Vector2 surface)
        {
            if (!RegionPlacement.TryWorldSlot(map, regionName, regionRoot, 0, 1, out var pose))
            {
                Debug.LogWarning($"[Stadium3D] Region \"{regionName}\" is missing from the map.");
                return;
            }

            var go = new GameObject(regionName + "Panel");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pose.position, pose.rotation);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Screen";
            quad.transform.SetParent(go.transform, false);
            // A region orients +Z INTO the surface so the readable face points back at the operator; a Unity
            // quad shows its +Z face, so it turns to meet them. If a panel ever renders mirrored on hardware,
            // this half-turn is the first thing to check.
            quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Object.DestroyImmediate(quad.GetComponent<Collider>());

            // The wall-pick collider the wand raycasts in M6. On the panel, not on CAVE_Room, so wall
            // selection never has to share a layer with the room's own geometry.
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(surface.x, surface.y, 0.04f);

            var panel = go.AddComponent<WallPanel>();
            panel.Configure(source, quad.GetComponent<Renderer>(), surface);
        }

        static GameObject InstantiatePrefab(string path, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[Stadium3D] Missing prefab at {path}.");
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            return go;
        }
    }
}
