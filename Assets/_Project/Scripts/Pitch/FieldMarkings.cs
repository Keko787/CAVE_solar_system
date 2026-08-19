using Stadium3D.Football;
using UnityEngine;

namespace Stadium3D.Pitch
{
    /// <summary>
    /// Every fixed number an NFL field is painted from, in one place. The turf builder, the paint-mesh
    /// builder, the EditMode tests and the later tactical overlays all read their geometry from here, so
    /// a yard figure is written exactly once and a correction is a one-line edit rather than a hunt.
    /// </summary>
    /// <remarks>
    /// <para><b>This class does not own the frame.</b> <see cref="FieldGeometry"/> does, and it stays the
    /// authority: +X runs toward the away goal line, +Z across the field, turf at y = 0, the 50 at the
    /// origin, and an absolute yard of 0 at the home goal line through 100 at the away one. Anything
    /// <c>FieldGeometry</c> can already express is derived from it below rather than restated - the field
    /// width, the hash offset, the end-zone depth, every station-to-X conversion. What lives here is only
    /// what a game-state class has no reason to know: stripe widths, glyph sizes, tube diameters, and the
    /// Y-stack.</para>
    ///
    /// <para><b>Metres, not yards.</b> Every value in this class is metres (or a bare count). That matches
    /// the rule in <c>FieldGeometry</c> - the sim runs in metres because Unity does - and it means the
    /// helpers below hand back coordinates a vertex writer can use directly, with no second conversion
    /// anywhere in the mesh builders. Yard figures appear only as the yard-denominated inputs to
    /// <see cref="YardLineX"/> and friends.</para>
    ///
    /// <para><b>The non-overlap rule.</b> All of the paint sits at one Y (<see cref="PaintY"/>), so nothing
    /// separates two white quads in depth and any overlap between them is a coplanar z-fight. The rule that
    /// prevents it is geometric and is encoded in the constants here, not enforced at draw time:</para>
    /// <list type="bullet">
    /// <item><description>Yard lines are clipped at the sideline <em>inner</em> edge
    /// (<see cref="SidelineInnerZ"/>) and never reach the sideline stripe, which lies entirely outboard of
    /// the field of play.</description></item>
    /// <item><description>The end lines own all four mitred corners of the boundary frame; the sidelines
    /// stop at <see cref="EndLineInnerX"/>.</description></item>
    /// <item><description>One-yard ticks are emitted only at stations that are not multiples of five -
    /// see <see cref="HasOneYardTick"/> - because a five-yard station already has a full-width line
    /// through it.</description></item>
    /// <item><description>The goal line abuts the goal-line plane and is painted entirely into the end
    /// zone, so it never crosses the sidelines and never overlaps the 5-yard grid.</description></item>
    /// </list>
    /// <para>Every one of those is a property of the constants, which is what makes a zero-overlap
    /// assertion over the whole paint mesh a real test rather than a tautology.</para>
    /// </remarks>
    public static class FieldMarkings
    {
        // ── units and scene wiring ─────────────────────────────────────────────────────────────────

        /// <summary>Metres in one foot. Exact by definition, like <see cref="FieldGeometry.MetersPerYard"/>.</summary>
        public const float MetersPerFoot = 0.3048f;

        /// <summary>Metres in one inch. Exact. Every stripe width in the rulebook is quoted in inches.</summary>
        public const float MetersPerInch = 0.0254f;

        /// <summary>
        /// The layer everything the field builder creates goes on. It exists because the field is 110 m of
        /// geometry parked well outside the CAVE room: if it lands on a layer the MiddleVR display cameras
        /// render, it appears raw inside the room. Named rather than indexed so a shifted TagManager entry
        /// fails loudly at build time instead of silently rendering the pitch on the walls.
        /// </summary>
        public const string LayerName = "PitchShared";

        // ── enclosure ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Goal line to the 50, in metres. 50 yd.</summary>
        public const float HalfLengthMeters =
            FieldGeometry.FieldLengthYards * 0.5f * FieldGeometry.MetersPerYard;

        /// <summary>Centre of the field to a sideline's inner edge, in metres. 80 ft = 24.384 m.</summary>
        public const float HalfWidthMeters =
            FieldGeometry.HalfWidthYards * FieldGeometry.MetersPerYard;

        /// <summary>Sideline to sideline, in metres. 160 ft = 48.768 m.</summary>
        public const float WidthMeters = HalfWidthMeters * 2f;

        /// <summary>One end zone's depth, in metres. 10 yd = 9.144 m.</summary>
        public const float EndZoneDepthMeters =
            FieldGeometry.EndZoneDepthYards * FieldGeometry.MetersPerYard;

        /// <summary>The 50 to an end line, in metres. 60 yd = 54.864 m. Half the total marked length.</summary>
        public const float HalfMarkedLengthMeters = HalfLengthMeters + EndZoneDepthMeters;

        // ── line widths ────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Every boundary line, yard line and tick: 4 in. The one width that dominates the paint mesh, so
        /// it is the default and the exceptions below name themselves.
        /// </summary>
        public const float LineWidth = 4f * MetersPerInch;

        /// <summary>
        /// The goal line is 8 in under NFL rules - twice everything else, and visibly so from the ISO cams.
        /// NCAA paints it at <see cref="LineWidth"/>; that is the only edit needed to switch codes.
        /// </summary>
        public const float GoalLineWidth = 8f * MetersPerInch;

        /// <summary>
        /// The solid white border ringing the whole field: 6 ft, measured outward from the outer edge of
        /// the sideline and end-line stripes. It is not decoration - it is what stops the turf reading as
        /// an unbounded green plane in a wide CAVE shot.
        /// </summary>
        public const float BorderWidth = 6f * MetersPerFoot;

        // ── boundary frame ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Half the stripe width, i.e. how far a boundary stripe's centreline sits outboard of the
        /// boundary itself. The pylons are placed on these centrelines, which is why it is named.
        /// </summary>
        public const float BoundaryStripeCenterInset = LineWidth * 0.5f;

        /// <summary>
        /// Inner edge of the sideline stripe. This <em>is</em> the boundary - the whole stripe lies
        /// outside the field of play, so a foot on the paint is out of bounds and a yard line that stops
        /// here has not touched it.
        /// </summary>
        public const float SidelineInnerZ = HalfWidthMeters;

        /// <summary>Outer edge of the sideline stripe.</summary>
        public const float SidelineOuterZ = SidelineInnerZ + LineWidth;

        /// <summary>Centreline of the sideline stripe. Pylon lateral placement.</summary>
        public const float SidelineStripeCenterZ = SidelineInnerZ + BoundaryStripeCenterInset;

        /// <summary>Inner edge of the end-line stripe - the back of the end zone.</summary>
        public const float EndLineInnerX = HalfMarkedLengthMeters;

        /// <summary>Outer edge of the end-line stripe.</summary>
        public const float EndLineOuterX = EndLineInnerX + LineWidth;

        /// <summary>
        /// Centreline of the end-line stripe. Both the goal-post plane and the end-line pylons sit on it,
        /// so it is worth having as a named value rather than two independent sums.
        /// </summary>
        public const float EndLineStripeCenterX = EndLineInnerX + BoundaryStripeCenterInset;

        /// <summary>Outer edge of the white border along X. 56.7944 m.</summary>
        public const float BorderOuterX = EndLineOuterX + BorderWidth;

        /// <summary>Outer edge of the white border along Z. 26.3144 m.</summary>
        public const float BorderOuterZ = SidelineOuterZ + BorderWidth;

        // ── yard lines and goal lines ──────────────────────────────────────────────────────────────

        /// <summary>Spacing of the painted full-width lines, in yards.</summary>
        public const float YardLineSpacingYards = 5f;

        /// <summary>
        /// Distance from the 50 to the centreline of a goal-line stripe. The stripe is painted entirely
        /// <em>into</em> the end zone - its inner edge is the goal-line plane - so that the scoring plane
        /// stays exactly at the 0 and 100 stations no matter how wide the paint is. Centring the stripe
        /// on the station instead would move the plane 4 in into the field of play, and every goal-line
        /// ruling in the sim would be wrong by that much.
        /// </summary>
        public const float GoalLineStripeCenterOffset = HalfLengthMeters + GoalLineWidth * 0.5f;

        /// <summary>Interior full-width lines: stations 5 through 95, so 19 of them.</summary>
        public const int FiveYardLineCount =
            (int)(FieldGeometry.FieldLengthYards / YardLineSpacingYards) - 1;

        /// <summary>Whole-yard stations strictly between the goal lines: 1 through 99.</summary>
        public const int OneYardStationCount = (int)FieldGeometry.FieldLengthYards - 1;

        /// <summary>
        /// Ticks in one row: the 99 whole-yard stations less the 19 that already carry a full line. 80 per
        /// row, two hash rows and two sideline rows, so 320 ticks in the paint mesh.
        /// </summary>
        public const int OneYardTickCount = OneYardStationCount - FiveYardLineCount;

        // ── hash marks and sideline ticks ──────────────────────────────────────────────────────────

        /// <summary>
        /// Inner edge of a hash row. Derived from <see cref="FieldGeometry.HashLateralYards"/>, which is
        /// where the 70 ft 9 in inbound distance already lives.
        /// </summary>
        public const float HashInnerZ =
            FieldGeometry.HashLateralYards * FieldGeometry.MetersPerYard;

        /// <summary>
        /// Length of a tick, 24 in, running <em>perpendicular</em> to the sideline. Perpendicular is the
        /// deliberate reading: it leaves 2 ft 8 in of along-field gap between consecutive ticks, which is
        /// the ruler-tick look of a real field. The parallel reading leaves a 12 in gap and renders as a
        /// near-continuous dashed lengthwise stripe, which no field has.
        /// </summary>
        public const float TickLength = 24f * MetersPerInch;

        /// <summary>Outer edge of a hash row - the ticks extend outward, toward the sideline.</summary>
        public const float HashOuterZ = HashInnerZ + TickLength;

        /// <summary>
        /// The clear channel between the two hash rows. This must come out at 18 ft 6 in - exactly the
        /// goal posts' inside width - and that coincidence is the build assertion: if
        /// <see cref="HashInnerZ"/> and <see cref="UprightInnerSpacing"/> ever disagree, one of them is
        /// wrong.
        /// </summary>
        public const float HashChannelWidth = HashInnerZ * 2f;

        /// <summary>Inner end of a sideline tick. These run inboard from the sideline's inner edge.</summary>
        public const float SidelineTickInnerZ = SidelineInnerZ - TickLength;

        // ── conversion / extra-point marks ─────────────────────────────────────────────────────────

        /// <summary>Yards in front of the goal-line plane. NFL 2, NCAA 3 - the single number that differs.</summary>
        public const float ConversionInsetYards = 2f;

        /// <summary>The mark's 24 in dimension, across the field.</summary>
        public const float ConversionMarkLength = 24f * MetersPerInch;

        // ── yard numerals ──────────────────────────────────────────────────────────────────────────

        /// <summary>Numeral cap height, 6 ft. Two yards tall, which is the fastest way to eyeball a build.</summary>
        public const float NumeralCapHeight = 6f * MetersPerFoot;

        /// <summary>Width of one digit, 4 ft.</summary>
        public const float NumeralDigitWidth = 4f * MetersPerFoot;

        /// <summary>
        /// The gap the yard line runs through, between the two digits. 12 in is authored, not a rule -
        /// the rulebook only says the line passes between them - but it is what makes a "40" read as one
        /// number instead of two.
        /// </summary>
        public const float NumeralDigitGap = 12f * MetersPerInch;

        /// <summary>Total width of a two-digit block, 9 ft.</summary>
        public const float NumeralBlockWidth = NumeralDigitWidth * 2f + NumeralDigitGap;

        /// <summary>Along-field offset from the yard line to a digit's inner edge.</summary>
        public const float NumeralInnerEdgeOffsetX = NumeralDigitGap * 0.5f;

        /// <summary>Along-field offset from the yard line to a digit's outer edge. The arrow starts here.</summary>
        public const float NumeralOuterEdgeOffsetX = NumeralInnerEdgeOffsetX + NumeralDigitWidth;

        /// <summary>Along-field offset from the yard line to a digit's centre - where a glyph gets placed.</summary>
        public const float NumeralDigitCenterOffsetX = NumeralInnerEdgeOffsetX + NumeralDigitWidth * 0.5f;

        /// <summary>
        /// Distance from the sideline to the numerals' sideline-facing edge, in yards. NFL 12, NCAA 9.
        /// <para>Flagged deliberately as the one cosmetic figure worth checking against an official
        /// diagram before final paint: sources phrase this as "bottom at 12 yd" and as "top at 12 yd",
        /// a 2 yd difference that is visible in every wide shot. It is a single constant precisely so
        /// that correction stays one line.</para>
        /// </summary>
        public const float NumeralSidelineInsetYards = 12f;

        /// <summary>
        /// Lateral position of the numeral baseline, metres. The baseline is the sideline-facing edge,
        /// because the glyphs read upright from the nearest sideline - baseline-up points at the
        /// centreline.
        /// </summary>
        public const float NumeralBaselineZ =
            (FieldGeometry.HalfWidthYards - NumeralSidelineInsetYards) * FieldGeometry.MetersPerYard;

        /// <summary>
        /// Lateral position of the numerals' cap (centre-facing) edge, metres. Derived from the baseline
        /// so the band cannot drift from the glyph height; it lands at 14 yd from the sideline, which is
        /// the figure the second half of the rule quotes.
        /// </summary>
        public const float NumeralCapZ = NumeralBaselineZ - NumeralCapHeight;

        /// <summary>Numbered stations per side: 10 through 90 by tens, so nine blocks and eighteen glyphs.</summary>
        public const int NumberedStationCount = 9;

        // ── directional arrows ─────────────────────────────────────────────────────────────────────

        /// <summary>Arrow length along the field, 36 in, apex toward the nearer goal line.</summary>
        public const float ArrowLength = 36f * MetersPerInch;

        /// <summary>Arrow base across the field, 18 in.</summary>
        public const float ArrowBaseWidth = 18f * MetersPerInch;

        /// <summary>Gap between the outer digit edge and the arrow base, 6 in.</summary>
        public const float ArrowGapFromDigit = 6f * MetersPerInch;

        /// <summary>Along-field offset from the yard line to the arrow base.</summary>
        public const float ArrowBaseOffsetX = NumeralOuterEdgeOffsetX + ArrowGapFromDigit;

        /// <summary>Along-field offset from the yard line to the arrow apex.</summary>
        public const float ArrowApexOffsetX = ArrowBaseOffsetX + ArrowLength;

        /// <summary>
        /// The arrow's centre-facing edge, flush with the numerals' cap edge. The base extends from here
        /// back toward the sideline, so the arrow tucks against the number rather than floating inboard
        /// of it.
        /// </summary>
        public const float ArrowInnerZ = NumeralCapZ;

        /// <summary>The arrow's sideline-facing edge.</summary>
        public const float ArrowOuterZ = ArrowInnerZ + ArrowBaseWidth;

        /// <summary>
        /// Arrows per side: every numbered station except the 50, which has no nearer goal line to point
        /// at. Eight a side, sixteen in total.
        /// </summary>
        public const int ArrowsPerSide = NumberedStationCount - 1;

        // ── end zones ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Wordmark cap height, 8 ft. Taller than the yard numerals, as on a real field.</summary>
        public const float WordmarkCapHeight = 8f * MetersPerFoot;

        /// <summary>
        /// Yards from the goal line to the wordmark baseline - the middle of the end zone. Fed to
        /// <see cref="YardLineX"/> as an out-of-field station, which is legal: absolute yards run to -10
        /// and +110.
        /// </summary>
        public const float WordmarkInsetYards = 5f;

        /// <summary>
        /// Widest the wordmark may be drawn: three quarters of the field width. Whichever of cap height
        /// or this bound binds first wins, so a long team name shrinks rather than running into the
        /// sidelines.
        /// </summary>
        public const float WordmarkMaxWidth = WidthMeters * 0.75f;

        /// <summary>White outline around the wordmark, 4 in. Without it a dark secondary on a dark
        /// primary vanishes at the grazing ISO-cam angle.</summary>
        public const float WordmarkOutlineWidth = LineWidth;

        // ── mow bands ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Mow band width in yards. Equal to <see cref="YardLineSpacingYards"/> by construction: the band
        /// boundaries have to coincide with the 5-yard lines or the stripes read as a second, offset grid
        /// fighting the painted one.
        /// </summary>
        public const float MowBandWidthYards = YardLineSpacingYards;

        /// <summary>Mow band width in metres, 4.572 m.</summary>
        public const float MowBandWidthMeters = MowBandWidthYards * FieldGeometry.MetersPerYard;

        /// <summary>
        /// Bands between the goal lines: 20, ten light and ten dark. Not 24 - the count that covers all
        /// 120 marked yards - because the two end zones are their own solid-fill submeshes of the turf
        /// mesh. Banding them as well would put two coplanar quads on the same 9.144 m of ground, which
        /// is exactly the fight the whole four-submesh split exists to avoid.
        /// </summary>
        public const int MowBandCount =
            (int)(FieldGeometry.FieldLengthYards / MowBandWidthYards);

        // ── pylons ─────────────────────────────────────────────────────────────────────────────────

        /// <summary>Pylon footprint, 4 in square.</summary>
        public const float PylonWidth = 4f * MetersPerInch;

        /// <summary>Pylon height, 18 in.</summary>
        public const float PylonHeight = 18f * MetersPerInch;

        /// <summary>Centre height of a pylon, for a box mesh placed by its centre.</summary>
        public const float PylonCenterY = PylonHeight * 0.5f;

        /// <summary>
        /// Lateral placement: on the sideline stripe's centreline, just out of bounds. All eight pylons
        /// share it.
        /// </summary>
        public const float PylonLateralZ = SidelineStripeCenterZ;

        /// <summary>Along-field placement of the four goal-line pylons: on the goal-line stripe's centre.</summary>
        public const float PylonGoalLineX = GoalLineStripeCenterOffset;

        /// <summary>Along-field placement of the four end-line pylons: on the end-line stripe's centre.</summary>
        public const float PylonEndLineX = EndLineStripeCenterX;

        // ── goal posts ─────────────────────────────────────────────────────────────────────────────

        /// <summary>Diameter of the crossbar and upright tubes, 4 in.</summary>
        public const float PostTubeDiameter = 4f * MetersPerInch;

        /// <summary>
        /// Top of the crossbar, 10 ft. The rule fixes the <em>top</em>, not the axis, which is why the
        /// centreline below subtracts a radius instead of the other way round.
        /// </summary>
        public const float CrossbarTopY = 10f * MetersPerFoot;

        /// <summary>Crossbar axis height, for a cylinder placed by its centre.</summary>
        public const float CrossbarCenterY = CrossbarTopY - PostTubeDiameter * 0.5f;

        /// <summary>Crossbar length outside to outside, 19 ft 2 in.</summary>
        public const float CrossbarLength = 5.842f;

        /// <summary>
        /// Inside-face separation of the uprights, 18 ft 6 in. Identical to
        /// <see cref="HashChannelWidth"/>, and that is not a coincidence: the hashes were set so a snap
        /// from either one leaves the same angle on a kick.
        /// </summary>
        public const float UprightInnerSpacing = HashChannelWidth;

        /// <summary>Upright axis lateral offset - half the inside spacing plus a tube radius.</summary>
        public const float UprightCenterZ = UprightInnerSpacing * 0.5f + PostTubeDiameter * 0.5f;

        /// <summary>Top of the uprights, 45 ft above the turf.</summary>
        public const float UprightTopY = 45f * MetersPerFoot;

        /// <summary>Upright run above the crossbar top, 35 ft.</summary>
        public const float UprightHeight = UprightTopY - CrossbarTopY;

        /// <summary>
        /// The plane the posts stand in: centred on the end-line stripe, so the crossbar sits over the
        /// back of the end zone rather than in it.
        /// </summary>
        public const float GoalPostPlaneX = EndLineStripeCenterX;

        /// <summary>Distance the gooseneck standard sits behind the post plane, 6 ft.</summary>
        public const float GooseneckOffset = 6f * MetersPerFoot;

        /// <summary>Along-field position of the gooseneck standard.</summary>
        public const float GooseneckBaseX = GoalPostPlaneX + GooseneckOffset;

        /// <summary>Diameter of the vertical standard, 8 in. Thicker than the tubes it carries.</summary>
        public const float GooseneckTubeDiameter = 8f * MetersPerInch;

        /// <summary>
        /// Radius of the quarter-circle arm sweeping from the standard to the crossbar centre. Equal to
        /// <see cref="GooseneckOffset"/> by construction - that is what makes the arm a true quarter
        /// circle, tangent-vertical at the standard and tangent-horizontal at the crossbar.
        /// </summary>
        public const float GooseneckArmRadius = GooseneckOffset;

        /// <summary>
        /// Segments in the arm sweep. Eight is enough at the distance every pitch camera views it from;
        /// the arm is 1.83 m of tube 55 m from the nearest ISO cam.
        /// </summary>
        public const int GooseneckArmSegments = 8;

        /// <summary>Radius of the padding cylinder at the base of the standard.</summary>
        public const float BasePadRadius = 0.25f;

        /// <summary>Height of the base padding, 6 ft.</summary>
        public const float BasePadHeight = 6f * MetersPerFoot;

        /// <summary>Wind ribbon width, 4 in.</summary>
        public const float RibbonWidth = 4f * MetersPerInch;

        /// <summary>Wind ribbon length, 42 in.</summary>
        public const float RibbonLength = 42f * MetersPerInch;

        // ── apron ──────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Apron extent along the field. Well past the 113 m marked rectangle, because its whole job is
        /// that no offscreen pitch camera ever frames the void or an empty skybox past the border.
        /// </summary>
        public const float ApronSizeX = 180f;

        /// <summary>Apron extent across the field.</summary>
        public const float ApronSizeZ = 120f;

        // ── the Y-stack ────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Turf and end-zone fill. The ground truth of the whole stack.
        /// </summary>
        /// <remarks>
        /// The separations in this stack are what kill z-fighting, and they are geometric on purpose. No
        /// ShaderLab <c>Offset</c>, no polygon depth bias: the sign of a depth bias flips under reversed-Z
        /// and it resolves badly against MSAA. 10 mm between opaque layers is far inside float32 reversed-Z
        /// precision over a 110 m field, and the parallax it costs from a 4 m sideline camera is sub-pixel.
        /// It also survives the phase-2 CAVE-floor miniature, where 1:31 scale takes it to 0.32 mm - still
        /// resolvable. The one thing that can eat it is a 16-bit render-texture depth buffer, which
        /// resolves about 6 mm over this field; every pitch RT must use depth 24.
        /// </remarks>
        public const float TurfY = 0f;

        /// <summary>Lines, ticks, arrows, conversion marks. Opaque, 10 mm above the turf.</summary>
        public const float PaintY = 0.010f;

        /// <summary>Yard numerals and end-zone wordmarks. Alpha-blended, no depth write.</summary>
        public const float NumberY = 0.012f;

        /// <summary>
        /// Focus rings, route trails, line-of-scrimmage and first-down markers. Above the numbers so an
        /// overlay drawn across a numeral is never half-swallowed by it.
        /// </summary>
        public const float OverlayY = 0.014f;

        /// <summary>The apron plane, below the turf so it can never fight with it at a shared edge.</summary>
        public const float ApronY = -0.020f;

        // ── colours ────────────────────────────────────────────────────────────────────────────────
        //
        // Authored in sRGB and passed straight to Material.SetColor on _BaseColor. The project is in
        // Linear colour space, and SetColor on a Color-typed property applies the gamma-to-linear
        // conversion itself, so these need no pre-conversion - and must not be given one twice.

        /// <summary>
        /// Light mow band. Roughly 1.8x the luminance of the dark band, which reads clearly on camera
        /// without tipping into a cartoon stripe.
        /// </summary>
        public static readonly Color32 TurfLight = new Color32(0x3D, 0x7A, 0x44, 0xFF);

        /// <summary>Dark mow band.</summary>
        public static readonly Color32 TurfDark = new Color32(0x2D, 0x5C, 0x33, 0xFF);

        /// <summary>
        /// Field paint. Deliberately not pure white: the shipping renderer has HDR off and no
        /// post-process data, so there is no tonemapper and a 1.0 white clips flat. This lands near 0.85
        /// and keeps its edges.
        /// </summary>
        public static readonly Color32 Paint = new Color32(0xD8, 0xD8, 0xD4, 0xFF);

        /// <summary>Goal-post safety gold.</summary>
        public static readonly Color32 GoalPostGold = new Color32(0xF2, 0xC2, 0x18, 0xFF);

        /// <summary>Goal-post base padding. Dark enough to read as padding rather than more post.</summary>
        public static readonly Color32 GoalPostPad = new Color32(0x26, 0x28, 0x2C, 0xFF);

        /// <summary>Pylon orange.</summary>
        public static readonly Color32 PylonOrange = new Color32(0xFF, 0x5A, 0x1F, 0xFF);

        /// <summary>
        /// The apron. Darker than <see cref="TurfDark"/> so it never reads as more turf, but not black -
        /// with no tonemapper a true black plane bands visibly under the linear ambient gradient.
        /// </summary>
        public static readonly Color32 Apron = new Color32(0x1E, 0x20, 0x1C, 0xFF);

        /// <summary>
        /// End-zone fill until a team's real primary is wired in. A placeholder that is obviously a
        /// placeholder beats a plausible one that quietly ships.
        /// </summary>
        public static readonly Color32 DefaultEndZoneFill = new Color32(0x1B, 0x2A, 0x4A, 0xFF);

        /// <summary>End-zone wordmark until a team's real secondary is wired in.</summary>
        public static readonly Color32 DefaultWordmark = new Color32(0xC8, 0xA2, 0x4E, 0xFF);

        // ── helpers ────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Along-field position of a station's yard line, in metres. Routed through
        /// <see cref="FieldGeometry.Spot"/> so the station-to-X mapping exists in exactly one place, and
        /// accepting a float so end-zone stations (-10 through 110) and fractional spots both work.
        /// </summary>
        public static float YardLineX(float absoluteYard) => FieldGeometry.Spot(absoluteYard, 0f).x;

        /// <summary>
        /// True at a station that carries a full-width painted line. The negative side of the non-overlap
        /// rule: a station where this is true must not also get a one-yard tick.
        /// </summary>
        public static bool IsFiveYardStation(int absoluteYard) => absoluteYard % 5 == 0;

        /// <summary>
        /// Lateral position of a numeral block's baseline, in metres, on whichever sideline it belongs to.
        /// The two sides are 180-degree rotations of each other - each block reads upright from its
        /// nearest sideline - so the sign of this value and the sign in the block's rotation have to be
        /// the same one, which is why the caller passes a side rather than a raw offset.
        /// </summary>
        public static float NumeralBaselineLateral(bool nearPositiveSideline) =>
            nearPositiveSideline ? NumeralBaselineZ : -NumeralBaselineZ;

        /// <summary>
        /// True where a one-yard tick is emitted: strictly inside the goal lines, and not at a station
        /// that already has a line through it. This is the tick half of the non-overlap rule, and it is a
        /// function rather than a loop condition so the paint builder and the overlap test can both ask
        /// the same question.
        /// </summary>
        public static bool HasOneYardTick(int absoluteYard) =>
            absoluteYard > (int)FieldGeometry.HomeGoalLineYard
            && absoluteYard < (int)FieldGeometry.AwayGoalLineYard
            && !IsFiveYardStation(absoluteYard);

        /// <summary>True at a station that gets a numeral block: 10 through 90 by tens.</summary>
        public static bool IsNumberedStation(int absoluteYard) =>
            absoluteYard % 10 == 0
            && absoluteYard >= 10
            && absoluteYard <= (int)FieldGeometry.FieldLengthYards - 10;

        /// <summary>
        /// Sign along X that a station's arrow points, toward its nearer goal line. Zero at the 50, which
        /// is how the builder skips the two blocks that get no arrow - there is no nearer goal line to
        /// point at.
        /// </summary>
        public static float ArrowDirectionX(int absoluteYard) =>
            absoluteYard < FieldGeometry.MidfieldYard ? -1f
            : absoluteYard > FieldGeometry.MidfieldYard ? 1f
            : 0f;

        /// <summary>Along-field centre of one digit of a numeral block.</summary>
        public static float NumeralDigitCenterX(float absoluteYard, bool towardPositiveX) =>
            YardLineX(absoluteYard) +
            (towardPositiveX ? NumeralDigitCenterOffsetX : -NumeralDigitCenterOffsetX);

        /// <summary>Inner edge of a sideline, signed. The clip bound for every yard line.</summary>
        public static float SidelineInnerLateral(bool positiveSide) =>
            positiveSide ? SidelineInnerZ : -SidelineInnerZ;

        /// <summary>
        /// The goal-line plane, signed. This is the mathematical plane, not the stripe: the 8 in stripe is
        /// painted entirely into the end zone so that a ball breaking this X has scored, regardless of
        /// how wide the paint is.
        /// </summary>
        public static float GoalLinePlaneX(bool awayEnd) =>
            YardLineX(awayEnd ? FieldGeometry.AwayGoalLineYard : FieldGeometry.HomeGoalLineYard);

        /// <summary>
        /// Centreline of the goal-line stripe, signed - offset from the plane by half the stripe width,
        /// into the end zone. Where a stripe quad and the goal-line pylons get placed.
        /// </summary>
        public static float GoalLineStripeCenterX(bool awayEnd) =>
            awayEnd ? GoalLineStripeCenterOffset : -GoalLineStripeCenterOffset;

        /// <summary>
        /// Absolute yard a mow band starts at. Band 0 begins at the home goal line, so a band's own index
        /// is all the builder needs to place it.
        /// </summary>
        public static float MowBandStartYard(int bandIndex) => bandIndex * MowBandWidthYards;

        /// <summary>
        /// Which submesh a band belongs to. Alternating from band 0 means the home end zone is always
        /// followed by a light band, so a rebuild never flips the pattern.
        /// </summary>
        public static bool MowBandIsLight(int bandIndex) => (bandIndex & 1) == 0;

        /// <summary>
        /// The build layer, resolved by name. Returns -1 if the TagManager entry has been removed, which
        /// a builder should treat as a hard stop rather than falling back to Default - the whole
        /// per-camera culling scheme is that one layer.
        /// </summary>
        public static int Layer() => LayerMask.NameToLayer(LayerName);
    }
}
