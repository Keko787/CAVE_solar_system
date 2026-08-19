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

        /// <summary>
        /// Whether the M3.5 figure goes into the built scene. <b>Off.</b>
        /// </summary>
        /// <remarks>
        /// M3.5 was a go/no-go probe, not content: one life-size figure of real geometry, in correct
        /// stereo, to answer "why a CAVE rather than four televisions" before any effort went into stats
        /// or overlays. It answered it. The placeholder itself is not what ships — M8 puts real geometry
        /// in the room properly, as the miniature field.
        ///
        /// <para>A switch rather than a deletion, because the component and its tests are the groundwork
        /// M8 stands on, and because a known life-size reference standing in the room is genuinely useful
        /// when checking the side walls. Build it deliberately from
        /// <c>Stadium3D ▸ Build ▸ Life-Size Room Figure (M3.5)</c>.</para>
        ///
        /// <para>Deliberately <c>static readonly</c> rather than <c>const</c>: a <c>const false</c> makes
        /// the guarded block unreachable and the compiler says so, which is noise rather than news.</para>
        /// </remarks>
        static readonly bool IncludeRoomFigure = false;

        [MenuItem("Stadium3D/Build/CAVE Football Scene", priority = 20)]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            EnsureFootballRegions();

            var room = InstantiatePrefab("Assets/_Project/Prefabs/Environment/CAVE_Room.prefab", "CAVE_Room");
            var mvr = InstantiatePrefab("Assets/_Project/Prefabs/Rig/MVRManager Variant.prefab", "MVRManager");
            InstantiatePrefab("Assets/_Project/Prefabs/Rig/MVR_URP Variant.prefab", "MVR_URP");

            // Before the pitch cameras exist, so there is never a moment when MiddleVR could pick one.
            BuildTemplateCamera(mvr);

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

            // The one stereoscopic thing in the room, and off by default — see IncludeRoomFigure.
            // Everything on the four walls is a flat picture until M8, which is a known and accepted
            // property of the design rather than something this placeholder was ever going to fix.
            if (IncludeRoomFigure)
            {
                var hologram = RoomHologramBuilder.Build(room != null ? room.transform : null);
                RoomHologramBuilder.BuildScaleRod(room != null ? room.transform : null,
                                                  hologram.GetComponent<RoomHologram>().Anchor);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[Stadium3D] Built {ScenePath}: room + pitch at {PitchOrigin}, four cameras and " +
                      $"four panels. Room figure: {(IncludeRoomFigure ? "included" : "off — M3.5 was a probe")}.\n" +
                      "  The panels show a dim slate until you PRESS PLAY — a camera's render target is " +
                      "created in Awake, which does not run on a scene the editor has merely loaded.\n" +
                      "  The side-wall regions remain unverified until a test card is walked in the room.");
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

        // ── the camera MiddleVR clones ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Give MiddleVR a bare camera of its own to duplicate for its eight display cameras.
        /// </summary>
        /// <remarks>
        /// <c>MVRManagerScript</c> builds every display camera by calling <c>Object.Instantiate</c> on a
        /// template GameObject — <b>components and all</b> — and when no template is assigned it takes
        /// <c>Camera.allCameras[0]</c>. Once the four pitch rigs became the only cameras in the scene, that
        /// fallback picked one of them, and all eight display cameras came back as clones of a
        /// <see cref="PitchCameraBase"/>. Each then did three separate things that draw nothing: allocated
        /// its own 2560x1600 render target, pointed itself at that instead of at the wall, and carried a
        /// pitch culling mask which <see cref="PitchIsolation"/> stripped to exactly zero. One clone, an
        /// entirely black room, and not one error anywhere.
        ///
        /// <para>The template is deliberately plain. MiddleVR copies the camera settings as well as the
        /// components, and a display camera needs the default everything-mask to see the room at all. The
        /// very low depth is belt and braces: <c>Camera.allCameras</c> is sorted by depth, so even if the
        /// assignment below is ever lost, the fallback path picks this camera rather than a pitch rig.</para>
        /// </remarks>
        static void BuildTemplateCamera(GameObject mvrManager)
        {
            var go = new GameObject("MVRTemplateCamera");
            var cam = go.AddComponent<Camera>();
            cam.depth = -1000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            if (mvrManager == null) return;

            // Assigned through the serialized property rather than the typed field: this builder has no
            // compile-time reference to MiddleVR and should not grow one just to fill in a GameObject slot.
            var script = mvrManager.GetComponent("MVRManagerScript");
            if (script == null)
            {
                Debug.LogWarning("[Stadium3D] No MVRManagerScript on the MVRManager instance, so no template " +
                                 "camera could be assigned. MiddleVR will fall back to cloning " +
                                 "Camera.allCameras[0] — which is why MVRTemplateCamera sits at depth -1000.");
                return;
            }

            var so = new SerializedObject(script);
            var prop = so.FindProperty("advancedProperties.TemplateCamera");
            if (prop == null)
            {
                Debug.LogWarning("[Stadium3D] MVRManagerScript has no advancedProperties.TemplateCamera — the " +
                                 "MiddleVR template-camera API has moved. Falling back to camera depth.");
                return;
            }

            prop.objectReferenceValue = go;
            so.ApplyModifiedPropertiesWithoutUndo();
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

            // The side is not a label: it picks the culling mask and the sideline the boom sits on, and both
            // are read once in Awake. Left unset, both walls came up Home — two cameras at the same z showing
            // the same team, on the two walls whose entire purpose is to show different ones.
            var home = Cam<TeamIsoPitchCamera>(root.transform, "HomeIsoCamera");
            var away = Cam<TeamIsoPitchCamera>(root.transform, "AwayIsoCamera");
            home.SetSide(TeamSide.Home);
            away.SetSide(TeamSide.Away);

            return new WallRig
            {
                Front = Cam<BroadcastPitchCamera>(root.transform, "BroadcastCamera"),
                Right = home,
                Left  = away,
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
            // No extra rotation. A CaveRegion orients +Z INTO the surface precisely so the readable face
            // points back at the operator, and a Unity Quad's normal is -Z — the same convention the
            // world-space UGUI panels in CaveBox already rely on. An earlier half-turn here turned every
            // panel to face into the wall, and backface culling made all four of them invisible.
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
