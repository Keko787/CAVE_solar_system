using UnityEditor;
using UnityEngine;
using Stadium3D.Football;
using Stadium3D.Pitch;

namespace Stadium3D.EditorTools
{
    /// <summary>
    /// Builds the M3.5 life-size figure: a person, at the size a person is, standing on the CAVE floor as
    /// real geometry rather than a picture.
    /// </summary>
    /// <remarks>
    /// The figure is assembled from primitives with <b>human proportions</b> rather than reused as the
    /// single capsule the wall markers use. That is not decoration. The question being asked is "does a body
    /// at true scale in stereo land differently from the same body flat on a wall", and a coloured pill at
    /// 1.88 m tests the wrong thing — it would answer "no" for reasons that have nothing to do with stereo.
    /// A head, shoulders, limbs and a stance are the minimum needed for the comparison to be about depth.
    ///
    /// <para>The proportions are a loose eight-head canon at 1.88 m: head 0.235 m, shoulder span 0.65 m,
    /// hip at 0.94 m, and a slight athletic crouch. Nobody will mistake it for a person, and nobody needs
    /// to — they need to be able to tell how tall it is without being told.</para>
    /// </remarks>
    public static class RoomHologramBuilder
    {
        const string RootName = "RoomHologram";

        /// <summary>Overall height, metres. The number the whole milestone is about.</summary>
        public const float FigureHeight = WallFraming.PlayerHeightMeters;

        [MenuItem("Stadium3D/Build/Life-Size Room Figure (M3.5)", priority = 21)]
        public static void BuildStandalone()
        {
            var existing = GameObject.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing);

            var go = Build(null);
            Selection.activeGameObject = go;
            Debug.Log($"[Stadium3D] Built {RootName}: {FigureHeight:0.00} m figure on the " +
                      $"{PitchLayers.Hologram} layer. It is the only stereoscopic content in the demo — the " +
                      "four walls are flat pictures.", go);
        }

        /// <summary>Build the figure under an optional parent (the CAVE room's transform).</summary>
        public static GameObject Build(Transform roomRoot)
        {
            var root = new GameObject(RootName);
            if (roomRoot != null) root.transform.SetParent(roomRoot, false);

            var figure = new GameObject("Figure");
            figure.transform.SetParent(root.transform, false);

            var skin = Material("HologramSkin", new Color(0.85f, 0.62f, 0.13f));   // Ironhawks gold
            var dark = Material("HologramTrim", new Color(0.14f, 0.15f, 0.17f));

            // Eight-head canon at 1.88 m. Every number below is a fraction of that height, so changing the
            // height alone rescales the whole figure correctly.
            const float H = FigureHeight;

            Part(figure.transform, "Head",      PrimitiveType.Sphere,  new Vector3(0f, 0.935f * H, 0f),  new Vector3(0.125f, 0.130f, 0.125f) * H, skin);
            Part(figure.transform, "Neck",      PrimitiveType.Capsule, new Vector3(0f, 0.880f * H, 0f),  new Vector3(0.055f, 0.030f, 0.055f) * H, skin);
            Part(figure.transform, "Torso",     PrimitiveType.Capsule, new Vector3(0f, 0.700f * H, 0f),  new Vector3(0.190f, 0.130f, 0.130f) * H, skin);
            Part(figure.transform, "Hips",      PrimitiveType.Capsule, new Vector3(0f, 0.520f * H, 0f),  new Vector3(0.165f, 0.060f, 0.125f) * H, dark);

            // Shoulders sit wide: the silhouette is most of what makes a figure read as a person at distance.
            Part(figure.transform, "ShoulderL", PrimitiveType.Sphere,  new Vector3(-0.120f * H, 0.815f * H, 0f), new Vector3(0.105f, 0.105f, 0.105f) * H, skin);
            Part(figure.transform, "ShoulderR", PrimitiveType.Sphere,  new Vector3( 0.120f * H, 0.815f * H, 0f), new Vector3(0.105f, 0.105f, 0.105f) * H, skin);

            Part(figure.transform, "ArmL",      PrimitiveType.Capsule, new Vector3(-0.145f * H, 0.680f * H, 0.02f * H), new Vector3(0.062f, 0.115f, 0.062f) * H, skin);
            Part(figure.transform, "ArmR",      PrimitiveType.Capsule, new Vector3( 0.145f * H, 0.680f * H, 0.02f * H), new Vector3(0.062f, 0.115f, 0.062f) * H, skin);

            // A slight crouch, so the stance reads as an athlete rather than a mannequin at attention.
            Part(figure.transform, "ThighL",    PrimitiveType.Capsule, new Vector3(-0.062f * H, 0.380f * H, 0.01f * H), new Vector3(0.078f, 0.125f, 0.078f) * H, dark);
            Part(figure.transform, "ThighR",    PrimitiveType.Capsule, new Vector3( 0.062f * H, 0.380f * H, 0.01f * H), new Vector3(0.078f, 0.125f, 0.078f) * H, dark);
            Part(figure.transform, "ShinL",     PrimitiveType.Capsule, new Vector3(-0.062f * H, 0.145f * H, 0f), new Vector3(0.062f, 0.115f, 0.062f) * H, dark);
            Part(figure.transform, "ShinR",     PrimitiveType.Capsule, new Vector3( 0.062f * H, 0.145f * H, 0f), new Vector3(0.062f, 0.115f, 0.062f) * H, dark);

            var ring = Quad(root.transform, "GroundRing", Vector3.zero, 0.9f,
                            Material("HologramRing", new Color(1f, 0.78f, 0.22f, 0.55f), transparent: true));

            var holo = root.AddComponent<RoomHologram>();
            holo.Configure(figure.transform, ring);

            PitchLayers.ApplyRecursively(root, PitchLayers.Hologram);
            return root;
        }

        /// <summary>
        /// A calibrated height rod beside the figure: ticks at every half metre to 2 m.
        /// </summary>
        /// <remarks>
        /// Present because "is it actually life-size?" has to be answerable by a viewer standing next to it,
        /// not by trusting a number in a log. Someone can hold a hand at their own shoulder and compare. It
        /// is scaffolding for the go/no-go and should come out once the answer is in.
        /// </remarks>
        public static GameObject BuildScaleRod(Transform roomRoot, Vector3 anchor)
        {
            var rod = new GameObject("HologramScaleRod");
            if (roomRoot != null) rod.transform.SetParent(roomRoot, false);
            rod.transform.localPosition = anchor + new Vector3(0.45f, 0f, 0f);

            var pale = Material("ScaleRod", new Color(0.92f, 0.92f, 0.88f));
            var mark = Material("ScaleRodTick", new Color(0.20f, 0.55f, 0.95f));

            Part(rod.transform, "Post", PrimitiveType.Cylinder, new Vector3(0f, 1f, 0f), new Vector3(0.018f, 1f, 0.018f), pale);

            for (int i = 1; i <= 4; i++)
            {
                float y = i * 0.5f;
                bool metre = i % 2 == 0;
                Part(rod.transform, $"Tick{y:0.0}m", PrimitiveType.Cube,
                     new Vector3(0f, y, 0f),
                     new Vector3(metre ? 0.16f : 0.10f, 0.012f, 0.012f),
                     metre ? mark : pale);
            }

            PitchLayers.ApplyRecursively(rod, PitchLayers.Hologram);
            return rod;
        }

        static void Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());

            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        static Transform Quad(Transform parent, string name, Vector3 pos, float size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos + new Vector3(0f, 0.004f, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = Vector3.one * size;
            Object.DestroyImmediate(go.GetComponent<Collider>());

            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        static Material Material(string name, Color color, bool transparent = false)
        {
            var shader = transparent
                ? (Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"))
                : (Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));

            var m = new UnityEngine.Material(shader) { name = name };
            m.SetColor("_BaseColor", color);
            m.SetColor("_Color", color);

            if (transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else if (m.HasProperty("_Smoothness"))
            {
                m.SetFloat("_Smoothness", 0.18f);
                m.SetFloat("_Metallic", 0f);
            }

            return m;
        }
    }
}
