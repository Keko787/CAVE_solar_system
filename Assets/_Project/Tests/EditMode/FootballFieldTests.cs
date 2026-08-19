using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Stadium3D.EditorTools;
using Stadium3D.Football;
using UnityEngine;

namespace Stadium3D.Football.Tests
{
    /// <summary>
    /// The painted field, pinned. The field is drawn as geometry rather than as decals or one baked texture,
    /// and every stripe on it — 19 yard lines, 2 goal lines, the boundary frame, the border ring, 320 ticks,
    /// 2 conversion marks — sits on a single plane 10 mm above the turf. That decision buys a one-draw-call
    /// field with edges that stay hard at any zoom, and it costs exactly one invariant: <b>no two painted
    /// rectangles may overlap in area.</b> Two coplanar white quads sharing a patch of turf is the textbook
    /// z-fight, and it is the failure this suite exists to make impossible.
    /// </summary>
    /// <remarks>
    /// <para>It has to be a test rather than a look in the Scene view because the failure does not show up
    /// there. A tick painted across a yard line is a few square centimetres; it renders as a stable white
    /// patch on a still frame and only starts shimmering once a camera moves — and the cameras that move are
    /// the twelve the CAVE renders per frame, not the one an author is watching. By then the geometry is
    /// baked into a scene.</para>
    ///
    /// <para><b>Where the numbers come from.</b> Anything <see cref="FieldGeometry"/> can express is derived
    /// from it, because it is the project's coordinate authority and a second copy of 24.384 is a second
    /// place for the field to be wrong. Only the figures it deliberately does not own are stated here: stripe
    /// widths, the border width, and the Y-stack — <see cref="FieldGeometry.Spot"/> is the ground plane and
    /// always returns y = 0, so the 10 mm has to come from somewhere else.</para>
    ///
    /// <para><b>What is deliberately not pinned.</b> The 24 in dimension of a hash tick runs perpendicular to
    /// the sideline in the spec, and the spec itself flags that as the one reading worth checking against a
    /// reference photo. So the hash test pins the inbound distance and the 18 ft 6 in channel — which are rule
    /// book, not interpretation — and says nothing about which way the tick is long.</para>
    /// </remarks>
    public class FootballFieldTests
    {
        // ── tolerances and the numbers FieldGeometry does not own ──────────────────────────────────

        /// <summary>One millimetre. Every field dimension in the spec is exact to 0.1 mm, so this is slack.</summary>
        const float PositionTolerance = 1e-3f;

        /// <summary>Exact by definition, so a rule book figure can be written the way the rule book writes it.</summary>
        const float InchInMeters = 0.0254f;
        const float FootInMeters = 0.3048f;

        /// <summary>Boundary and yard stripes: 4 in.</summary>
        const float LineWidth = 4f * InchInMeters;

        /// <summary>Solid white border outside the boundary stripes: 6 ft on all four sides.</summary>
        const float BorderWidth = 6f * FootInMeters;

        /// <summary>
        /// Height of the paint above the turf, metres. This is the entire anti-z-fight budget for the field,
        /// which is why it is asserted to a tenth of a millimetre rather than "greater than zero": the pitch
        /// render textures resolve roughly 6 mm of depth over a 110 m field, so half this offset would not
        /// survive the depth buffer it is being separated in.
        /// </summary>
        const float PaintY = 0.010f;

        /// <summary>
        /// Every rectangle is shrunk by this before the overlap check, so two stripes that share an edge —
        /// which the build recipe requires all over the field — are not read as overlapping. Half a
        /// millimetre is a two-hundredth of the narrowest feature out there (a 4 in stripe), so a genuine
        /// overlap of even one millimetre still lands.
        /// </summary>
        const float OverlapInset = 0.0005f;

        /// <summary>Enough offending pairs to see the pattern; the rest are counted, not printed.</summary>
        const int MaxReportedOverlaps = 12;

        /// <summary>Midfield to an end line, metres. 60 yards: 50 to the goal line plus the end zone.</summary>
        static readonly float HalfLength = FieldGeometry.Spot(
            FieldGeometry.AwayGoalLineYard + FieldGeometry.EndZoneDepthYards, 0f).x;

        /// <summary>Centre of the field to a sideline inner edge, metres. The inner edge is the boundary.</summary>
        static readonly float HalfWidth = FieldGeometry.YardsToMeters(FieldGeometry.HalfWidthYards);

        /// <summary>Centre of the field to the goal-line plane, metres.</summary>
        static readonly float GoalLinePlane = FieldGeometry.Spot(FieldGeometry.AwayGoalLineYard, 0f).x;

        /// <summary>Centre of the field to a hash row's inner edge, metres.</summary>
        static readonly float HashInner = FieldGeometry.YardsToMeters(FieldGeometry.HashLateralYards);

        /// <summary>
        /// Half extents of the outer border rectangle. Measured outward from the <em>outer</em> edge of the
        /// boundary stripe, because that is where the 6 ft border starts — the stripes themselves lie wholly
        /// outside the field of play. The spec quotes the outer rectangle both ways, differing by one stripe
        /// width; the containment tests use the larger reading, which is the claim both readings satisfy.
        /// </summary>
        static readonly float OuterHalfX = HalfLength + LineWidth + BorderWidth;
        static readonly float OuterHalfZ = HalfWidth + LineWidth + BorderWidth;

        Mesh m_Turf;
        Mesh m_Paint;

        /// <summary>
        /// Meshes built by a test are unowned by any scene object, so nothing else will ever collect them.
        /// A suite run that leaks one per test leaks them for the rest of the editor session.
        /// </summary>
        [TearDown]
        public void DestroyBuiltMeshes()
        {
            if (m_Turf != null) Object.DestroyImmediate(m_Turf);
            if (m_Paint != null) Object.DestroyImmediate(m_Paint);
            m_Turf = null;
            m_Paint = null;
        }

        Mesh Turf()
        {
            if (m_Turf == null) m_Turf = FootballFieldBuilder.BuildTurfMesh();
            Assert.That(m_Turf, Is.Not.Null, "BuildTurfMesh() returned null");
            return m_Turf;
        }

        Mesh Paint()
        {
            if (m_Paint == null) m_Paint = FootballFieldBuilder.BuildPaintMesh();
            Assert.That(m_Paint, Is.Not.Null, "BuildPaintMesh() returned null");
            return m_Paint;
        }

        // ── turf ───────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void TurfMesh_HasTheFourSubmeshesTheMowPatternAndTheEndZonesNeed()
        {
            var turf = Turf();

            Assert.That(turf.subMeshCount, Is.EqualTo(4),
                "the turf is one mesh with four submeshes — light mow bands, dark mow bands, +X end zone " +
                "fill, -X end zone fill. Four separate meshes would mean four culling volumes and four " +
                "transforms for geometry that never moves relative to itself, and the end zone fills would " +
                "no longer be guaranteed to abut the bands rather than sit on top of them.");

            for (int sub = 0; sub < turf.subMeshCount; sub++)
                Assert.That(turf.GetIndexCount(sub), Is.GreaterThan(0u),
                    $"turf submesh {sub} is empty. Four submeshes were declared and only some were filled, " +
                    "which renders as an end zone drawn in the mow-band material — a mistake that looks " +
                    "like a colour choice rather than a build error.");

            Assert.That(turf.indexFormat, Is.EqualTo(UnityEngine.Rendering.IndexFormat.UInt16),
                "the whole turf is 26 quads. UInt32 doubles the index buffer to address vertices that do " +
                "not exist, on a mesh that is resident for the entire session on every pitch camera.");
        }

        [Test]
        public void TurfMesh_LiesFlatOnTheTurfPlaneAndSpansExactlyTheMarkedField()
        {
            var turf = Turf();
            var verts = turf.vertices;

            Assert.That(verts.Length, Is.GreaterThan(0), "the turf mesh has no vertices");

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            for (int i = 0; i < verts.Length; i++)
            {
                // The turf is the datum of the Y-stack. Every other layer is placed as an offset from it, so
                // a turf vertex off the plane does not tilt the grass — it eats the paint's 10 mm somewhere.
                Assert.That(verts[i].y, Is.EqualTo(0f).Within(PositionTolerance),
                    $"turf vertex {i} sits at y = {verts[i].y:0.0000} m rather than on the ground plane");

                minX = Mathf.Min(minX, verts[i].x); maxX = Mathf.Max(maxX, verts[i].x);
                minZ = Mathf.Min(minZ, verts[i].z); maxZ = Mathf.Max(maxZ, verts[i].z);
            }

            // Placement first: an off-centre field round-trips through every span check below and still puts
            // the 50 somewhere other than the origin, which silently moves every ball spot in the sim.
            Assert.That(minX, Is.EqualTo(-HalfLength).Within(PositionTolerance),
                "the -X end line is 60 yards from the origin");
            Assert.That(maxX, Is.EqualTo(HalfLength).Within(PositionTolerance),
                "the +X end line is 60 yards from the origin");
            Assert.That(minZ, Is.EqualTo(-HalfWidth).Within(PositionTolerance),
                "the -Z sideline inner edge is half the field width from the origin");
            Assert.That(maxZ, Is.EqualTo(HalfWidth).Within(PositionTolerance),
                "the +Z sideline inner edge is half the field width from the origin");

            // Then size, against the real-world figures rather than against the constants that produce them.
            Assert.That(maxX - minX, Is.EqualTo(109.728f).Within(PositionTolerance),
                "end line to end line is 120 yards");
            Assert.That(maxZ - minZ, Is.EqualTo(48.768f).Within(PositionTolerance),
                "sideline to sideline is 160 ft");

            // Culling reads mesh.bounds, not the vertices. A builder that writes vertices after setting the
            // triangles and never calls RecalculateBounds leaves a volume that is right in the Scene view
            // and wrong the moment a camera is far enough away to cull against it.
            Assert.That(turf.bounds.size.x, Is.EqualTo(maxX - minX).Within(PositionTolerance),
                "mesh.bounds does not match the vertices — RecalculateBounds was not called");
            Assert.That(turf.bounds.size.z, Is.EqualTo(maxZ - minZ).Within(PositionTolerance),
                "mesh.bounds does not match the vertices — RecalculateBounds was not called");
        }

        // ── paint ──────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void PaintMesh_IsOneSubmeshSittingTenMillimetresAboveTheTurf()
        {
            var paint = Paint();

            Assert.That(paint.subMeshCount, Is.EqualTo(1),
                "all of the paint is one white material, so it is one submesh and one draw call. Splitting " +
                "it by feature multiplies that by twelve camera renders per frame for no visual difference.");

            Assert.That(paint.indexFormat, Is.EqualTo(UnityEngine.Rendering.IndexFormat.UInt16),
                "the paint is roughly 1420 vertices, an order of magnitude under the 16-bit limit");

            var verts = paint.vertices;
            Assert.That(verts.Length, Is.GreaterThan(0), "the paint mesh has no vertices");

            for (int i = 0; i < verts.Length; i++)
            {
                // The offset has to live in the vertices rather than on the transform. A transform-only
                // offset is lost by anything that bakes or combines the mesh — the phase-2 floor miniature
                // rebuilds this geometry at 1:31 — and losing it flattens every stripe onto the turf at once.
                Assert.That(verts[i].y, Is.EqualTo(PaintY).Within(1e-4f),
                    $"paint vertex {i} sits at y = {verts[i].y:0.0000} m rather than the paint plane at " +
                    $"{PaintY:0.000} m. The separation from the turf is what stops the field z-fighting, " +
                    "and it must be in the mesh, not in the transform above it.");
            }
        }

        [Test]
        public void PaintMesh_KeepsEveryVertexInsideTheOuterBorderRectangle()
        {
            var paint = Paint();
            var verts = paint.vertices;

            for (int i = 0; i < verts.Length; i++)
            {
                // Nothing painted belongs outside the border ring. Paint that escapes it lands on the apron,
                // where there is no turf underneath and the stripe reads as a floating white line.
                Assert.That(Mathf.Abs(verts[i].x), Is.LessThanOrEqualTo(OuterHalfX + PositionTolerance),
                    $"paint vertex {i} is {Mathf.Abs(verts[i].x) - OuterHalfX:0.0000} m beyond the end of " +
                    "the border ring");
                Assert.That(Mathf.Abs(verts[i].z), Is.LessThanOrEqualTo(OuterHalfZ + PositionTolerance),
                    $"paint vertex {i} is {Mathf.Abs(verts[i].z) - OuterHalfZ:0.0000} m beyond the side of " +
                    "the border ring");
            }
        }

        // ── the headline: no paint sits on top of other paint ──────────────────────────────────────

        [Test]
        public void PaintRects_ContainNoTwoRectanglesThatOverlapInArea()
        {
            // This is the invariant the whole paint design rests on. Everything is at one Y, so overlap is
            // not a cosmetic double-coverage — it is two quads at identical depth, and which one wins is
            // decided per pixel per frame by float noise. The recipe's four rules exist to make it
            // impossible by construction: yard lines clip at the sideline inner edge, the end lines own the
            // four mitred corners, ticks are emitted only at stations that are not multiples of five, and
            // the goal line abuts the sidelines without crossing them. This test is what makes those four
            // rules a fact rather than an intention.
            var rects = PaintRectangles();

            Assert.That(rects.Count, Is.GreaterThanOrEqualTo(300),
                $"PaintRects() returned {rects.Count} rectangles. The field's inventory is roughly 351 — 19 " +
                "yard lines, 2 goal lines, a 4-quad boundary frame, a 4-quad border ring, 320 ticks and 2 " +
                "conversion marks. A much shorter list means the overlap check below is passing because it " +
                "has almost nothing to check.");

            var report = new StringBuilder();
            int offenders = 0;

            for (int i = 0; i < rects.Count; i++)
            {
                Rect a = Deflate(rects[i], OverlapInset);

                for (int j = i + 1; j < rects.Count; j++)
                {
                    Rect b = Deflate(rects[j], OverlapInset);
                    if (!a.Overlaps(b))
                        continue;

                    offenders++;
                    if (offenders > MaxReportedOverlaps)
                        continue;

                    Rect ra = rects[i], rb = rects[j];
                    float w = Mathf.Min(ra.xMax, rb.xMax) - Mathf.Max(ra.xMin, rb.xMin);
                    float h = Mathf.Min(ra.yMax, rb.yMax) - Mathf.Max(ra.yMin, rb.yMin);

                    report.AppendLine($"  {Describe(i, ra)}");
                    report.AppendLine($"  overlaps {Describe(j, rb)}");
                    report.AppendLine($"  by {w * 1000f:0.0} x {h * 1000f:0.0} mm = {w * h * 1e4f:0.00} cm^2");
                    report.AppendLine();
                }
            }

            if (offenders > MaxReportedOverlaps)
                report.AppendLine($"  ... and {offenders - MaxReportedOverlaps} further overlapping pair(s).");

            Assert.That(offenders, Is.Zero,
                $"{offenders} pair(s) of paint rectangles overlap in area. Each one is a patch of field " +
                $"where two white quads sit at exactly y = {PaintY:0.000} m, which shimmers under any camera " +
                "that moves and cannot be fixed with a depth bias.\n" + report);
        }

        [Test]
        public void PaintRects_StayInsideTheOuterBorderRectangle()
        {
            var rects = PaintRectangles();

            for (int i = 0; i < rects.Count; i++)
            {
                Rect r = rects[i];

                // Rect.x is the along-field metre and Rect.y is the lateral one. If those are ever swapped
                // the field is 52 m long and 113 m wide, and this is the assertion that says so first.
                Assert.That(Mathf.Max(Mathf.Abs(r.xMin), Mathf.Abs(r.xMax)),
                    Is.LessThanOrEqualTo(OuterHalfX + PositionTolerance),
                    $"{Describe(i, r)} runs past the end of the border ring at |x| = {OuterHalfX:0.0000} m");

                Assert.That(Mathf.Max(Mathf.Abs(r.yMin), Mathf.Abs(r.yMax)),
                    Is.LessThanOrEqualTo(OuterHalfZ + PositionTolerance),
                    $"{Describe(i, r)} runs past the side of the border ring at |z| = {OuterHalfZ:0.0000} m");
            }
        }

        // ── the rule book figures ──────────────────────────────────────────────────────────────────

        [Test]
        public void HashMarks_SitAtTheInboundDistanceAndLeaveTheEighteenFootSixChannel()
        {
            var rects = PaintRectangles();

            var plus = new List<Rect>();
            var minus = new List<Rect>();

            foreach (Rect r in rects)
            {
                // The hash rows are the only paint living wholly on one side of the centre line within six
                // metres of it. The conversion mark straddles z = 0 and so fails "wholly on one side"; the
                // sideline ticks and the numbers' arrows are all far outside six metres.
                if (r.width >= 1f) continue;
                if (r.yMin > 0f && r.yMax < 6f) plus.Add(r);
                else if (r.yMax < 0f && r.yMin > -6f) minus.Add(r);
            }

            Assert.That(plus.Count, Is.EqualTo(80),
                "a hash row is one tick at every station from 1 to 99 except the multiples of 5, which the " +
                "full-width yard lines already cover — 80 ticks. A count of 99 means the skip is missing, " +
                "and every one of those 19 extra ticks is painted across a yard line.");
            Assert.That(minus.Count, Is.EqualTo(80), "the two hash rows are mirror images");

            // The inbound distance itself, derived from FieldGeometry rather than restated. NFL hashes are
            // 70 ft 9 in from each sideline, which is what leaves 18 ft 6 in between the rows.
            foreach (Rect r in plus)
                Assert.That(r.yMin, Is.EqualTo(HashInner).Within(PositionTolerance),
                    $"the +Z hash tick centred on absolute yard {YardOf(r):0.0} starts at z = {r.yMin:0.0000} m " +
                    $"rather than the inbound line at {HashInner:0.0000} m");

            foreach (Rect r in minus)
                Assert.That(r.yMax, Is.EqualTo(-HashInner).Within(PositionTolerance),
                    $"the -Z hash tick centred on absolute yard {YardOf(r):0.0} starts at z = {r.yMax:0.0000} m " +
                    $"rather than the inbound line at {-HashInner:0.0000} m");

            // The channel is the build assertion the spec asks for: it is exactly the goal-post width, so a
            // field whose hashes are wrong is a field where every field goal is aimed at the wrong gap.
            float channel = MinOfYMin(plus) - MaxOfYMax(minus);
            Assert.That(channel, Is.EqualTo(18.5f * FootInMeters).Within(PositionTolerance),
                "the clear channel between the hash rows is 18 ft 6 in, the same as the distance between " +
                "the inside faces of the uprights");
            Assert.That(channel, Is.EqualTo(FieldGeometry.YardsToMeters(FieldGeometry.HashLateralYards * 2f))
                .Within(PositionTolerance),
                "the painted channel and FieldGeometry.HashLateralYards must describe the same field — the " +
                "sim spots the ball on the hash using the constant and the viewer checks it against paint");

            // And nothing in either row may be painted over a yard line, which is the other half of the
            // reason the rows skip the multiples of 5.
            for (int s = 5; s <= 95; s += 5)
            {
                float x = FieldGeometry.Spot(s, 0f).x;
                foreach (Rect r in plus)
                    Assert.That(r.xMin < x && r.xMax > x, Is.False,
                        $"a +Z hash tick covers the {s}-yard line at x = {x:0.0000} m");
                foreach (Rect r in minus)
                    Assert.That(r.xMin < x && r.xMax > x, Is.False,
                        $"a -Z hash tick covers the {s}-yard line at x = {x:0.0000} m");
            }
        }

        [Test]
        public void YardLines_RunAtEveryFiveYardStationAndStopAtTheSidelineInnerEdge()
        {
            var lines = FullWidthLinesInsideTheFieldOfPlay();

            Assert.That(lines.Count, Is.EqualTo(19),
                "there are 19 interior yard lines, at the stations 5 through 95. A different count usually " +
                "means a line was split at the hash rows, which turns one clean stripe into two that have " +
                "to be checked for a seam.");

            for (int s = 5; s <= 95; s += 5)
            {
                float x = FieldGeometry.Spot(s, 0f).x;
                int found = 0;

                foreach (Rect r in lines)
                    if (Mathf.Abs(r.center.x - x) <= PositionTolerance)
                        found++;

                Assert.That(found, Is.EqualTo(1),
                    $"expected exactly one yard line centred on absolute yard {s} (x = {x:0.0000} m), found {found}. " +
                    "The stripe is centred on the station because every game-state figure in the sim — the " +
                    "line of scrimmage, the ball spot, the first-down marker — is measured to the station, " +
                    "not to an edge of the paint.");
            }

            foreach (Rect r in lines)
            {
                // Both directions matter and both are visible. Crossing the inner edge overlaps the sideline
                // stripe, which is the z-fight; stopping short of it leaves a white gap at the boundary that
                // reads on a wide shot as a missing line.
                Assert.That(r.yMin, Is.EqualTo(-HalfWidth).Within(PositionTolerance),
                    $"the yard line at absolute yard {YardOf(r):0.0} does not meet the -Z sideline inner edge exactly");
                Assert.That(r.yMax, Is.EqualTo(HalfWidth).Within(PositionTolerance),
                    $"the yard line at absolute yard {YardOf(r):0.0} does not meet the +Z sideline inner edge exactly");
            }
        }

        [Test]
        public void GoalLines_ArePaintedIntoTheEndZoneSoTheScoringPlaneIsTheStation()
        {
            var rects = PaintRectangles();
            var goalLines = new List<Rect>();

            foreach (Rect r in rects)
            {
                if (r.height < 2f * HalfWidth - 1f) continue;                 // a full-width stripe
                float centre = Mathf.Abs(r.center.x);
                if (centre > GoalLinePlane - 1f && centre < GoalLinePlane + 1f)
                    goalLines.Add(r);
            }

            Assert.That(goalLines.Count, Is.EqualTo(2), "one goal line at each end");

            foreach (Rect r in goalLines)
            {
                // The whole stripe goes into the end zone, so the edge of the paint facing the field IS the
                // scoring plane. Centring it on the station instead would put four inches of the goal line
                // in the field of play, and a ball spotted on the paint would already have scored.
                float inner = r.center.x > 0f ? r.xMin : r.xMax;
                Assert.That(Mathf.Abs(inner), Is.EqualTo(GoalLinePlane).Within(PositionTolerance),
                    $"the goal line near x = {r.center.x:0.0000} m has its field-facing edge at " +
                    $"|x| = {Mathf.Abs(inner):0.0000} m rather than on the goal-line plane at {GoalLinePlane:0.0000} m");

                float outer = r.center.x > 0f ? r.xMax : r.xMin;
                Assert.That(Mathf.Abs(outer), Is.GreaterThan(GoalLinePlane),
                    "the goal line is painted into the end zone, away from the field of play");

                Assert.That(r.yMin, Is.EqualTo(-HalfWidth).Within(PositionTolerance),
                    "the goal line abuts the -Z sideline and must not cross it");
                Assert.That(r.yMax, Is.EqualTo(HalfWidth).Within(PositionTolerance),
                    "the goal line abuts the +Z sideline and must not cross it");
            }
        }

        // ── the layer the whole field depends on ───────────────────────────────────────────────────

        [Test]
        public void PitchSharedLayer_ExistsSoTheFieldCanBeHiddenFromTheRoomCameras()
        {
            // LayerMask.NameToLayer returns -1 for a layer that is not defined, and assigning -1 leaves an
            // object on Default without complaining. That failure mode is 110 m of turf rendering inside a
            // 3.5 m room, and it appears only in the CAVE.
            Assert.That(LayerMask.NameToLayer("PitchShared"), Is.GreaterThanOrEqualTo(0),
                "the PitchShared layer (index 14) is missing from TagManager.asset, so everything the field " +
                "builder creates would land on Default and be rendered by the CAVE display cameras");
        }

        // ── helpers ────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Every painted rectangle, normalised. Normalising is not tidiness: a rectangle authored from a
        /// corner with a negative extent makes <see cref="Rect.Overlaps"/> quietly return false for every
        /// comparison, which would turn the headline test into one that always passes.
        /// </summary>
        static List<Rect> PaintRectangles()
        {
            var raw = FootballFieldBuilder.PaintRects();
            Assert.That(raw, Is.Not.Null, "PaintRects() returned null");

            var rects = new List<Rect>();
            foreach (Rect r in raw)
                rects.Add(Normalized(r));

            return rects;
        }

        /// <summary>
        /// The stripes that span the field width and lie between the goal lines: the 19 yard lines. The end
        /// lines are also full width but sit outside the goal lines, and they run wider still because they
        /// own the mitred corners of the boundary frame.
        /// </summary>
        static List<Rect> FullWidthLinesInsideTheFieldOfPlay()
        {
            var lines = new List<Rect>();

            foreach (Rect r in PaintRectangles())
            {
                if (r.height < 2f * HalfWidth - 1f) continue;
                if (Mathf.Abs(r.center.x) >= GoalLinePlane - 0.5f) continue;
                lines.Add(r);
            }

            return lines;
        }

        static Rect Normalized(Rect r) => Rect.MinMaxRect(
            Mathf.Min(r.xMin, r.xMax), Mathf.Min(r.yMin, r.yMax),
            Mathf.Max(r.xMin, r.xMax), Mathf.Max(r.yMin, r.yMax));

        /// <summary>Shrink a rectangle on all four sides, so shapes that merely share an edge do not count.</summary>
        static Rect Deflate(Rect r, float inset) => new Rect(
            r.xMin + inset, r.yMin + inset,
            Mathf.Max(0f, r.width - 2f * inset), Mathf.Max(0f, r.height - 2f * inset));

        /// <summary>The absolute yard a rectangle is centred on, so a failure names a place on the field.</summary>
        static float YardOf(Rect r) => FieldGeometry.YardAt(new Vector3(r.center.x, 0f, 0f));

        static string Describe(int index, Rect r) =>
            $"rect #{index} x[{r.xMin:0.0000}..{r.xMax:0.0000}] z[{r.yMin:0.0000}..{r.yMax:0.0000}] " +
            $"(absolute yard {YardOf(r):0.0}, {FieldGeometry.MetersToYards(r.center.y):0.0} yd off centre)";

        static float MinOfYMin(List<Rect> rects)
        {
            float min = float.MaxValue;
            foreach (Rect r in rects) min = Mathf.Min(min, r.yMin);
            return min;
        }

        static float MaxOfYMax(List<Rect> rects)
        {
            float max = float.MinValue;
            foreach (Rect r in rects) max = Mathf.Max(max, r.yMax);
            return max;
        }
    }
}
