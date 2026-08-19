using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Stadium3D.Football;
using Stadium3D.Pitch;

namespace Stadium3D.EditorTools
{
    /// <summary>
    /// Builds the playable pitch: the field, twenty-two stylized markers, the ball, and the single driver
    /// that ticks the sim — plus a MiddleVR-free scene to watch it in.
    /// </summary>
    /// <remarks>
    /// <b>Why a separate scene.</b> <c>MVRManagerScript</c> disables every camera in the scene whose
    /// <c>targetTexture</c> is null at startup, which is exactly what a Game-view camera is. With the CAVE
    /// rig present there is therefore nothing to watch the pitch through until the M3 render-texture cameras
    /// exist, so "does the play look right" cannot be answered in the CAVE scene at all. A plain scene with
    /// one ordinary camera answers it today, and stays useful afterwards as the place to iterate on content
    /// without booting the room.
    ///
    /// <para><b>Shared materials, not property blocks.</b> The project has <c>m_UseSRPBatcher: 1</c> and
    /// <c>m_SupportsDynamicBatching: 0</c>; a <c>MaterialPropertyBlock</c> breaks the SRP batcher, and with
    /// 22 bodies drawn by up to twelve cameras a frame that is the wrong trade. Six shared materials cover
    /// every emphasis state, so all markers in a given state batch together.</para>
    /// </remarks>
    public static class PitchRigBuilder
    {
        const string RigName = "PitchRig";
        const string SceneFolder = "Assets/_Project/Scenes/Pitch";
        const string ScenePath = SceneFolder + "/PitchSandbox.unity";

        [MenuItem("Stadium3D/Build/Football Pitch Rig", priority = 11)]
        public static void BuildRig()
        {
            var world = GameObject.Find("PitchWorld") ?? new GameObject("PitchWorld");

            FootballFieldBuilder.BuildFootballField();

            var existing = world.transform.Find(RigName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var rig = new GameObject(RigName);
            rig.transform.SetParent(world.transform, false);

            var mats = MarkerMaterials.Create();
            var drive = ScriptedDrive.Build();
            var driver = rig.AddComponent<FootballSimDriver>();

            foreach (var identity in drive.Roster.All)
                BuildMarker(rig.transform, identity, mats);

            BuildBall(rig.transform);

            PitchLayers.ApplyRecursively(rig, PitchLayers.Players);

            // Re-stamp the jersey numbers after the sweep: they belong to the team walls alone, and the
            // recursive pass above would otherwise put them back on the shared player layer.
            int numberLayer = LayerMask.NameToLayer(PitchLayers.Numbers);
            if (numberLayer >= 0)
            {
                foreach (var text in rig.GetComponentsInChildren<TextMesh>(true))
                    text.gameObject.layer = numberLayer;
            }

            EditorUtility.SetDirty(rig);

            Debug.Log($"[Stadium3D] Pitch rig ready: {drive.Roster.All.Count} markers, {drive.Plays.Count} plays. " +
                      $"Open {ScenePath} and press Play, or run Stadium3D ▸ Build ▸ Pitch Sandbox Scene.", driver);
        }

        [MenuItem("Stadium3D/Build/Pitch Sandbox Scene", priority = 12)]
        public static void BuildSandboxScene()
        {
            if (!AssetDatabase.IsValidFolder(SceneFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Scenes", "Pitch");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // One ordinary camera. Nothing MiddleVR here on purpose — see the class remarks.
            var camGo = new GameObject("SandboxCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.07f);
            cam.farClipPlane = 400f;
            cam.cullingMask = PitchLayers.BroadcastMask | PitchLayers.Mask(PitchLayers.Tactical);
            camGo.transform.SetPositionAndRotation(
                new Vector3(-10f, 26f, -46f), Quaternion.Euler(28f, 22f, 0f));

            // A single directional key. Additional lights are disabled at the shipping quality level and
            // main-light shadows are off, so this is doing all of the work.
            var lightGo = new GameObject("PitchKeyLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.97f, 0.91f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(52f, 145f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.35f, 0.38f);
            RenderSettings.fog = false;

            BuildRig();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[Stadium3D] Built {ScenePath}. Press Play to watch the drive — no MiddleVR in this " +
                      "scene, so the Game view camera survives.");
        }

        // ── markers ────────────────────────────────────────────────────────────────────────────────

        static void BuildMarker(Transform parent, PlayerIdentity identity, MarkerMaterials mats)
        {
            var go = new GameObject($"#{identity.Number} {identity.Name}");
            go.transform.SetParent(parent, false);

            // Capsule body, 1.85 m tall, sunk so its base sits on the turf.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.925f, 0f);
            body.transform.localScale = new Vector3(0.62f, 0.925f, 0.62f);
            Object.DestroyImmediate(body.GetComponent<Collider>());

            var chevron = Quad("Chevron", go.transform,
                new Vector3(0f, FieldMarkings.OverlayY, 0.85f), new Vector3(0.7f, 1.1f, 1f), mats.Chevron);

            var blob = Quad("BlobShadow", go.transform,
                new Vector3(0f, FieldMarkings.OverlayY - 0.002f, 0f), new Vector3(1.15f, 1.15f, 1f), mats.BlobShadow);

            var ring = Quad("FocusRing", go.transform,
                new Vector3(0f, FieldMarkings.OverlayY + 0.002f, 0f), new Vector3(2.1f, 2.1f, 1f), mats.FocusRing);
            ring.enabled = false;

            // Legacy TextMesh rather than TMP: one draw, no atlas dependency, and the number only has to
            // read from the team walls where the player is close to life-size.
            var numberGo = new GameObject("Number");
            numberGo.transform.SetParent(go.transform, false);
            numberGo.transform.localPosition = new Vector3(0f, 2.15f, 0f);
            var number = numberGo.AddComponent<TextMesh>();
            number.text = identity.Number.ToString();
            // Sized against the 1.85 m body, not against the camera: a number roughly a third of a player's
            // height reads from the side walls without turning the broadcast view into a wall of digits.
            number.characterSize = 0.11f;
            number.fontSize = 72;
            number.anchor = TextAnchor.MiddleCenter;
            number.alignment = TextAlignment.Center;
            number.color = Color.white;

            // The number goes on its own layer so only the team walls show it — see PitchLayers.Numbers.
            // Applied after the marker's own layer sweep, which would otherwise fold it back in.
            int numberLayer = LayerMask.NameToLayer(PitchLayers.Numbers);
            if (numberLayer >= 0) numberGo.layer = numberLayer;

            var marker = go.AddComponent<PlayerMarker>();
            marker.Configure(body.GetComponent<Renderer>(), chevron, blob, ring, number);
            marker.ConfigureMaterials(mats.HomeNormal, mats.HomeDimmed, mats.HomeGhosted,
                                      mats.AwayNormal, mats.AwayDimmed, mats.AwayGhosted);
            marker.Bind(identity);

            // Park it on its pre-snap spot so the scene looks right before anyone presses Play. Local, like
            // everything else the sim drives — the pitch root may be parked far from the origin.
            go.transform.localPosition = FieldGeometry.Spot(ScriptedDrive.StartSituation.LineOfScrimmageYard, 0f);
        }

        static void BuildBall(Transform parent)
        {
            var go = new GameObject("Ball");
            go.transform.SetParent(parent, false);

            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            // Oversized on purpose. A regulation ball is 28 cm long; at the distance any camera has to stand
            // to frame a whole play that is a couple of pixels, and the ball is the one object every viewer
            // is tracking. Roughly double scale plus a bright rim is the difference between followable and
            // lost against the turf.
            body.transform.localScale = new Vector3(0.34f, 0.22f, 0.56f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial =
                MarkerMaterials.Lit("BallMat", new Color(0.86f, 0.55f, 0.22f));

            var tick = Quad("GroundTick", go.transform, Vector3.zero, new Vector3(1.2f, 1.2f, 1f),
                            MarkerMaterials.Unlit("BallTickMat", new Color(1f, 0.9f, 0.25f, 0.85f)));

            var ball = go.AddComponent<BallMarker>();
            ball.Configure(body.transform, tick.transform, null);
        }

        static Renderer Quad(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = localPos;
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // face up out of the turf
            quad.transform.localScale = localScale;
            Object.DestroyImmediate(quad.GetComponent<Collider>());

            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        // ── the shared material set ────────────────────────────────────────────────────────────────

        class MarkerMaterials
        {
            public Material HomeNormal, HomeDimmed, HomeGhosted;
            public Material AwayNormal, AwayDimmed, AwayGhosted;
            public Material Chevron, BlobShadow, FocusRing;

            public static MarkerMaterials Create()
            {
                var home = new Color(0.85f, 0.62f, 0.13f);   // Ironhawks gold
                var away = new Color(0.20f, 0.31f, 0.62f);   // Sentinels navy

                return new MarkerMaterials
                {
                    HomeNormal = Lit("MarkerHome", home),
                    HomeDimmed = Lit("MarkerHomeDim", Color.Lerp(home, Color.gray, 0.55f)),
                    HomeGhosted = Unlit("MarkerHomeGhost", new Color(home.r, home.g, home.b, 0.28f)),
                    AwayNormal = Lit("MarkerAway", away),
                    AwayDimmed = Lit("MarkerAwayDim", Color.Lerp(away, Color.gray, 0.55f)),
                    AwayGhosted = Unlit("MarkerAwayGhost", new Color(away.r, away.g, away.b, 0.28f)),
                    Chevron = Unlit("MarkerChevron", new Color(1f, 1f, 1f, 0.55f)),
                    BlobShadow = Unlit("MarkerBlob", new Color(0f, 0f, 0f, 0.38f)),
                    FocusRing = Unlit("MarkerFocusRing", new Color(1f, 0.78f, 0.22f, 0.9f)),
                };
            }

            public static Material Lit(string name, Color color)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var m = new Material(shader) { name = name };
                m.SetColor("_BaseColor", color);
                m.SetColor("_Color", color);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
                return m;
            }

            /// <summary>
            /// Transparent unlit, set up the way the rest of this project does it — explicit blend state and
            /// the surface keyword, because a URP Unlit material created from script defaults to opaque and
            /// silently ignores the alpha you set on it.
            /// </summary>
            public static Material Unlit(string name, Color color)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                var m = new Material(shader) { name = name };
                m.SetColor("_BaseColor", color);
                m.SetColor("_Color", color);
                m.SetFloat("_Surface", 1f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                return m;
            }
        }
    }
}
