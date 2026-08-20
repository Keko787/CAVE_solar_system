using UnityEngine;
using Stadium3D.Pitch;

namespace Stadium3D.EditorTools
{
    /// <summary>
    /// The dark frame around each CAVE surface.
    /// </summary>
    public static class WallBezelBuilder
    {
        /// <summary>
        /// Uniform border width, metres. Read as an angle, not as a length.
        /// </summary>
        /// <remarks>
        /// A viewer stands about 1.11 m from the front wall, so 6 cm subtends roughly 3° — and at a corner
        /// two of them meet, which is 6° of division between one picture and the next. Three centimetres
        /// was the first try and disappeared at that distance: present in the render, and not doing the job
        /// it was added for. Nothing on any surface is anywhere near the edge — the front wall's score sits
        /// 0.46 m in, the team cards 0.36 m up — so this costs no content at all.
        /// </remarks>
        public const float BezelMeters = 0.06f;

        /// <summary>
        /// A dark frame around one surface, so two pictures meeting at a corner read as two pictures.
        /// </summary>
        /// <remarks>
        /// <b>Four different views butt together with nothing between them.</b> The front wall and a team
        /// wall share a vertical corner and show the same football from two places at once; the floor meets
        /// all three. Edge to edge, that reads as one continuous image that has gone wrong somewhere,
        /// because a viewer's assumption about adjacent surfaces showing a continuous scene is doing exactly
        /// what a CAVE normally wants it to do. A bezel says these are screens.
        ///
        /// <para><b>It costs essentially no picture.</b> Each panel is already inset from its surface — 3.50
        /// inside 3.556, 2.19 inside 2.2225 — so 2.8 cm of the front wall's width and 1.6 cm of every
        /// surface's height are unused today. The frame runs from the true wall edge inward, which spends
        /// that margin first and overlaps the image by at most 14 mm of a 2.19 m panel: under 1%, and none
        /// of it where anything is drawn. Sizing the panels down instead would have been the obvious move
        /// and the wrong one — the side walls' framing arithmetic says a 1.88 m player renders
        /// <c>1.88 / 2.5</c> metres tall <em>on the surface</em>, and shrinking the surface quietly makes
        /// that claim false.</para>
        ///
        /// <para><b>Two millimetres proud, not two centimetres.</b> §3.4 rules out floating content in front
        /// of the picture at 2 cm because of the stereo comfort cost; this is a tenth of that, on a static
        /// object at the wall, and it only has to beat z-fighting.</para>
        /// </remarks>
        public static GameObject Build(Transform panel, Vector2 wall)
        {
            var go = new GameObject("Bezel");
            go.transform.SetParent(panel, false);

            // -Z is toward the room: a CaveRegion orients +Z into the surface so the readable face points
            // back at the operator, which is the same convention the panel quad relies on.
            go.transform.localPosition = new Vector3(0f, 0f, -0.002f);

            go.AddComponent<MeshFilter>().sharedMesh = BezelMesh(wall, BezelMeters);

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = BezelMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return go;
        }

        /// <summary>A rectangular annulus: the wall's outline, with a hole the picture shows through.</summary>
        public static Mesh BezelMesh(Vector2 outer, float width)
        {
            float ox = outer.x * 0.5f, oy = outer.y * 0.5f;
            float ix = Mathf.Max(0.001f, ox - width), iy = Mathf.Max(0.001f, oy - width);

            var verts = new[]
            {
                new Vector3(-ox, -oy, 0f), new Vector3(-ox, oy, 0f), new Vector3(ox, oy, 0f), new Vector3(ox, -oy, 0f),
                new Vector3(-ix, -iy, 0f), new Vector3(-ix, iy, 0f), new Vector3(ix, iy, 0f), new Vector3(ix, -iy, 0f),
            };

            var tris = new int[24];
            for (int i = 0; i < 4; i++)
            {
                int a = i, b = (i + 1) % 4;

                tris[i * 6 + 0] = a;     tris[i * 6 + 1] = b;     tris[i * 6 + 2] = 4 + b;
                tris[i * 6 + 3] = a;     tris[i * 6 + 4] = 4 + b; tris[i * 6 + 5] = 4 + a;
            }

            var mesh = new Mesh { name = "BezelMesh" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Unlit and two-sided, for the same reasons <see cref="WallPanel"/> is.
        /// </summary>
        /// <remarks>
        /// Unlit because a bezel that catches the room's key light is a sheen that moves as the viewer does,
        /// and the panel beside it has none. Two-sided because a winding worked out on paper is exactly the
        /// kind of thing that is right until somebody regenerates the mesh, and the failure — no border at
        /// all — looks identical to this having never been built.
        /// </remarks>
        static Material BezelMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var m = new Material(shader) { name = "WallBezel" };

            var colour = new Color(0.045f, 0.050f, 0.062f);
            m.SetColor("_BaseColor", colour);
            m.SetColor("_Color", colour);

            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            return m;
        }
    }
}
