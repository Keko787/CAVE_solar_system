using UnityEngine;

namespace Stadium3D.Pitch
{
    /// <summary>
    /// Where a stat readout sits on a surface, in fractions of that surface rather than metres.
    /// </summary>
    /// <remarks>
    /// <b>The stats are drawn into the video, not in front of it.</b> §3.4 asks for the stat panels to sit at
    /// zero disparity, coplanar with the picture, because text held 2 cm proud of a zero-disparity background
    /// at 1–2 m is a known stereo comfort problem. The obvious implementation — a canvas in the room, parked
    /// at the wall plane — makes coplanarity a number somebody has to keep correct, and buys z-fighting with
    /// the panel quad as well. Composing the readout into the render texture instead makes it coplanar
    /// <em>by construction</em>: it is the same pixels as the picture, so there is no second plane to
    /// misalign, and no way for a later edit to introduce one.
    ///
    /// <para>So a HUD element is a child of its pitch camera, laid out in viewport fractions and converted
    /// here to a local offset on a plane a couple of metres in front. For a perspective camera that plane's
    /// distance cancels out — position and size both scale with it — so the on-screen result is the same at
    /// any distance, and the distance is chosen only to sit clear of the subjects.</para>
    ///
    /// <para><b>Fractions, not metres, and never pixels.</b> The four surfaces are three different pixel
    /// sizes and two different aspect ratios. §4.3 records what happens when a layout carries an implicit
    /// ratio: the source project's feed window put a 1.333 image in a 2.256 rect and shipped a 1.69×
    /// stretch that looked almost right.</para>
    /// </remarks>
    public static class HudLayout
    {
        /// <summary>Front wall width, metres. From the <c>.vrx</c> screen declaration.</summary>
        public const float FrontWallWidthMeters = 3.556f;

        /// <summary>
        /// How much of the front wall a seated viewer can actually read without moving their eyes.
        /// </summary>
        /// <remarks>
        /// The front wall subtends 116° horizontally at 1.11 m, and comfortable single-glance vision is
        /// about 60° — the central 1.28 m. <b>64% of that wall is peripheral</b>, so a readout in the corners
        /// is decoration. This is the number §8 is built on, and the one worth asserting in a test rather
        /// than trusting a designer's eye against a 2560-pixel-wide preview on a desk monitor.
        /// </remarks>
        public const float ReadableWidthMeters = 1.28f;

        /// <summary>The readable band as a fraction of the surface — about 0.36.</summary>
        public static float ReadableFraction => ReadableWidthMeters / FrontWallWidthMeters;

        /// <summary>Left edge of the readable band, in viewport x.</summary>
        public static float ReadableMin => 0.5f - ReadableFraction * 0.5f;

        /// <summary>Right edge of the readable band, in viewport x.</summary>
        public static float ReadableMax => 0.5f + ReadableFraction * 0.5f;

        /// <summary>Is this viewport x inside the band a viewer can read at a glance?</summary>
        public static bool IsReadable(float viewportX) => viewportX >= ReadableMin && viewportX <= ReadableMax;

        /// <summary>Does this whole element fall inside the readable band?</summary>
        public static bool IsReadable(float centreX, float widthFraction) =>
            IsReadable(centreX - widthFraction * 0.5f) && IsReadable(centreX + widthFraction * 0.5f);

        /// <summary>
        /// Half-width and half-height of the camera's frame, in metres, on a plane <paramref name="distance"/>
        /// in front of it.
        /// </summary>
        public static Vector2 HalfExtents(bool orthographic, float fovOrSizeDegrees, float aspect, float distance)
        {
            float halfHeight = orthographic
                ? fovOrSizeDegrees
                : distance * Mathf.Tan(fovOrSizeDegrees * 0.5f * Mathf.Deg2Rad);

            return new Vector2(halfHeight * aspect, halfHeight);
        }

        /// <inheritdoc cref="HalfExtents(bool,float,float,float)"/>
        public static Vector2 HalfExtents(Camera camera, float distance) =>
            camera == null
                ? Vector2.zero
                : HalfExtents(camera.orthographic,
                              camera.orthographic ? camera.orthographicSize : camera.fieldOfView,
                              camera.aspect, distance);

        /// <summary>
        /// A viewport point as a position in the camera's local space, on the plane at
        /// <paramref name="distance"/>.
        /// </summary>
        public static Vector3 LocalPosition(bool orthographic, float fovOrSizeDegrees, float aspect,
                                            float distance, Vector2 viewport)
        {
            var half = HalfExtents(orthographic, fovOrSizeDegrees, aspect, distance);

            return new Vector3((viewport.x - 0.5f) * half.x * 2f,
                               (viewport.y - 0.5f) * half.y * 2f,
                               distance);
        }

        /// <inheritdoc cref="LocalPosition(bool,float,float,float,Vector2)"/>
        public static Vector3 LocalPosition(Camera camera, Vector2 viewport, float distance) =>
            camera == null
                ? Vector3.zero
                : LocalPosition(camera.orthographic,
                                camera.orthographic ? camera.orthographicSize : camera.fieldOfView,
                                camera.aspect, distance, viewport);

        /// <summary>
        /// A height given as a fraction of the frame, in metres on the plane at <paramref name="distance"/>.
        /// </summary>
        public static float WorldHeight(bool orthographic, float fovOrSizeDegrees, float aspect,
                                        float distance, float heightFraction) =>
            HalfExtents(orthographic, fovOrSizeDegrees, aspect, distance).y * 2f * heightFraction;

        /// <inheritdoc cref="WorldHeight(bool,float,float,float,float)"/>
        public static float WorldHeight(Camera camera, float heightFraction, float distance) =>
            camera == null
                ? 0f
                : WorldHeight(camera.orthographic,
                              camera.orthographic ? camera.orthographicSize : camera.fieldOfView,
                              camera.aspect, distance, heightFraction);
    }
}
