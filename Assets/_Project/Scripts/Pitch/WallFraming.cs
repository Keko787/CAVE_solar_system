using UnityEngine;

namespace Stadium3D.Pitch
{
    /// <summary>
    /// The arithmetic that decides what each wall camera frames. Pure and unit-tested, because these are the
    /// numbers that make the difference between "a picture of a football game" and the two things this room
    /// can do that a television cannot: a player at <b>true scale</b> on the side walls, and a tactical view
    /// on the floor at a scale where formations actually separate.
    /// </summary>
    /// <remarks>
    /// Every method here takes the <em>physical</em> size of the surface the image lands on. That is the whole
    /// trick — the CAVE's walls are known, fixed, measured objects, so "how big will this look to the person
    /// standing in the room" is a solvable question rather than something to eyeball in the editor.
    /// </remarks>
    public static class WallFraming
    {
        /// <summary>Physical height of every CAVE surface, metres. All four are the same.</summary>
        public const float WallHeightMeters = 2.2225f;

        /// <summary>Physical width of the front and floor surfaces, metres.</summary>
        public const float WideWallWidthMeters = 3.556f;

        /// <summary>Physical width of the two side surfaces, metres.</summary>
        public const float SideWallWidthMeters = 2.3876f;

        /// <summary>A standing player, metres. The subject the side walls are solved around.</summary>
        public const float PlayerHeightMeters = 1.88f;

        /// <summary>
        /// The floor's tactical scale: 1 metre on the floor is this many metres of field.
        /// </summary>
        /// <remarks>
        /// 22 is not arbitrary. At 1:22 the wall's 2.2225 m height carries 48.9 m of field — which is the
        /// full 48.768 m width of a gridiron, with nothing to spare. Any larger scale crops the sidelines;
        /// any smaller and the whole field fits but bodies collapse into an unreadable smear. Fitting the
        /// entire 120-yard length instead would force roughly 1:31, where seven linemen span 3.7 cm and a
        /// jersey number subtends 0.2 degrees — about two and a half times too small to read. So the floor
        /// shows full width and a sliding window along the length, and never carries identity.
        /// </remarks>
        public const float FloorScale = 22f;

        /// <summary>
        /// Vertical field of view, in degrees, that renders a subject of <paramref name="subjectHeight"/> at
        /// true physical scale on a wall of <paramref name="wallHeight"/>, from <paramref name="distance"/>.
        /// </summary>
        /// <remarks>
        /// The derivation is short and worth keeping visible. The render target maps onto the whole wall, so a
        /// subject of height <c>h</c> standing where the frustum is <c>H</c> tall renders on the wall at
        /// <c>h · wallHeight / H</c>. Setting that equal to <c>h</c> — life-size — gives <c>H = wallHeight</c>,
        /// with the subject's own height cancelling out entirely.
        ///
        /// <para>So the rule is simply: <b>frame exactly one wall-height of world at the subject.</b> Which
        /// means the side walls are not "a camera pointed at a player" but a camera whose entire job is to
        /// keep 2.2225 m of world in frame, wherever the player happens to be.</para>
        ///
        /// <para><paramref name="subjectHeight"/> is still taken, because
        /// <paramref name="framingScale"/> is expressed relative to it — a scale of 1 is life-size, 2 shows
        /// twice as much world and renders the player at half scale, which is the knob to reach for if the
        /// tight shot proves too claustrophobic on hardware.</para>
        /// </remarks>
        public static float LifeSizeVerticalFov(float distance, float wallHeight = WallHeightMeters,
                                                float subjectHeight = PlayerHeightMeters, float framingScale = 1f)
        {
            distance = Mathf.Max(0.01f, distance);
            float visibleHeight = Mathf.Max(0.01f, wallHeight * Mathf.Max(0.01f, framingScale));
            return 2f * Mathf.Atan2(visibleHeight * 0.5f, distance) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// How tall a subject of <paramref name="subjectHeight"/> renders on the wall, in metres, for a given
        /// camera. The inverse of <see cref="LifeSizeVerticalFov"/>, so a test can assert the round trip
        /// rather than trusting one formula against itself.
        /// </summary>
        public static float RenderedHeightOnWall(float distance, float verticalFovDeg,
                                                 float wallHeight = WallHeightMeters,
                                                 float subjectHeight = PlayerHeightMeters)
        {
            float visibleHeight = 2f * Mathf.Max(0.01f, distance) * Mathf.Tan(verticalFovDeg * 0.5f * Mathf.Deg2Rad);
            return subjectHeight * wallHeight / Mathf.Max(1e-4f, visibleHeight);
        }

        /// <summary>
        /// The angle a rendered subject subtends at the viewer's eye, in degrees — the number that actually
        /// decides whether something is legible, since it folds in both how big the image is and how far the
        /// person is standing from the wall.
        /// </summary>
        public static float AngularSizeDegrees(float renderedHeightMeters, float viewingDistanceMeters) =>
            2f * Mathf.Atan2(renderedHeightMeters * 0.5f, Mathf.Max(0.01f, viewingDistanceMeters)) * Mathf.Rad2Deg;

        /// <summary>
        /// Orthographic size — half the vertical extent — for a top-down camera showing the field at 1 to
        /// <paramref name="scale"/> on a surface <paramref name="wallHeight"/> tall.
        /// </summary>
        public static float FloorOrthographicSize(float scale = FloorScale, float wallHeight = WallHeightMeters) =>
            wallHeight * Mathf.Max(0.01f, scale) * 0.5f;

        /// <summary>
        /// Metres of field visible across a surface, given its physical size and the scale.
        /// </summary>
        public static Vector2 FloorCoverageMeters(float scale = FloorScale,
                                                  float wallWidth = WideWallWidthMeters,
                                                  float wallHeight = WallHeightMeters) =>
            new Vector2(wallWidth * scale, wallHeight * scale);

        /// <summary>
        /// Where the floor camera should centre, so the window slides with the ball but never runs off the
        /// end of the world.
        /// </summary>
        /// <remarks>
        /// Clamped so the visible window always stays inside the marked field plus its end zones. Without
        /// this the view drifts past the end line on a deep play and the operator sees a band of empty apron
        /// exactly when the football is most interesting.
        /// </remarks>
        public static float FloorWindowCenterX(float ballX, float coverageAlongX, float halfFieldExtentX)
        {
            float halfWindow = coverageAlongX * 0.5f;
            if (halfWindow >= halfFieldExtentX)
                return 0f;                                   // the whole field fits; stay centred
            return Mathf.Clamp(ballX, -halfFieldExtentX + halfWindow, halfFieldExtentX - halfWindow);
        }

        /// <summary>
        /// Render-texture dimensions for a surface, matched to its projector viewport so the panel samples
        /// one-to-one with no resample.
        /// </summary>
        /// <remarks>
        /// The <c>.vrx</c> gives both the physical size and the viewport rectangle, and they already agree to
        /// four decimal places — so matching the viewport also matches the physical aspect. Deriving the
        /// resolution from the aspect instead would invite the rounding that produced the inherited feed
        /// window's 1.69x horizontal stretch.
        /// </remarks>
        public static Vector2Int RenderTextureSize(bool wideSurface, float resolutionScale = 1f)
        {
            resolutionScale = Mathf.Clamp(resolutionScale, 0.25f, 2f);
            int w = wideSurface ? 2560 : 1719;
            int h = 1600;
            return new Vector2Int(
                Mathf.Max(16, Mathf.RoundToInt(w * resolutionScale)),
                Mathf.Max(16, Mathf.RoundToInt(h * resolutionScale)));
        }

        /// <summary>Aspect of a surface from its physical dimensions.</summary>
        public static float Aspect(float wallWidth, float wallHeight) =>
            wallWidth / Mathf.Max(1e-4f, wallHeight);
    }
}
