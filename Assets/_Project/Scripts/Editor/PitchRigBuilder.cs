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

            // Catch-all for the parts nobody placed deliberately. Anything the builders above stamped
            // themselves — the numbers, the two dressed bodies, the chevrons, the shadows, the ball — keeps
            // what it was given, so there is no second pass to forget.
            PitchLayers.ApplyToUnassigned(rig, PitchLayers.Players);

            // Built after the sweep above, or the recursive pass would put the overlay on the player
            // layer and the broadcast wall would start drawing chains across the play.
            BuildFieldStateOverlay(rig.transform);

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

            bool home = identity.Side == TeamSide.Home;

            // Three bodies, same geometry, one per kind of surface that needs its own version of a player.
            // The broadcast wall and the floor want everyone in full colour; each team wall wants its own
            // side lit and the opposition ghosted back. Both team walls render the same frame, so this
            // cannot be one renderer with a material chosen per camera — and swapping materials in
            // onPreCull would break the SRP batcher. Fixed materials, no per-frame work.
            var body = Body(go.transform, "Body", home ? mats.HomeNormal : mats.AwayNormal);

            var bodyRight = Body(go.transform, "BodyRight", home ? mats.HomeNormal : mats.AwayGhosted);
            var bodyLeft = Body(go.transform, "BodyLeft", home ? mats.HomeGhosted : mats.AwayNormal);

            Stamp(bodyRight, PitchLayers.RightWall);
            Stamp(bodyLeft, PitchLayers.LeftWall);

            var chevron = Quad("Chevron", go.transform,
                new Vector3(0f, FieldMarkings.OverlayY, 0.85f), new Vector3(0.7f, 1.1f, 1f), mats.Chevron);

            var blob = Quad("BlobShadow", go.transform,
                new Vector3(0f, FieldMarkings.OverlayY - 0.002f, 0f), new Vector3(1.15f, 1.15f, 1f), mats.BlobShadow);

            // Neither of these wants a per-wall variant — a facing arrow and a shadow read the same to
            // everyone — so they move to the shared layer rather than growing a copy per surface. Without
            // this they would vanish from both team walls the moment Players left those masks.
            Stamp(chevron.gameObject, PitchLayers.Shared);
            Stamp(blob.gameObject, PitchLayers.Shared);

            var ring = Ring("FocusRing", go.transform, mats);

            // One ring per team wall. No camera sees more than one of the three, so they can share a
            // height without fighting for it.
            var ringRight = Ring("FocusRingRight", go.transform, mats);
            Stamp(ringRight.gameObject, PitchLayers.RightWall);

            var ringLeft = Ring("FocusRingLeft", go.transform, mats);
            Stamp(ringLeft.gameObject, PitchLayers.LeftWall);

            // Legacy TextMesh rather than TMP: one draw, no atlas dependency, and the number only has to
            // read from the team walls where the player is close to life-size.
            var numberGo = new GameObject("Number");
            numberGo.transform.SetParent(go.transform, false);
            numberGo.transform.localPosition = new Vector3(0f, PlayerMarker.NumberRestHeight, 0f);
            var number = numberGo.AddComponent<TextMesh>();
            number.text = identity.Number.ToString();
            // Sized against the 1.85 m body, not against the camera: a number roughly a third of a player's
            // height reads from the side walls without turning the broadcast view into a wall of digits.
            number.characterSize = 0.11f;
            number.fontSize = 72;
            number.anchor = TextAnchor.MiddleCenter;
            number.alignment = TextAlignment.Center;
            number.color = Color.white;

            // The number rides its own player's wall layer rather than a shared one, so each team wall
            // labels its own eleven and nobody else's. A single Numbers layer is visible to both walls or
            // to neither, which had the away wall drawing numbers for eleven opponents it had just ghosted
            // out — and stacking them into a pile along the horizon on the way.
            Stamp(numberGo, home ? PitchLayers.RightWall : PitchLayers.LeftWall);

            var marker = go.AddComponent<PlayerMarker>();
            marker.Configure(body.GetComponent<Renderer>(), chevron, blob, ring, number);
            marker.ConfigureWallRings(ringRight, ringLeft);
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

            // Shared, not Players: the team walls stopped carrying Players when the dressed bodies arrived,
            // and a wall following a receiver with no ball in the picture is worse than no wall at all.
            Stamp(body, PitchLayers.Shared);

            var tick = Quad("GroundTick", go.transform, Vector3.zero, new Vector3(1.2f, 1.2f, 1f),
                            MarkerMaterials.Unlit("BallTickMat", new Color(1f, 0.9f, 0.25f, 0.85f)));

            var ball = go.AddComponent<BallMarker>();
            ball.Configure(body.transform, tick.transform, null);
        }

        /// <summary>
        /// A focus ring lying flat on the turf, disabled until some wall claims this player.
        /// </summary>
        /// <remarks>
        /// An annulus rather than a quad. The original was a filled 2.1 m square, which never showed until
        /// something actually focused a player — and then arrived as a solid gold slab covering the yard
        /// lines under the very body it was supposed to draw attention to. A highlight that hides the
        /// ground it highlights is worse than none.
        /// </remarks>
        static Renderer Ring(string name, Transform parent, MarkerMaterials mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, FieldMarkings.OverlayY + 0.002f, 0f);

            go.AddComponent<MeshFilter>().sharedMesh = mats.RingMesh;

            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mats.FocusRing;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;
            return r;
        }

        /// <summary>
        /// A flat ring in the XZ plane. Built once and shared by all sixty-six rings in the scene.
        /// </summary>
        static Mesh BuildRingMesh(float inner, float outer, int segments)
        {
            var verts = new Vector3[segments * 2];
            var tris = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts[i * 2] = dir * inner;
                verts[i * 2 + 1] = dir * outer;
            }

            for (int i = 0; i < segments; i++)
            {
                int a = i * 2, b = a + 1;
                int c = (i + 1) % segments * 2, d = c + 1;

                tris[i * 6 + 0] = a; tris[i * 6 + 1] = b; tris[i * 6 + 2] = d;
                tris[i * 6 + 3] = a; tris[i * 6 + 4] = d; tris[i * 6 + 5] = c;
            }

            var mesh = new Mesh { name = "FocusRingMesh" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>One stylized body. Every copy shares these numbers so they cannot drift apart.</summary>
        static GameObject Body(Transform parent, string name, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.SetParent(parent, false);

            // 1.85 m tall, sunk so the base sits on the turf.
            go.transform.localPosition = new Vector3(0f, 0.925f, 0f);
            go.transform.localScale = new Vector3(0.62f, 0.925f, 0.62f);

            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>Place one object on a named layer, deliberately, so the later sweep leaves it alone.</summary>
        static void Stamp(GameObject go, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (go != null && layer >= 0) go.layer = layer;
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

        /// <summary>
        /// The two broadcast lines on the turf: where the ball is, and where the offence has to reach.
        /// </summary>
        /// <remarks>
        /// Drawn at three times a painted stripe. The floor frames 48.9 m across a 2.2225 m surface, so a
        /// real 4-inch line arrives about 4.6 mm wide and is the first thing lost to projector softness —
        /// television widens these lines for the same reason. Three times 4 inches is a clean foot.
        /// </remarks>
        static void BuildFieldStateOverlay(Transform parent)
        {
            var root = new GameObject("FieldStateOverlay");
            root.transform.SetParent(parent, false);

            var scrimmage = Strip(root.transform, "ScrimmageLine",
                                  new Color(0.30f, 0.55f, 1f, 0.80f));
            var lineToGain = Strip(root.transform, "LineToGain",
                                   new Color(1f, 0.85f, 0.05f, 0.92f));

            root.AddComponent<FieldStateOverlay>()
                .Configure(scrimmage.transform, lineToGain.transform);

            PitchLayers.ApplyRecursively(root, PitchLayers.Tactical);
        }

        /// <summary>A flat strip lying across the full width of the field at one yard line.</summary>
        static GameObject Strip(Transform parent, string name, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);

            // +90 about X lays a Quad flat with its normal up, which is the one orientation the top-down
            // camera can see. The floor camera itself had this wrong once, as Euler(90, 90, 0).
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(FieldMarkings.LineWidth * 3f,
                                                  FieldMarkings.WidthMeters, 1f);

            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = MarkerMaterials.Unlit(name, color);
            return go;
        }

        class MarkerMaterials
        {
            public Material HomeNormal, HomeDimmed, HomeGhosted;
            public Material AwayNormal, AwayDimmed, AwayGhosted;
            public Material Chevron, BlobShadow, FocusRing;

            /// <summary>Shared by every focus ring in the scene — 66 renderers, one mesh.</summary>
            public Mesh RingMesh;

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
                    FocusRing = RingMaterial(),
                    RingMesh = BuildRingMesh(0.82f, 1.05f, 48),
                };
            }

            /// <summary>
            /// The focus ring's material, drawn on both faces.
            /// </summary>
            /// <remarks>
            /// Two-sided because the ring is generated geometry lying flat on the turf, and a winding order
            /// worked out on paper is exactly the kind of thing that is right until someone regenerates the
            /// mesh. The failure — an invisible highlight — looks identical to focus not working at all.
            /// </remarks>
            static Material RingMaterial()
            {
                var m = Unlit("MarkerFocusRing", new Color(1f, 0.78f, 0.22f, 0.9f));
                if (m.HasProperty("_Cull"))
                    m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
                return m;
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
