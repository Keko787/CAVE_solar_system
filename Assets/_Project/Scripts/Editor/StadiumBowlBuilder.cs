using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Stadium3D.Pitch;

namespace Stadium3D.EditorTools
{
    /// <summary>
    /// Builds the bowl around the field: a raked lower deck, a concourse, an upper wall and a rim.
    /// </summary>
    /// <remarks>
    /// <b>This exists to remove a void, not to model a stadium.</b> The side walls sit 3.4 m off the turf
    /// looking almost level, so everything above the far sideline was the camera's clear colour — a black
    /// band across the top third of both team walls. Framing them at 2.5 wall-heights made it worse by
    /// showing more of it. Nothing else on those surfaces reads as wrong; the emptiness does.
    ///
    /// <para><b>Stepped, not ramped, and two-toned on purpose.</b> A smooth rake in one colour reads as a
    /// grey embankment. Real seating is a staircase, and alternating two close tones down the steps gives
    /// horizontal banding that reads as rows at forty metres without a single texture — which matters,
    /// because a generated texture is another asset that has to survive the scene save, and this project
    /// has already lost a milestone to state that did not.
    /// </para>
    ///
    /// <para><b>Never on the floor.</b> <see cref="PitchLayers.FloorMask"/> deliberately omits
    /// <see cref="PitchLayers.Stadium"/> — seen from directly overhead a bowl is a ring of clutter around
    /// the one view that has to stay legible, and it would occlude the sidelines at 1:22.</para>
    /// </remarks>
    public static class StadiumBowlBuilder
    {
        const string RootName = "StadiumBowl";

        /// <summary>Points around each rounded corner. Thirty-two around the whole ring is plenty.</summary>
        const int CornerSegments = 8;

        /// <summary>Rows in the lower deck. Each is a riser and a tread, alternating tone.</summary>
        const int LowerRows = 10;

        /// <summary>Rows in the upper deck, which is what fills the top of a side wall's frame.</summary>
        const int UpperRows = 8;

        [MenuItem("Stadium3D/Build/Stadium Bowl", priority = 13)]
        public static GameObject BuildBowl()
        {
            var world = GameObject.Find("PitchWorld") ?? new GameObject("PitchWorld");

            var existing = world.transform.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var root = new GameObject(RootName);
            root.transform.SetParent(world.transform, false);

            var path = RingPath();
            var profile = Profile(out var submeshOf);

            var mesh = Sweep(path, profile, submeshOf, submeshCount: 3);

            var go = new GameObject("Bowl");
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[]
            {
                // Brighter than they look on a colour picker. These surfaces face inward, away from a key
                // light aimed down at the turf, so almost all their output is the ambient term - the first
                // pass used values that read as charcoal on paper and as pure void on the wall, which is
                // exactly the thing the bowl was built to remove.
                Lit("BowlStructure", new Color(0.235f, 0.245f, 0.285f)),
                Lit("BowlSeatingA", new Color(0.350f, 0.360f, 0.430f)),
                Lit("BowlSeatingB", new Color(0.445f, 0.455f, 0.520f)),
            };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            PitchLayers.ApplyRecursively(root, PitchLayers.Stadium);

            Debug.Log($"[Stadium3D] Stadium bowl: {mesh.vertexCount} verts, " +
                      $"{mesh.triangles.Length / 3} triangles, 3 materials. Broadcast and both team walls " +
                      "see it; the floor deliberately does not.");
            return root;
        }

        // ── the ring ───────────────────────────────────────────────────────────────────────────────

        struct PathPoint
        {
            public Vector3 Position;
            public Vector3 Outward;
        }

        /// <summary>
        /// A rounded rectangle around the field, as positions with the direction "away from the pitch".
        /// </summary>
        /// <remarks>
        /// Every point sits on one of the four corner arcs, so its outward direction is simply the radius
        /// it stands on — and the straight sides fall out as the spans between arcs, which is why there is
        /// no special case for them anywhere below.
        /// </remarks>
        static List<PathPoint> RingPath()
        {
            // Clear of the apron by enough that the low wall never crowds a sideline camera.
            float halfLength = FieldMarkings.HalfMarkedLengthMeters + 12f;
            float halfWidth = FieldMarkings.HalfWidthMeters + 12f;
            float corner = 20f;

            float cx = halfLength - corner;
            float cz = halfWidth - corner;

            var points = new List<PathPoint>(CornerSegments * 4);
            AddArc(points, new Vector3(cx, 0f, cz), corner, 0f, 90f);
            AddArc(points, new Vector3(-cx, 0f, cz), corner, 90f, 180f);
            AddArc(points, new Vector3(-cx, 0f, -cz), corner, 180f, 270f);
            AddArc(points, new Vector3(cx, 0f, -cz), corner, 270f, 360f);
            return points;
        }

        static void AddArc(List<PathPoint> into, Vector3 center, float radius, float fromDeg, float toDeg)
        {
            for (int i = 0; i <= CornerSegments; i++)
            {
                float deg = Mathf.Lerp(fromDeg, toDeg, i / (float)CornerSegments);
                float rad = deg * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));

                into.Add(new PathPoint { Position = center + outward * radius, Outward = outward });
            }
        }

        // ── the cross section ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The bowl in cross section, as (metres outward, metres up) with the material each band uses.
        /// </summary>
        static List<Vector2> Profile(out List<int> submeshOf)
        {
            var profile = new List<Vector2>();
            var bands = new List<int>();

            void Step(float outward, float up, int submesh)
            {
                profile.Add(new Vector2(outward, up));
                if (profile.Count > 1) bands.Add(submesh);
            }

            // Surround first: a flat skirt running back under the turf edge. The bowl stands 12 m clear of
            // the sideline, and with nothing in between a low camera saw straight through the gap to the
            // clear colour - a black strip pinned to the horizon, which is the very thing the bowl is here
            // to remove and looks identical to it. Slightly below zero so it can never fight the apron.
            Step(-14f, -0.05f, 0);

            Step(0f, 0f, 0);
            Step(0f, 1.2f, 0);          // the wall a photographer leans on

            float o = 0f, u = 1.2f;
            for (int row = 0; row < LowerRows; row++)
            {
                int tone = 1 + row % 2;  // alternating seating tone, which is what reads as rows
                Step(o, u += 1.15f, tone);
                Step(o += 1.6f, u, tone);
            }

            // The dark gap between decks that every bowl has, and the one band up here meant to read as
            // structure rather than as people.
            Step(o += 2.4f, u += 0.8f, 0);
            Step(o, u += 2.2f, 0);

            // Upper deck, seating again rather than one tall wall. The first version capped the bowl with
            // a single flat slab, which from a camera 3.4 m off the turf is most of the frame and reads as
            // a dark curtain whatever colour it is painted - the same void, in a different shade.
            for (int row = 0; row < UpperRows; row++)
            {
                int tone = 1 + row % 2;
                Step(o, u += 1.5f, tone);
                Step(o += 1.9f, u, tone);
            }

            Step(o + 1.5f, u + 1f, 0);     // rim

            submeshOf = bands;
            return profile;
        }

        // ── sweep ──────────────────────────────────────────────────────────────────────────────────

        static Mesh Sweep(List<PathPoint> path, List<Vector2> profile, List<int> submeshOf, int submeshCount)
        {
            int rings = profile.Count;
            var verts = new Vector3[path.Count * rings];

            for (int p = 0; p < path.Count; p++)
            {
                for (int r = 0; r < rings; r++)
                {
                    verts[p * rings + r] =
                        path[p].Position + path[p].Outward * profile[r].x + Vector3.up * profile[r].y;
                }
            }

            var tris = new List<int>[submeshCount];
            for (int i = 0; i < submeshCount; i++) tris[i] = new List<int>();

            for (int p = 0; p < path.Count; p++)
            {
                int q = (p + 1) % path.Count;      // closed ring

                for (int r = 0; r < rings - 1; r++)
                {
                    int a = p * rings + r, b = p * rings + r + 1;
                    int c = q * rings + r, d = q * rings + r + 1;

                    // Wound to face inward, toward the field — the only side any camera stands on.
                    var t = tris[submeshOf[r]];
                    t.Add(a); t.Add(b); t.Add(d);
                    t.Add(a); t.Add(d); t.Add(c);
                }
            }

            // Which way these faces point is decided by the direction the sweep happens to run around the
            // ring, and getting it wrong lights every seat from behind. With two-sided materials the bowl
            // still draws, so nothing looks broken - it just renders near-black at any albedo, which is
            // indistinguishable from the void it was built to remove. Two rounds of brightening the
            // materials went past this before anyone measured it. So: measure it.
            var probe = tris[submeshOf[0]];
            var faceNormal = Vector3.Cross(verts[probe[1]] - verts[probe[0]], verts[probe[2]] - verts[probe[0]]);

            if (Vector3.Dot(faceNormal, -path[0].Outward) < 0f)
            {
                for (int i = 0; i < submeshCount; i++)
                {
                    for (int t = 0; t < tris[i].Count; t += 3)
                    {
                        int swap = tris[i][t + 1];
                        tris[i][t + 1] = tris[i][t + 2];
                        tris[i][t + 2] = swap;
                    }
                }
            }

            var mesh = new Mesh { name = "StadiumBowlMesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.subMeshCount = submeshCount;
            for (int i = 0; i < submeshCount; i++) mesh.SetTriangles(tris[i], i);

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Material Lit(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader) { name = name };
            m.SetColor("_BaseColor", color);
            m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);

            // Two-sided. The sweep is wound to face the field, and a bowl that vanishes from one wall
            // because a corner arc runs the other way is a confusing way to lose an evening.
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            return m;
        }
    }
}
