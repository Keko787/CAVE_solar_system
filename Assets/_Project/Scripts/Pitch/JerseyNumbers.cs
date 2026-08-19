using System.Collections.Generic;
using UnityEngine;
using Stadium3D.Football;

namespace Stadium3D.Pitch
{
    /// <summary>
    /// Turns the jersey numbers toward one camera, and pulls apart the ones that would otherwise land on
    /// the same pixels.
    /// </summary>
    /// <remarks>
    /// <b>Why a lift by distance alone does nothing.</b> A point at world height <c>h</c> and depth
    /// <c>d</c> lands at a screen height proportional to <c>(h − eyeY) / d</c>. Raise every number by
    /// <c>k · d</c> and the contribution is <c>k · d / d = k</c> — the same shift for the near player and
    /// the far one. Every number moves up together and none of them separates. The lift has to be measured
    /// <em>against a reference depth</em>, which is what turns a difference in depth into a difference on
    /// screen.
    ///
    /// <para><b>The reference is the nearest number drawn, and the curve saturates rather than clamps.</b>
    /// Both details were learned the hard way. Referencing the <em>mean</em> depth looks more balanced and
    /// fails badly: with two receivers in front of the camera and nine team-mates thirty metres downfield,
    /// the mean sits behind the whole visible group, so both near numbers take a large negative lift, both
    /// hit the limit, and two identical lifts separate nothing — while dropping the labels onto the bodies
    /// they belong to. Referencing the nearest makes every lift non-negative: the front number rests where
    /// it was put and everything behind it rises off it.</para>
    ///
    /// <para>Saturating matters for the same reason. A hard clamp hands the same height to everything past
    /// the limit, which un-separates exactly the crowded far group it was meant to tidy.
    /// <c>limit · (1 − e^(−k·Δ/limit))</c> rises across the whole range a wall actually shows, while
    /// approaching a bound rather than climbing off the top of it. The near pairs — the ones actually
    /// legible — get almost the full <c>k · Δ</c> the slope promises.</para>
    ///
    /// <para><b>It does give up eventually, and on purpose.</b> The curve is strictly increasing in exact
    /// arithmetic but not in <c>float</c>: past roughly 28 m behind the nearest number, the exponential
    /// has fallen below single-precision resolution and two players genuinely receive the same lift. That
    /// is the right place to stop caring — at that range the bodies have converged to a few pixels and
    /// their labels are unreadable whatever height they are drawn at.</para>
    ///
    /// <para><b>Only the cameras that draw numbers do this work.</b> Numbers ride the two per-wall layers,
    /// so a camera carrying neither returns immediately — which is every display camera, the broadcast wall
    /// and the floor. Gating on the player layer instead, as this once did, skipped exactly the two walls
    /// that show numbers and left them facing wherever they were last pointed.</para>
    /// </remarks>
    public static class JerseyNumbers
    {
        /// <summary>Metres of lift per metre of depth away from the group. Zero disables the separation.</summary>
        public const float DefaultDepthLift = 0.7f;

        /// <summary>Most a number may be moved from its resting height, metres.</summary>
        public const float DefaultLiftLimit = 1.2f;

        /// <summary>Face and separate every number this camera draws.</summary>
        public static void Orient(Camera camera, IReadOnlyList<PlayerMarker> markers,
                                  float depthLift = DefaultDepthLift, float liftLimit = DefaultLiftLimit)
        {
            if (camera == null || markers == null || markers.Count == 0) return;

            int rightLayer = LayerMask.NameToLayer(PitchLayers.RightWall);
            int leftLayer = LayerMask.NameToLayer(PitchLayers.LeftWall);

            bool home = rightLayer >= 0 && (camera.cullingMask & (1 << rightLayer)) != 0;
            bool away = leftLayer >= 0 && (camera.cullingMask & (1 << leftLayer)) != 0;
            if (!home && !away) return;

            Vector3 eye = camera.transform.position;

            // Anchored on the nearest player who is actually IN SHOT, not the nearest on the field. A
            // wall following a receiver often has a team-mate ten metres nearer the boom and off the side
            // of the frame; anchoring there puts the whole visible group deep into the saturating tail of
            // the curve, where every lift is the limit, every lift is therefore equal, and the labels
            // separate no better than before while floating far above the heads they belong to.
            float nearest = float.MaxValue;
            for (int i = 0; i < markers.Count; i++)
            {
                if (!Draws(markers[i], home, away) || !InShot(camera, markers[i])) continue;
                nearest = Mathf.Min(nearest, Vector3.Distance(eye, markers[i].transform.position));
            }

            // Nobody in shot: nothing on this wall to keep apart, so leave the numbers at rest.
            if (nearest == float.MaxValue) return;

            for (int i = 0; i < markers.Count; i++)
            {
                var marker = markers[i];
                if (!Draws(marker, home, away)) continue;

                float behind = Vector3.Distance(eye, marker.transform.position) - nearest;
                marker.FaceNumberAt(camera, Lift(behind, depthLift, liftLimit));
            }
        }

        /// <summary>Whether this player is inside the camera's frame at all.</summary>
        static bool InShot(Camera camera, PlayerMarker marker)
        {
            Vector3 v = camera.WorldToViewportPoint(marker.transform.position);
            return v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
        }

        /// <summary>
        /// How far to raise a number for a player standing <paramref name="behind"/> metres further away
        /// than the nearest one drawn. Strictly increasing, and bounded by <paramref name="limit"/>.
        /// </summary>
        static float Lift(float behind, float slope, float limit)
        {
            if (limit <= 0f || slope <= 0f || behind <= 0f) return 0f;
            return limit * (1f - Mathf.Exp(-slope * behind / limit));
        }

        /// <summary>Whether this camera draws this player's number, which is decided by whose wall it is.</summary>
        static bool Draws(PlayerMarker marker, bool home, bool away)
        {
            if (marker == null || marker.Identity == null) return false;
            return marker.Identity.Side == TeamSide.Home ? home : away;
        }
    }
}
