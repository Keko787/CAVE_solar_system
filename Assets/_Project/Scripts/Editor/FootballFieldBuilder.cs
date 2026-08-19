using System.Collections.Generic;
using Stadium3D.Football;
using Stadium3D.Pitch;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Stadium3D.EditorTools
{
    /// <summary>
    /// Builds the NFL field as real triangles: striped turf, every painted line, the numbers, the posts and
    /// the pylons, under "PitchWorld/Field".
    /// </summary>
    /// <remarks>
    /// <para><b>Why geometry and not decals.</b> A <c>DecalRendererFeature</c> is a per-renderer setting, and
    /// the renderer this project ships is shared with the whole CAVE room. Turning decals on there forces a
    /// depth prepass on every camera that uses it — eight MiddleVR eye cameras plus four offscreen pitch
    /// cameras, twelve renders a frame — each paying an MSAA-4 depth resolve, to draw lines that cost
    /// essentially nothing as triangles. Decals would also be paid for by the room, which draws none.</para>
    ///
    /// <para><b>Why geometry and not one baked texture.</b> Holding the numerals at the density the broadcast
    /// camera wants over a 110 by 49 metre field needs a 16k by 8k asset, roughly a hundred times the memory
    /// of the small glyph atlas this will eventually use, and it is still softer: every line edge becomes a
    /// resampled ramp instead of a hard triangle edge, and at the grazing angle of the sideline ISO camera
    /// the mips take it to grey. Triangle edges are sharp at every zoom and cost nothing to store.</para>
    ///
    /// <para><b>The non-overlap rule.</b> Every painted rectangle sits at the same Y, so paint-on-paint
    /// z-fighting is prevented by never letting two rectangles share area at all, rather than by depth bias
    /// (whose sign flips under reversed-Z and which interacts badly with the MSAA resolve). Four clauses make
    /// that true by construction: yard lines stop at the sideline's inner edge; the end lines own all four
    /// mitred corners of the boundary frame so the sidelines can stop short of them; ticks are only emitted
    /// at stations that are not multiples of five, because a multiple of five already has a full-width line;
    /// and the goal line is painted entirely into the end zone so it abuts the sideline without crossing it.
    /// <see cref="PaintRects"/> hands the whole set to an EditMode test that asserts zero overlaps.</para>
    ///
    /// <para><b>The Y stack</b> does the rest: turf at 0, paint at +10 mm, glyphs at +12 mm. Ten millimetres
    /// over a 110 m field is far inside reversed-Z float precision, and it survives the phase-2 floor
    /// miniature — at 1:31 the offset is still 0.32 mm, resolvable and invisible.</para>
    ///
    /// <para><b>Coordinates</b> come from <see cref="FieldGeometry"/>, never from the numbers in a spec sheet.
    /// Everything positional is a <see cref="FieldGeometry.Spot"/> call on an absolute yard and a lateral
    /// yard offset. Only the things <see cref="FieldGeometry"/> does not own — stripe widths, glyph sizes,
    /// tube diameters — are literals here, and each one carries its imperial source in a comment.</para>
    /// </remarks>
    public static class FootballFieldBuilder
    {
        // ── material colours ───────────────────────────────────────────────────────────────────────
        // Authored as sRGB hex. The project renders in Linear, and Material.SetColor on a Color-typed
        // property such as _BaseColor applies the gamma-to-linear conversion itself, so these go straight
        // through with no manual conversion. Held as consts so a test and the builder cannot drift apart.

        /// <summary>Light mow band. Roughly 1.8x the luminance of the dark band — reads on camera without
        /// looking cartoon.</summary>
        public const string TurfLightHex = "#3D7A44";

        /// <summary>Dark mow band.</summary>
        public const string TurfDarkHex = "#2D5C33";

        /// <summary>Field paint. Deliberately not pure white: the shipping renderer has no tonemapper and no
        /// HDR, so a 1.0 white clips flat instead of reading as bright paint.</summary>
        public const string PaintHex = "#D8D8D4";

        /// <summary>Fill of the end zone behind the away goal line (+X). Placeholder team primary.</summary>
        public const string EndZoneAwayHex = "#123A6B";

        /// <summary>Fill of the end zone behind the home goal line (-X). Placeholder team primary.</summary>
        public const string EndZoneHomeHex = "#7A1220";

        /// <summary>Goal post safety gold.</summary>
        public const string GoalPostHex = "#F2C218";

        /// <summary>Goal post base padding.</summary>
        public const string PostPadHex = "#26282B";

        /// <summary>Pylon orange.</summary>
        public const string PylonHex = "#FF5A1F";

        /// <summary>The apron plane. Dark enough to read as "outside the stadium", light enough not to be a
        /// black hole in a stereo view.</summary>
        public const string ApronHex = "#1A1C1A";

        public static Color TurfLight => Hex(TurfLightHex);
        public static Color TurfDark => Hex(TurfDarkHex);
        public static Color Paint => Hex(PaintHex);
        public static Color EndZoneAway => Hex(EndZoneAwayHex);
        public static Color EndZoneHome => Hex(EndZoneHomeHex);
        public static Color GoalPostGold => Hex(GoalPostHex);
        public static Color PostPad => Hex(PostPadHex);
        public static Color PylonOrange => Hex(PylonHex);
        public static Color Apron => Hex(ApronHex);

        /// <summary>
        /// How many rectangles <see cref="PaintRects"/> returns: 19 yard lines, 2 goal lines, 4 boundary
        /// stripes, 4 border-ring bands, 320 ticks, 2 conversion marks. The test asserts this so a silently
        /// dropped loop shows up as a count mismatch rather than as a missing line nobody looks at.
        /// </summary>
        public const int PaintRectCount = 19 + 2 + 4 + 4 + 320 + 2;

        /// <summary>Directional arrows. One per number block except the two 50s, both sides.</summary>
        public const int ArrowCount = 16;

        // ── the numbers FieldGeometry does not own ──────────────────────────────────────────────────

        /// <summary>Every boundary and yard stripe: 4 in.</summary>
        const float StripeMeters = 0.1016f;

        /// <summary>NFL goal line: 8 in. It is the only line whose width differs from the rest (NCAA paints
        /// 4 in), and it is painted entirely into the end zone so the scoring plane sits exactly on the
        /// mathematical goal line rather than somewhere inside a stripe.</summary>
        const float GoalLineMeters = 0.2032f;

        /// <summary>Solid white border outside the boundary frame: 6 ft.</summary>
        const float BorderMeters = 1.8288f;

        /// <summary>Hash and sideline ticks: 24 in of length, run perpendicular to the sideline.</summary>
        const float TickMeters = 0.6096f;

        /// <summary>Conversion mark: 24 in across the field.</summary>
        const float ConversionMeters = 0.6096f;

        /// <summary>The conversion mark sits 2 yd in front of the goal-line plane (NCAA uses 3).</summary>
        const float ConversionYards = 2f;

        /// <summary>Mow band width. Boundaries coincide with the 5-yard lines by construction; set this to 10
        /// for the wider pattern.</summary>
        const float MowBandYards = 5f;

        /// <summary>The turf albedo tiles every 2 m, which is 256 texels per metre off a 512 map — already
        /// heavily oversampled for any camera that looks at this field.</summary>
        const float TurfUvMeters = 2f;

        /// <summary>Numerals: 6 ft of cap height.</summary>
        const float DigitHeightMeters = 1.8288f;

        /// <summary>Numerals: 4 ft of cell width per digit.</summary>
        const float DigitWidthMeters = 1.2192f;

        /// <summary>The yard line runs through a 12 in gap between the two digits.</summary>
        const float DigitGapMeters = 0.3048f;

        /// <summary>Sideline-facing (bottom) edge of the numerals, in yards from the sideline.</summary>
        /// <remarks>
        /// The one figure worth checking against an official diagram before final paint. Sources phrase it
        /// inconsistently as "bottom at 12 yd" against "top at 12 yd", a 2 yd difference, and NCAA uses 9.
        /// Purely cosmetic, visible in every wide shot, and a one-line correction because it lives here.
        /// </remarks>
        const float NumberNearYards = 12f;

        /// <summary>Centre-facing (top) edge of the numerals, in yards from the sideline.</summary>
        const float NumberFarYards = 14f;

        /// <summary>Arrow: 36 in long along the field.</summary>
        const float ArrowLengthMeters = 0.9144f;

        /// <summary>Arrow: 18 in of base across the field.</summary>
        const float ArrowBaseMeters = 0.4572f;

        /// <summary>Arrow base sits 6 in outboard of the outer digit edge.</summary>
        const float ArrowClearanceMeters = 0.1524f;

        /// <summary>End-zone wordmark cap height: 8 ft.</summary>
        const float WordmarkHeightMeters = 2.4384f;

        /// <summary>Wordmarks auto-fit to at most three quarters of the field width.</summary>
        const float WordmarkWidthFraction = 0.75f;

        // Y stack. Opaque layers are separated by at least 10 mm so coplanar fighting is impossible.
        const float TurfY = 0f;
        const float PaintY = 0.010f;
        const float GlyphY = 0.012f;
        const float ApronY = -0.020f;

        // Goal posts. All tubes 4 in except the gooseneck standard and its arm, which are 8 in.
        const float PostTubeRadius = 0.0508f;
        const float StandardRadius = 0.1016f;
        const float CrossbarTopY = 3.048f;              // 10 ft to the top of the bar
        const float UprightTopY = 13.716f;              // 45 ft to the tops
        const float CrossbarSpanMeters = 5.842f;        // 19 ft 2 in outside to outside
        const float StandardSetbackMeters = 1.8288f;    // 6 ft behind the end line
        const float PadRadius = 0.25f;
        const float PadHeightMeters = 1.8288f;
        const int ArmSegments = 8;

        // Pylons: 4 x 4 x 18 in.
        const float PylonSideMeters = 0.1016f;
        const float PylonHeightMeters = 0.4572f;

        // Apron: large enough that no pitch camera ever frames its edge or the void beyond it.
        const float ApronLengthMeters = 180f;
        const float ApronWidthMeters = 120f;

        const string WorldName = "PitchWorld";
        const string FieldName = "Field";

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");

        // ── menu ───────────────────────────────────────────────────────────────────────────────────

        [MenuItem("Stadium3D/Build/Football Field", priority = 10)]
        public static void BuildFootballField()
        {
            const string undo = "Build Football Field";

            var world = EnsureWorldRoot(undo);

            // Idempotent by demolition. The field is pure output — nothing is wired into it and nothing is
            // authored on it by hand — so rebuilding it wholesale is safer than reconciling a hierarchy and
            // leaves no half-updated mesh behind when a constant changes.
            var stale = world.transform.Find(FieldName);
            if (stale != null)
                Undo.DestroyObjectImmediate(stale.gameObject);

            var field = new GameObject(FieldName);
            Undo.RegisterCreatedObjectUndo(field, undo);
            field.transform.SetParent(world.transform, false);
            SetPitchLayer(field);

            var apronMesh = BuildApronMesh();
            var turfMesh = BuildTurfMesh();
            var paintMesh = BuildPaintMesh();
            var postsMesh = BuildGoalPostsMesh();
            var pylonsMesh = BuildPylonsMesh();

            AddRenderer(field.transform, "Apron", apronMesh,
                new[] { Lit("PitchApron", Apron, 0.05f) }, ShadowCastingMode.Off, undo);

            AddRenderer(field.transform, "FieldTurf", turfMesh, new[]
            {
                Lit("TurfLight", TurfLight, 0.12f),
                Lit("TurfDark", TurfDark, 0.12f),
                Lit("EndZoneAway", EndZoneAway, 0.12f),
                Lit("EndZoneHome", EndZoneHome, 0.12f)
            }, ShadowCastingMode.Off, undo);

            AddRenderer(field.transform, "FieldPaint", paintMesh,
                new[] { Lit("FieldPaint", Paint, 0.20f) }, ShadowCastingMode.Off, undo);

            int glyphs = BuildNumbers(field.transform, undo);

            AddRenderer(field.transform, "GoalPosts", postsMesh,
                new[] { Lit("GoalPostGold", GoalPostGold, 0.55f), Lit("GoalPostPad", PostPad, 0.15f) },
                ShadowCastingMode.On, undo);

            AddRenderer(field.transform, "Pylons", pylonsMesh,
                new[] { Lit("Pylon", PylonOrange, 0.25f) }, ShadowCastingMode.On, undo);

            var rects = PaintRects();
            int overlaps = CountPaintOverlaps(rects);

            Selection.activeGameObject = field;
            EditorGUIUtility.PingObject(field);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Debug.Log($"[Stadium3D] Football field built under {WorldName}/{FieldName}. " +
                      $"Turf {turfMesh.vertexCount} verts across 4 submeshes (light bands, dark bands, " +
                      $"away end zone, home end zone), paint {rects.Length} rectangles plus {ArrowCount} " +
                      $"arrows in one mesh and one draw, {glyphs} TextMeshPro glyphs, goal posts " +
                      $"{postsMesh.vertexCount} verts, pylons {pylonsMesh.vertexCount} verts.\n" +
                      $"Paint overlap check: {overlaps} overlapping pairs (must be 0 — everything painted " +
                      "sits at the same Y, so any overlap is a z-fight you will only notice in the CAVE).\n" +
                      "Nothing here is on a display-camera layer by accident: every object is on " +
                      $"\"{PitchLayers.Shared}\". If the field appears raw inside the room, the MiddleVR " +
                      "display cameras still have that bit set in their culling masks.", field);

            if (overlaps > 0)
                Debug.LogError($"[Stadium3D] {overlaps} painted rectangles overlap. The non-overlap rule is " +
                               "broken — run the EditMode field test to find which pair.", field);
        }

        // ── public mesh construction (no scene dependency, so an EditMode test can inspect it) ──────

        /// <summary>
        /// The turf: one mesh, four submeshes. Light mow bands, dark mow bands, then the two end-zone fills.
        /// </summary>
        /// <remarks>
        /// The bands cover the field of play only, goal line to goal line — twenty of them, not the
        /// twenty-four you get by dividing the whole marked field by five. The end zones are their own
        /// submeshes precisely so they can be team-coloured, and a band running under one would be a second
        /// surface at y = 0 sharing area with the fill, which is the exact coplanar fight the Y stack exists
        /// to prevent. Band boundaries still land on the 5-yard lines, which is the property that matters.
        /// </remarks>
        public static Mesh BuildTurfMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var light = new List<int>();
            var dark = new List<int>();
            var away = new List<int>();
            var home = new List<int>();

            float halfWidth = Z(FieldGeometry.HalfWidthYards);
            int bands = Mathf.RoundToInt(FieldGeometry.FieldLengthYards / MowBandYards);

            for (int i = 0; i < bands; i++)
            {
                float fromYard = FieldGeometry.HomeGoalLineYard + i * MowBandYards;
                float toYard = fromYard + MowBandYards;
                var band = FromEdges(X(fromYard), X(toYard), -halfWidth, halfWidth);
                AddTurfQuad(verts, uvs, (i & 1) == 0 ? light : dark, band, TurfY);
            }

            AddTurfQuad(verts, uvs, away, FromEdges(
                X(FieldGeometry.AwayGoalLineYard),
                X(FieldGeometry.AwayGoalLineYard + FieldGeometry.EndZoneDepthYards),
                -halfWidth, halfWidth), TurfY);

            AddTurfQuad(verts, uvs, home, FromEdges(
                X(FieldGeometry.HomeGoalLineYard - FieldGeometry.EndZoneDepthYards),
                X(FieldGeometry.HomeGoalLineYard),
                -halfWidth, halfWidth), TurfY);

            var mesh = NewMesh("FieldTurf", verts.Count);
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(UpNormals(verts.Count));
            mesh.subMeshCount = 4;
            mesh.SetTriangles(light, 0);
            mesh.SetTriangles(dark, 1);
            mesh.SetTriangles(away, 2);
            mesh.SetTriangles(home, 3);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Every painted mark on the field in one mesh, one material, one draw: <see cref="PaintRects"/>
        /// emitted as quads, then the sixteen directional arrows as bare triangles.
        /// </summary>
        /// <remarks>
        /// The mesh is built <em>from</em> <see cref="PaintRects"/> rather than alongside it, so the set the
        /// overlap test checks is by definition the set that gets drawn. A rectangle that the test never sees
        /// is the only way the non-overlap guarantee can quietly stop being true.
        /// </remarks>
        public static Mesh BuildPaintMesh()
        {
            var rects = PaintRects();

            var verts = new List<Vector3>(rects.Length * 4 + ArrowCount * 3);
            var uvs = new List<Vector2>(verts.Capacity);
            var tris = new List<int>(rects.Length * 6 + ArrowCount * 3);

            for (int i = 0; i < rects.Length; i++)
                AddQuad(verts, uvs, tris, rects[i], PaintY);

            AddArrows(verts, uvs, tris);

            var mesh = NewMesh("FieldPaint", verts.Count);
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(UpNormals(verts.Count));
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Every painted rectangle in field space, as a <see cref="Rect"/> whose x/width run along the field
        /// and whose y/height run across it. Handed to the EditMode test that asserts no two of them overlap.
        /// </summary>
        /// <remarks>
        /// Abutting is not overlapping: <see cref="Rect.Overlaps(Rect)"/> compares with strict inequality, so
        /// a yard line whose edge is exactly the sideline's edge passes. That only holds because both sides
        /// of every shared edge are computed from the same expression and are therefore bit-identical — which
        /// is why the edges below are named once and reused rather than re-derived per element.
        /// </remarks>
        public static Rect[] PaintRects()
        {
            float sidelineInner = Z(FieldGeometry.HalfWidthYards);
            float sidelineOuter = sidelineInner + StripeMeters;
            float endLineInner = X(FieldGeometry.AwayGoalLineYard + FieldGeometry.EndZoneDepthYards);
            float endLineOuter = endLineInner + StripeMeters;
            float borderX = endLineOuter + BorderMeters;
            float borderZ = sidelineOuter + BorderMeters;
            float hashInner = Z(FieldGeometry.HashLateralYards);
            float hashOuter = hashInner + TickMeters;
            float tickInner = sidelineInner - TickMeters;
            float halfStripe = StripeMeters * 0.5f;

            var rects = new List<Rect>(PaintRectCount);

            // 19 five-yard lines, clipped to the sideline's inner edge. They stop there because the inner
            // edge is the boundary; running them into the sideline stripe would double-paint it.
            for (int yard = 5; yard <= 95; yard += 5)
            {
                float cx = X(yard);
                rects.Add(FromEdges(cx - halfStripe, cx + halfStripe, -sidelineInner, sidelineInner));
            }

            // 2 goal lines, painted entirely into their end zone so the scoring plane is the mathematical
            // goal line and not an edge of a stripe.
            rects.Add(FromEdges(X(FieldGeometry.HomeGoalLineYard) - GoalLineMeters,
                                X(FieldGeometry.HomeGoalLineYard),
                                -sidelineInner, sidelineInner));
            rects.Add(FromEdges(X(FieldGeometry.AwayGoalLineYard),
                                X(FieldGeometry.AwayGoalLineYard) + GoalLineMeters,
                                -sidelineInner, sidelineInner));

            // Boundary frame. The end lines run the full height of the frame and so own all four mitred
            // corners; the sidelines stop at the end lines' inner edges. Somebody has to own the corners,
            // and giving them to the two shorter stripes keeps the split to one rule instead of four.
            rects.Add(FromEdges(-endLineOuter, -endLineInner, -sidelineOuter, sidelineOuter));
            rects.Add(FromEdges(endLineInner, endLineOuter, -sidelineOuter, sidelineOuter));
            rects.Add(FromEdges(-endLineInner, endLineInner, sidelineInner, sidelineOuter));
            rects.Add(FromEdges(-endLineInner, endLineInner, -sidelineOuter, -sidelineInner));

            // Solid white border, 6 ft out from the frame's outer edge. Same split, mirrored: the two long
            // bands take the corners, the two short ones stop at the frame's outer edge.
            rects.Add(FromEdges(-borderX, borderX, sidelineOuter, borderZ));
            rects.Add(FromEdges(-borderX, borderX, -borderZ, -sidelineOuter));
            rects.Add(FromEdges(endLineOuter, borderX, -sidelineOuter, sidelineOuter));
            rects.Add(FromEdges(-borderX, -endLineOuter, -sidelineOuter, sidelineOuter));

            // 320 ticks: two inbound rows and two sideline rows, at every 1-yard station that is not already
            // carrying a full-width line. Skipping the multiples of five is the whole of the tick half of the
            // non-overlap rule, and it is also what a real field looks like.
            for (int yard = 1; yard <= 99; yard++)
            {
                if (yard % 5 == 0)
                    continue;

                float cx = X(yard);
                float x0 = cx - halfStripe;
                float x1 = cx + halfStripe;

                // Inbound ticks run outward from the hash toward their sideline. The gap left between the
                // two rows is 18 ft 6 in, which is exactly the goal-post width — the build assertion.
                rects.Add(FromEdges(x0, x1, hashInner, hashOuter));
                rects.Add(FromEdges(x0, x1, -hashOuter, -hashInner));

                // Sideline ticks run inboard from the sideline's inner edge.
                rects.Add(FromEdges(x0, x1, tickInner, sidelineInner));
                rects.Add(FromEdges(x0, x1, -sidelineInner, -tickInner));
            }

            // 2 conversion marks, on the centreline 2 yd in front of each goal-line plane.
            float halfConversion = ConversionMeters * 0.5f;
            float homeConversion = X(FieldGeometry.HomeGoalLineYard + ConversionYards);
            float awayConversion = X(FieldGeometry.AwayGoalLineYard - ConversionYards);
            rects.Add(FromEdges(homeConversion - halfStripe, homeConversion + halfStripe,
                                -halfConversion, halfConversion));
            rects.Add(FromEdges(awayConversion - halfStripe, awayConversion + halfStripe,
                                -halfConversion, halfConversion));

            return rects.ToArray();
        }

        // ── paint detail ───────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The sixteen directional arrows. Triangles rather than rectangles, so they are not part of the
        /// overlap set — they cannot collide with anything because the only band of the field they occupy,
        /// between 12 and 14 yd from a sideline, contains nothing but the numerals, which live 12 mm higher.
        /// </summary>
        static void AddArrows(List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            float outerDigitEdge = DigitGapMeters * 0.5f + DigitWidthMeters;
            float baseOffset = outerDigitEdge + ArrowClearanceMeters;
            float apexOffset = baseOffset + ArrowLengthMeters;
            float nearEdge = Z(FieldGeometry.HalfWidthYards - NumberFarYards);
            float farEdge = nearEdge + ArrowBaseMeters;
            float apexZ = (nearEdge + farEdge) * 0.5f;

            foreach (float yard in NumberedYards())
            {
                if (Mathf.Approximately(yard, FieldGeometry.MidfieldYard))
                    continue;   // the 50 has no nearer goal line to point at

                float cx = X(yard);
                float toGoal = yard < FieldGeometry.MidfieldYard ? -1f : 1f;
                float baseX = cx + toGoal * baseOffset;
                float apexX = cx + toGoal * apexOffset;

                for (int s = 0; s < 2; s++)
                {
                    float side = s == 0 ? 1f : -1f;
                    AddUpTriangle(verts, uvs, tris,
                        new Vector3(baseX, PaintY, side * nearEdge),
                        new Vector3(baseX, PaintY, side * farEdge),
                        new Vector3(apexX, PaintY, side * apexZ));
                }
            }
        }

        /// <summary>The nine numbered stations, as absolute yards.</summary>
        static IEnumerable<float> NumberedYards()
        {
            for (float yard = 10f; yard <= 90f; yard += 10f)
                yield return yard;
        }

        // ── numbers ────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The eighteen yard-number blocks and the two end-zone wordmarks, as TextMeshPro 3D.
        /// </summary>
        /// <remarks>
        /// TMP rather than a baked glyph atlas for v1: the SDF stays sharp at any zoom, including the phase-2
        /// floor miniature, and there is nothing to author. It costs twenty draws instead of folding into the
        /// single paint draw, which the SRP batcher largely absorbs; the atlas is the optimisation pass, not
        /// the correctness pass.
        ///
        /// <para>One TMP object per digit rather than one per block. The spec pins the gap between the digits
        /// at 12 in with the yard line running through it, and character spacing cannot be trusted to land a
        /// specific gap at a specific world position across font assets — placing each glyph at its own cell
        /// centre can.</para>
        /// </remarks>
        static int BuildNumbers(Transform parent, string undo)
        {
            var root = new GameObject("FieldNumbers");
            Undo.RegisterCreatedObjectUndo(root, undo);
            root.transform.SetParent(parent, false);
            SetPitchLayer(root);

            var font = FindFont();
            if (font == null)
                Debug.LogWarning("[Stadium3D] No TextMeshPro font asset found, so the yard numbers fall back " +
                                 "to whatever TMP picks by default. Import TMP Essential Resources " +
                                 "(Window > TextMeshPro > Import TMP Essential Resources) and rebuild.");

            float digitOffset = DigitGapMeters * 0.5f + DigitWidthMeters * 0.5f;
            float bandCentreYards = FieldGeometry.HalfWidthYards - (NumberNearYards + NumberFarYards) * 0.5f;
            int glyphs = 0;

            foreach (float yard in NumberedYards())
            {
                string painted = FieldGeometry.MarkedYardLine(yard).ToString("00");

                for (int s = 0; s < 2; s++)
                {
                    float side = s == 0 ? 1f : -1f;

                    // Each block reads upright from its nearest sideline, so the two sides are 180-degree
                    // rotations of each other. Forward is straight down because a TMP mesh faces its own
                    // local -Z; pointing local +Z at the turf is what turns the glyph face skyward.
                    var rotation = Quaternion.LookRotation(Vector3.down, side * Vector3.back);

                    // Reading direction is the block's local +X in world space: -X on the +Z side, +X on the
                    // -Z side. The first digit therefore sits on the opposite side of the yard line from
                    // where a naive "first digit at lower X" would put it.
                    float readDirX = -side;
                    var spot = FieldGeometry.Spot(yard, side * bandCentreYards);

                    for (int d = 0; d < painted.Length; d++)
                    {
                        float offset = (d == 0 ? -1f : 1f) * readDirX * digitOffset;
                        var position = new Vector3(spot.x + offset, GlyphY, spot.z);

                        AddText(root.transform, $"Num{yard:000}_{(side > 0f ? "PlusZ" : "MinusZ")}_{d}",
                            painted[d].ToString(), position, rotation,
                            DigitWidthMeters, DigitHeightMeters, true, font, undo);
                        glyphs++;
                    }
                }
            }

            // End-zone wordmarks read from behind their own end zone, so the letters run across the field.
            float wordmarkWidth = Z(FieldGeometry.FieldWidthYards) * WordmarkWidthFraction;
            float endZoneCentre = X(FieldGeometry.AwayGoalLineYard + FieldGeometry.EndZoneDepthYards * 0.5f);

            AddText(root.transform, "Wordmark_Away", "AWAY",
                new Vector3(endZoneCentre, GlyphY, 0f),
                Quaternion.LookRotation(Vector3.down, Vector3.left),
                wordmarkWidth, WordmarkHeightMeters, false, font, undo);

            AddText(root.transform, "Wordmark_Home", "HOME",
                new Vector3(-endZoneCentre, GlyphY, 0f),
                Quaternion.LookRotation(Vector3.down, Vector3.right),
                wordmarkWidth, WordmarkHeightMeters, false, font, undo);

            return glyphs + 2;
        }

        /// <summary>
        /// One TMP glyph, generated at a nominal size and then scaled so its ink fills the cell it was given.
        /// </summary>
        /// <remarks>
        /// Point size is not a world measurement: how tall a "4" comes out at font size 26 depends on the
        /// face's own metrics, and the spec asks for exactly 6 ft of cap height and 4 ft of width. Measuring
        /// what was actually generated and scaling by the ratio is font-agnostic, so it survives somebody
        /// swapping the font asset — which is the likely next change here, since real field numerals use a
        /// condensed block face and TMP's default does not.
        ///
        /// <para><paramref name="condense"/> is what tells the two cases apart. A yard number is a fixed
        /// block: 6 ft by 4 ft, with the digits squashed to fit, because letting the width run would close
        /// the 12 in gap the yard line has to show through. A wordmark scales uniformly and lets whichever
        /// of height or width binds first win, so a long team name shrinks rather than distorting.</para>
        /// </remarks>
        static void AddText(Transform parent, string name, string text, Vector3 position, Quaternion rotation,
                            float cellWidth, float cellHeight, bool condense, TMP_FontAsset font, string undo)
        {
            var go = new GameObject(name, typeof(TextMeshPro));
            Undo.RegisterCreatedObjectUndo(go, undo);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;

            var label = go.GetComponent<TextMeshPro>();
            if (font != null)
                label.font = font;
            label.text = text;
            label.color = Paint;
            label.fontSize = 10f;
            label.enableAutoSizing = false;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;

            // Generous rect so nothing is truncated before it can be measured; the fit below decides the
            // real size, and the rect only has to be big enough not to interfere.
            label.rectTransform.sizeDelta = new Vector2(cellWidth * 8f, cellHeight * 8f);

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }

            label.ForceMeshUpdate();
            FitInk(label, position, rotation, cellWidth, cellHeight, condense);

            SetPitchLayer(go);
        }

        /// <summary>
        /// Scale and re-centre a generated label so its <em>cap height</em> lands on
        /// <paramref name="cellHeight"/> and its ink is centred on <paramref name="position"/>.
        /// </summary>
        /// <remarks>
        /// Two corrections, both of which are invisible until you look at the field from the sideline and
        /// notice the numbers are small and riding high.
        ///
        /// <para>First, <c>textBounds</c> measures the <em>line box</em> — ascender to descender — not the
        /// glyph. Scaling that to 6 ft leaves a digit around 30 per cent short, because a third of the box is
        /// empty space above the cap and below the baseline. The face's own metrics convert one to the
        /// other.</para>
        ///
        /// <para>Second, the ink is not centred in that box: a digit has no descender, so its middle sits
        /// above the box's middle. Placing the box centre on the yard line would ride every number about
        /// half a foot toward its sideline. Both corrections fall back to a plausible ratio if the font asset
        /// was built without face metrics, rather than refusing to place the number at all.</para>
        /// </remarks>
        static void FitInk(TMP_Text label, Vector3 position, Quaternion rotation,
                           float cellWidth, float cellHeight, bool condense)
        {
            var box = label.textBounds;
            if (box.size.x <= 1e-4f || box.size.y <= 1e-4f)
                return;   // nothing generated, so there is nothing to measure; leave the nominal size

            var face = label.font != null ? label.font.faceInfo : default;
            float lineSpan = face.ascentLine - face.descentLine;
            float capSpan = face.capLine - face.baseline;
            bool haveMetrics = lineSpan > 0f && capSpan > 0f;

            float capLocal = box.size.y * (haveMetrics ? capSpan / lineSpan : 0.7f);
            float fromHeight = cellHeight / capLocal;
            float fromWidth = cellWidth / box.size.x;
            float uniform = Mathf.Min(fromHeight, fromWidth);

            var scale = condense
                ? new Vector3(fromWidth, fromHeight, fromHeight)
                : new Vector3(uniform, uniform, uniform);

            float perFaceUnit = haveMetrics ? box.size.y / lineSpan : 0f;
            float inkY = haveMetrics
                ? box.min.y + ((face.baseline + face.capLine) * 0.5f - face.descentLine) * perFaceUnit
                : box.center.y;

            var ink = new Vector3(box.center.x, inkY, box.center.z);
            label.transform.localScale = scale;
            label.transform.localPosition = position - rotation * Vector3.Scale(ink, scale);
        }

        static TMP_FontAsset FindFont()
        {
            var font = TMP_Settings.defaultFontAsset;
            if (font == null)
                font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            return font;
        }

        // ── goal posts, pylons, apron ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Both gooseneck standards as one mesh, two submeshes: gold tubes then dark base padding.
        /// </summary>
        /// <remarks>
        /// The arm's geometry is the one place the spec sheet cannot be taken literally. It asks for a
        /// quarter circle of radius 6 ft sweeping from the top of the vertical standard forward to the
        /// crossbar centre, and gives that offset as 6 ft too — but a quarter circle cannot join two points
        /// at the same height. The physical gooseneck resolves it: the arc turns from vertical to horizontal,
        /// so its start is one radius <em>below</em> the crossbar. The standard therefore rises to
        /// crossbar height minus the radius and the arc lands exactly on the crossbar centre, which keeps
        /// every published figure — 6 ft of setback, 6 ft of radius, 10 ft to the bar — intact.
        /// </remarks>
        static Mesh BuildGoalPostsMesh()
        {
            var cylinder = PrimitiveMesh(PrimitiveType.Cylinder);
            var gold = new List<CombineInstance>();
            var pads = new List<CombineInstance>();

            float endLineCentre = X(FieldGeometry.AwayGoalLineYard + FieldGeometry.EndZoneDepthYards)
                                + StripeMeters * 0.5f;
            float uprightZ = Z(FieldGeometry.HashLateralYards) + PostTubeRadius;
            float crossbarY = CrossbarTopY - PostTubeRadius;
            float arcBaseY = crossbarY - StandardSetbackMeters;
            float halfCrossbar = CrossbarSpanMeters * 0.5f;

            for (int e = 0; e < 2; e++)
            {
                float end = e == 0 ? 1f : -1f;      // +1 is the away end (+X), -1 the home end
                float postX = end * endLineCentre;
                float standardX = postX + end * StandardSetbackMeters;

                // Uprights. Their inside faces are 18 ft 6 in apart, which is the same channel the two hash
                // rows leave — so the centres are the hash inner edge plus one tube radius, not a new number.
                for (int u = 0; u < 2; u++)
                {
                    float z = (u == 0 ? 1f : -1f) * uprightZ;
                    gold.Add(Tube(cylinder,
                        new Vector3(postX, CrossbarTopY, z),
                        new Vector3(postX, UprightTopY, z), PostTubeRadius));
                }

                gold.Add(Tube(cylinder,
                    new Vector3(postX, crossbarY, -halfCrossbar),
                    new Vector3(postX, crossbarY, halfCrossbar), PostTubeRadius));

                gold.Add(Tube(cylinder,
                    new Vector3(standardX, 0f, 0f),
                    new Vector3(standardX, arcBaseY, 0f), StandardRadius));

                // Quarter-circle arm, centred at the crossbar's X and the arc's base height, so angle 0 is
                // the top of the standard and angle 90 is the crossbar centre.
                for (int i = 0; i < ArmSegments; i++)
                {
                    var a = ArmPoint(postX, arcBaseY, end, i / (float)ArmSegments);
                    var b = ArmPoint(postX, arcBaseY, end, (i + 1) / (float)ArmSegments);
                    gold.Add(Tube(cylinder, a, b, StandardRadius));
                }

                pads.Add(Tube(cylinder,
                    new Vector3(standardX, 0f, 0f),
                    new Vector3(standardX, PadHeightMeters, 0f), PadRadius));
            }

            return CombineGroups("GoalPosts", gold, pads);
        }

        /// <summary>A point on the gooseneck arc, <paramref name="t"/> running 0 (standard) to 1 (crossbar).</summary>
        static Vector3 ArmPoint(float crossbarX, float arcBaseY, float end, float t)
        {
            float angle = t * Mathf.PI * 0.5f;
            return new Vector3(
                crossbarX + end * StandardSetbackMeters * Mathf.Cos(angle),
                arcBaseY + StandardSetbackMeters * Mathf.Sin(angle),
                0f);
        }

        /// <summary>All eight pylons as one mesh, one material, one draw.</summary>
        static Mesh BuildPylonsMesh()
        {
            var cube = PrimitiveMesh(PrimitiveType.Cube);
            var boxes = new List<CombineInstance>(8);

            // Pylons stand on the stripes, not beside them: each sits on the centreline of the goal-line or
            // end-line stripe and of the sideline stripe, which is why every coordinate below is a stripe
            // centre rather than a boundary.
            float goalLineCentre = X(FieldGeometry.AwayGoalLineYard) + GoalLineMeters * 0.5f;
            float endLineCentre = X(FieldGeometry.AwayGoalLineYard + FieldGeometry.EndZoneDepthYards)
                                + StripeMeters * 0.5f;
            float sidelineCentre = Z(FieldGeometry.HalfWidthYards) + StripeMeters * 0.5f;
            var scale = new Vector3(PylonSideMeters, PylonHeightMeters, PylonSideMeters);

            for (int e = 0; e < 2; e++)
            {
                float end = e == 0 ? 1f : -1f;
                for (int s = 0; s < 2; s++)
                {
                    float side = s == 0 ? 1f : -1f;
                    foreach (float x in new[] { end * goalLineCentre, end * endLineCentre })
                    {
                        boxes.Add(new CombineInstance
                        {
                            mesh = cube,
                            transform = Matrix4x4.TRS(
                                new Vector3(x, PylonHeightMeters * 0.5f, side * sidelineCentre),
                                Quaternion.identity, scale)
                        });
                    }
                }
            }

            var mesh = NewMesh("Pylons", boxes.Count * cube.vertexCount);
            mesh.CombineMeshes(boxes.ToArray(), true, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// The apron: one large dark quad below the turf. It exists so no pitch camera ever frames the void
        /// past the border, which in a stereo room reads as a hole rather than as distance.
        /// </summary>
        static Mesh BuildApronMesh()
        {
            var verts = new List<Vector3>(4);
            var uvs = new List<Vector2>(4);
            var tris = new List<int>(6);
            AddQuad(verts, uvs, tris, FromEdges(
                -ApronLengthMeters * 0.5f, ApronLengthMeters * 0.5f,
                -ApronWidthMeters * 0.5f, ApronWidthMeters * 0.5f), ApronY);

            var mesh = NewMesh("PitchApron", verts.Count);
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(UpNormals(verts.Count));
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ── mesh primitives ────────────────────────────────────────────────────────────────────────

        /// <summary>A ground quad with its normal up and a unit UV cell.</summary>
        /// <remarks>
        /// The unit cell is what makes the eventual glyph atlas a drop-in: every plain rectangle points at
        /// one solid cell, so folding the numbers into this same mesh later changes the UVs and nothing else.
        /// </remarks>
        static void AddQuad(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Rect r, float y)
        {
            int b = verts.Count;

            verts.Add(new Vector3(r.xMin, y, r.yMin));
            verts.Add(new Vector3(r.xMin, y, r.yMax));
            verts.Add(new Vector3(r.xMax, y, r.yMax));
            verts.Add(new Vector3(r.xMax, y, r.yMin));

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f));

            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
        }

        /// <summary>As <see cref="AddQuad"/>, but with the UVs in world space so the turf albedo tiles
        /// continuously across a band seam instead of stretching to fit each quad.</summary>
        static void AddTurfQuad(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Rect r, float y)
        {
            AddQuad(verts, uvs, tris, r, y);
            for (int i = uvs.Count - 4; i < uvs.Count; i++)
                uvs[i] = new Vector2(verts[i].x / TurfUvMeters, verts[i].z / TurfUvMeters);
        }

        /// <summary>A triangle wound so its normal points up, whichever order the corners arrive in.</summary>
        /// <remarks>
        /// The arrows mirror across both the centreline and midfield, so one fixed vertex order faces four
        /// different ways and half of them would be back-faces. Deriving the winding costs one cross product
        /// and removes an entire class of "the arrows on that end are invisible" bug.
        /// </remarks>
        static void AddUpTriangle(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
                                  Vector3 a, Vector3 b, Vector3 c)
        {
            if (Vector3.Cross(b - a, c - a).y < 0f)
                (b, c) = (c, b);

            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0.5f, 1f));
            uvs.Add(new Vector2(1f, 0f));
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        /// <summary>A capsule-free tube from <paramref name="a"/> to <paramref name="b"/>, as a combine
        /// instance over Unity's cylinder primitive (height 2, radius 0.5 at unit scale).</summary>
        static CombineInstance Tube(Mesh cylinder, Vector3 a, Vector3 b, float radius)
        {
            var axis = b - a;
            float length = axis.magnitude;
            var rotation = length > 1e-5f
                ? Quaternion.FromToRotation(Vector3.up, axis / length)
                : Quaternion.identity;

            return new CombineInstance
            {
                mesh = cylinder,
                transform = Matrix4x4.TRS((a + b) * 0.5f, rotation,
                    new Vector3(radius * 2f, length * 0.5f, radius * 2f))
            };
        }

        /// <summary>
        /// Merge two groups of combine instances into one mesh with one submesh each, in order.
        /// </summary>
        /// <remarks>
        /// <see cref="Mesh.CombineMeshes"/> gives either one submesh for everything or one per instance,
        /// neither of which is "one per material". Combining each group first and then combining the two
        /// results without merging is the way to land on exactly two.
        /// </remarks>
        static Mesh CombineGroups(string name, List<CombineInstance> first, List<CombineInstance> second)
        {
            var a = new Mesh { indexFormat = IndexFormat.UInt16 };
            a.CombineMeshes(first.ToArray(), true, true);

            var b = new Mesh { indexFormat = IndexFormat.UInt16 };
            b.CombineMeshes(second.ToArray(), true, true);

            var mesh = NewMesh(name, a.vertexCount + b.vertexCount);
            mesh.CombineMeshes(new[]
            {
                new CombineInstance { mesh = a, transform = Matrix4x4.identity },
                new CombineInstance { mesh = b, transform = Matrix4x4.identity }
            }, false, true);
            mesh.RecalculateBounds();

            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            return mesh;
        }

        /// <summary>
        /// A built-in primitive's shared mesh, without leaving the primitive in the scene.
        /// </summary>
        static Mesh PrimitiveMesh(PrimitiveType type)
        {
            var probe = GameObject.CreatePrimitive(type);
            var mesh = probe.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(probe);
            return mesh;
        }

        /// <summary>
        /// A named mesh pinned to 16-bit indices, with the vertex budget asserted rather than assumed.
        /// </summary>
        /// <remarks>
        /// Nothing here comes close to the limit — the whole of the paint is about 1450 vertices — but the
        /// assertion is what turns "somebody raised the tick density" from a silent 32-bit index buffer on
        /// every one of twelve camera renders into a console message at build time.
        /// </remarks>
        static Mesh NewMesh(string name, int vertexCount)
        {
            Debug.Assert(vertexCount <= 65535,
                $"[Stadium3D] {name} wants {vertexCount} vertices; IndexFormat.UInt16 tops out at 65535.");

            return new Mesh { name = name, indexFormat = IndexFormat.UInt16 };
        }

        static Vector3[] UpNormals(int count)
        {
            var normals = new Vector3[count];
            for (int i = 0; i < count; i++)
                normals[i] = Vector3.up;
            return normals;
        }

        // ── field-space helpers ────────────────────────────────────────────────────────────────────

        /// <summary>The world X of an absolute yard, straight off the coordinate authority.</summary>
        static float X(float absoluteYard) => FieldGeometry.Spot(absoluteYard, 0f).x;

        /// <summary>The world Z of a lateral offset in yards from the centre of the field.</summary>
        static float Z(float lateralYards) => FieldGeometry.Spot(0f, lateralYards).z;

        /// <summary>
        /// A rectangle in field space from its four edges. X and width run along the field, Y and height
        /// run across it — the same convention <see cref="PaintRects"/> documents, named once here.
        /// </summary>
        static Rect FromEdges(float xMin, float xMax, float zMin, float zMax) =>
            Rect.MinMaxRect(xMin, zMin, xMax, zMax);

        static int CountPaintOverlaps(Rect[] rects)
        {
            int overlaps = 0;
            for (int i = 0; i < rects.Length; i++)
                for (int j = i + 1; j < rects.Length; j++)
                    if (rects[i].Overlaps(rects[j]))
                        overlaps++;
            return overlaps;
        }

        // ── scene assembly ─────────────────────────────────────────────────────────────────────────

        static GameObject EnsureWorldRoot(string undo)
        {
            // By root object rather than GameObject.Find, which skips anything inactive and would happily
            // build a second PitchWorld next to a disabled one.
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name == WorldName)
                    return go;

            var created = new GameObject(WorldName);
            Undo.RegisterCreatedObjectUndo(created, undo);
            SetPitchLayer(created);
            return created;
        }

        static GameObject AddRenderer(Transform parent, string name, Mesh mesh, Material[] materials,
                                      ShadowCastingMode casting, string undo)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            Undo.RegisterCreatedObjectUndo(go, undo);
            go.transform.SetParent(parent, false);

            go.GetComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = casting;
            renderer.receiveShadows = true;

            SetPitchLayer(go);
            return go;
        }

        /// <summary>
        /// URP Lit, found defensively. The fallback matters because a builder that throws leaves half a field
        /// in the scene; a builder that draws the field in the wrong shader leaves something you can see and
        /// fix.
        /// </summary>
        static Material Lit(string name, Color baseColor, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Standard")
                      ?? Shader.Find("Diffuse");

            var material = new Material(shader) { name = name };

            if (material.HasProperty(BaseColorId))
                material.SetColor(BaseColorId, baseColor);
            else
                material.color = baseColor;

            if (material.HasProperty(MetallicId))
                material.SetFloat(MetallicId, 0f);

            if (material.HasProperty(SmoothnessId))
                material.SetFloat(SmoothnessId, smoothness);
            else if (material.HasProperty(GlossinessId))
                material.SetFloat(GlossinessId, smoothness);

            return material;
        }

        /// <summary>
        /// Put an object on the shared pitch layer, or leave it alone if that layer has been removed from
        /// Tags and Layers.
        /// </summary>
        /// <remarks>
        /// Degrading rather than throwing is the same call <see cref="PitchLayers.Mask"/> makes, and for the
        /// same reason: a missing layer should mean "that content is not visible on the wall it belongs to",
        /// not a failed build in front of an audience.
        /// </remarks>
        static void SetPitchLayer(GameObject go)
        {
            int layer = LayerMask.NameToLayer(PitchLayers.Shared);
            if (layer < 0)
            {
                Debug.LogWarning($"[Stadium3D] Layer \"{PitchLayers.Shared}\" is not defined in Tags and " +
                                 "Layers, so the field stays on Default and every camera in the room will " +
                                 "render it.", go);
                return;
            }

            go.layer = layer;
        }

        static Color Hex(string html) =>
            ColorUtility.TryParseHtmlString(html, out var color) ? color : Color.magenta;
    }
}
