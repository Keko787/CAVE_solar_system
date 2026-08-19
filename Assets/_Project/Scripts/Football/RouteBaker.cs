using System.Collections.Generic;
using UnityEngine;

namespace Stadium3D.Football
{
    /// <summary>Warnings raised while baking a route, surfaced at authoring time rather than in the CAVE.</summary>
    public sealed class BakeReport
    {
        readonly List<string> m_Warnings = new List<string>();

        public IReadOnlyList<string> Warnings => m_Warnings;
        public bool HasWarnings => m_Warnings.Count > 0;

        public void Warn(string message) => m_Warnings.Add(message);
    }

    /// <summary>
    /// Turns authored waypoints plus a <see cref="MotionLimits"/> envelope into a <see cref="MotionTrack"/>,
    /// once, at load. This is the replacement for the per-frame integrator the source project used
    /// (<c>SimulatedFeedSource.Advance</c>), and it exists because that shape had four defects that no amount
    /// of tuning fixes:
    /// </summary>
    /// <remarks>
    /// <list type="number">
    /// <item><description><c>travel = Speed * dt</c> put a body at full sprint on the first frame after the
    /// snap. Here the speed solve starts at rest and is bounded by acceleration, so it cannot.</description></item>
    /// <item><description><c>Quaternion.LookRotation(dir)</c> snapped the facing in a single frame on a cut.
    /// Here heading is turn-rate limited and seeded from the authored <em>pre-snap</em> facing — a lineman in
    /// his stance faces the defense, not the direction of his first step.</description></item>
    /// <item><description><c>Seg = (Seg + 1) % Loop.Length</c> made every route an infinite patrol with no
    /// terminal state. A play is finite; <see cref="MotionTrack"/> clamps and the loop is a transport
    /// decision.</description></item>
    /// <item><description>A body standing <b>on</b> its first waypoint silently lost that waypoint to a
    /// duplicate-point guard — which is exactly the pre-snap case for all 22 players. Here co-located
    /// waypoints <see cref="MergeControlPoints">merge into a dwell</see> instead of being stepped past.</description></item>
    /// </list>
    ///
    /// <para>The solve runs in five passes: resample the path by arc length, cap speed by curvature and grip,
    /// solve the fastest feasible speed forwards and backwards, integrate to time, then limit the facing.
    /// The result is resampled to a fixed rate so every later lookup is O(1) with no cursor.</para>
    /// </remarks>
    public static class RouteBaker
    {
        /// <summary>Arc-length step for the resampled path, metres. Fine enough that curvature is stable.</summary>
        const float ArcStepMeters = 0.05f;

        /// <summary>Sub-samples per spline segment before arc-length resampling.</summary>
        const int SegmentSubdivisions = 32;

        /// <summary>Two waypoints closer than this are the same point, and the gap between them is a dwell.</summary>
        const float CoincidentMeters = 0.02f;

        /// <summary>
        /// Tightest corner radius the curvature caps are allowed to see, metres. A spline through a hard
        /// reversal — a screen lineman turning back, a receiver's comeback — can report an arbitrarily small
        /// radius over a handful of nodes, and <c>sqrt(grip / curvature)</c> then collapses toward zero.
        /// Nobody turns inside half a metre, so treating anything tighter as half a metre is both physically
        /// honest and the thing that stops one sharp break dominating a whole route's duration.
        /// </summary>
        const float MinCornerRadiusMeters = 0.5f;

        /// <summary>
        /// Floor on the curvature-derived speed cap, as a fraction of top speed. A body that is running
        /// slows through a break; it does not stop. A genuine stop is authored — <see cref="WaypointKind.Stop"/>
        /// or <see cref="WaypointKind.Dwell"/> — and those bypass this floor, which is what keeps "stopped
        /// because the designer said so" distinguishable from "stopped because the spline pinched".
        /// </summary>
        const float MinCornerSpeedFraction = 0.18f;

        /// <summary>
        /// Floor on the average speed used to integrate a step to time, m/s. Without it a near-zero pair of
        /// speeds turns a 5 cm step into a multi-second crawl, and a route silently balloons to twenty
        /// seconds — which is exactly the failure this constant exists to make impossible.
        /// </summary>
        const float MinIntegrationSpeedMps = 0.35f;

        const float Eps = 1e-5f;

        /// <summary>
        /// Bake one route. <paramref name="points"/> are already in pitch-local space, paired 1:1 with
        /// <paramref name="waypoints"/> for their authoring metadata.
        /// </summary>
        /// <param name="initialFacingDegrees">Pre-snap facing. Seeds the heading solve and is held exactly at t = 0.</param>
        /// <param name="minDuration">Pad the track out to at least this long with a terminal dwell.</param>
        public static MotionTrack Bake(
            IReadOnlyList<Vector3> points,
            IReadOnlyList<RouteWaypoint> waypoints,
            MotionLimits limits,
            float initialFacingDegrees,
            float minDuration,
            float sampleHz,
            BakeReport report = null,
            string routeLabel = null)
        {
            limits = limits.Sanitized();
            sampleHz = Mathf.Max(1f, sampleHz);

            if (points == null || points.Count == 0)
                return MotionTrack.Standing(Vector3.zero, initialFacingDegrees, minDuration, sampleHz);

            // ── pass 0: merge co-located waypoints into dwells ─────────────────────────────────────
            MergeControlPoints(points, waypoints, out var control, out var meta);

            if (control.Count == 1)
            {
                // A lineman with one authored point stands the whole play. Not an error — most of the
                // offensive line is authored this way.
                float dwell = Mathf.Max(minDuration, meta[0].DwellSeconds);
                return MotionTrack.Standing(control[0], initialFacingDegrees, dwell, sampleHz);
            }

            // ── pass 1: spline, arc-length resample, curvature ─────────────────────────────────────
            var path = ResampleByArcLength(control, out var nodeOfControl);
            int n = path.Count;
            var kappa = Curvature(path);

            // ── pass 2: speed caps, then the forward/backward trapezoidal solve ────────────────────
            float cornerFloor = limits.MaxSpeedMps * MinCornerSpeedFraction;
            float maxKappa = 1f / MinCornerRadiusMeters;

            var vCap = new float[n];
            for (int k = 0; k < n; k++)
            {
                float k2 = Mathf.Min(Mathf.Max(kappa[k], Eps), maxKappa);
                float byGrip = Mathf.Sqrt(limits.LateralGripMps2 / k2);
                float byTurn = (limits.MaxTurnRateDegPerSec * Mathf.Deg2Rad) / k2;
                vCap[k] = Mathf.Max(cornerFloor, Mathf.Min(limits.MaxSpeedMps, Mathf.Min(byGrip, byTurn)));
            }

            // Waypoint speed limits are POINT constraints, not interpolated along the segment: the
            // accel/decel passes below are what create the ramp into and out of a break, and that reads
            // sharper than dragging the whole segment down to the cut speed.
            for (int c = 0; c < control.Count; c++)
            {
                int k = nodeOfControl[c];
                var m = meta[c];
                float cap = limits.MaxSpeedMps * Mathf.Clamp01(m.SpeedFraction);
                if (m.Kind == WaypointKind.Dwell || m.DwellSeconds > 0f)
                    cap = 0f;
                vCap[k] = Mathf.Min(vCap[k], cap);
            }

            vCap[0] = 0f;                                                   // out of a stance, from rest
            if (meta[meta.Count - 1].Kind == WaypointKind.Stop)
                vCap[n - 1] = 0f;

            var v = new float[n];
            v[0] = vCap[0];
            for (int k = 1; k < n; k++)                                     // forward: acceleration-bounded
            {
                float ds = Vector3.Distance(path[k - 1], path[k]);
                v[k] = Mathf.Min(vCap[k], Mathf.Sqrt(v[k - 1] * v[k - 1] + 2f * limits.AccelMps2 * ds));
            }
            for (int k = n - 2; k >= 0; k--)                                // backward: braking-bounded
            {
                float ds = Vector3.Distance(path[k], path[k + 1]);
                v[k] = Mathf.Min(v[k], Mathf.Sqrt(v[k + 1] * v[k + 1] + 2f * limits.DecelMps2 * ds));
            }

            if (report != null)
            {
                for (int c = 0; c < control.Count; c++)
                {
                    int k = nodeOfControl[c];
                    if (meta[c].Kind == WaypointKind.Cut && v[k] < limits.MaxSpeedMps * 0.55f)
                        report.Warn($"{routeLabel ?? "route"}: the break at waypoint {c} caps speed to " +
                                    $"{v[k]:0.0} m/s — the corner is tighter than the body can hold.");
                }
            }

            // ── pass 3: integrate to time, inserting dwells as flat stretches ──────────────────────
            var times = new List<float>(n + 8);
            var poses = new List<Vector3>(n + 8);
            var speeds = new List<float>(n + 8);
            var dists = new List<float>(n + 8);

            int nextControl = 0;
            float t = 0f, dist = 0f;

            for (int k = 0; k < n; k++)
            {
                if (k > 0)
                {
                    float ds = Vector3.Distance(path[k - 1], path[k]);
                    float vBar = 0.5f * (v[k - 1] + v[k]);
                    t += ds / Mathf.Max(vBar, MinIntegrationSpeedMps);
                    dist += ds;
                }

                times.Add(t); poses.Add(path[k]); speeds.Add(v[k]); dists.Add(dist);

                // A dwell is a second node at the same place, later in time — which is precisely what the
                // old duplicate-waypoint guard threw away.
                while (nextControl < control.Count && nodeOfControl[nextControl] == k)
                {
                    float dwell = meta[nextControl].DwellSeconds;
                    if (dwell > 0f)
                    {
                        t += dwell;
                        times.Add(t); poses.Add(path[k]); speeds.Add(0f); dists.Add(dist);
                    }
                    nextControl++;
                }
            }

            // ── pass 4: heading, turn-rate limited, now that dt per step is known ──────────────────
            var headings = SolveHeadings(poses, times, control, nodeOfControl, meta,
                                         initialFacingDegrees, limits.MaxTurnRateDegPerSec);

            // ── pass 5: resample to the fixed rate ─────────────────────────────────────────────────
            float natural = times[times.Count - 1];
            float duration = Mathf.Max(natural, Mathf.Max(0f, minDuration));

            if (report != null)
            {
                // A route that takes far longer than its length can justify means the speed solve collapsed
                // somewhere — almost always a spline pinch at a hard reversal. Because a play's duration is
                // the MAX over its participants, one such route silently stretches the whole play, so this is
                // worth saying out loud at bake time rather than leaving to whoever notices the huddle is long.
                float authoredDwell = 0f;
                for (int c = 0; c < meta.Count; c++) authoredDwell += Mathf.Max(0f, meta[c].DwellSeconds);

                float floorSpeed = limits.MaxSpeedMps * MinCornerSpeedFraction;
                float plausible = authoredDwell + dist / Mathf.Max(floorSpeed, Eps) + 1.5f;

                if (natural > plausible)
                    report.Warn($"{routeLabel ?? "route"}: baked to {natural:0.0}s for {dist:0.0} m of path " +
                                $"(expected under {plausible:0.0}s) — the speed solve is collapsing, most " +
                                $"likely at a reversal the spline is turning into a near-zero-radius corner.");
            }

            return Resample(times, poses, headings, speeds, dists, duration, sampleHz, initialFacingDegrees);
        }

        // ── pass 0 ─────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Collapse consecutive waypoints that sit on the same spot into one control point carrying their
        /// combined dwell. The old integrator's guard force-advanced past a duplicate, which silently ate a
        /// waypoint whenever a body started standing on it — the pre-snap case for every player on the field.
        /// </summary>
        static void MergeControlPoints(
            IReadOnlyList<Vector3> points,
            IReadOnlyList<RouteWaypoint> waypoints,
            out List<Vector3> control,
            out List<RouteWaypoint> meta)
        {
            control = new List<Vector3>(points.Count);
            meta = new List<RouteWaypoint>(points.Count);

            for (int i = 0; i < points.Count; i++)
            {
                var wp = (waypoints != null && i < waypoints.Count && waypoints[i] != null)
                    ? waypoints[i].Clone()
                    : new RouteWaypoint();

                if (control.Count > 0 && Vector3.Distance(control[control.Count - 1], points[i]) <= CoincidentMeters)
                {
                    var prev = meta[meta.Count - 1];
                    prev.DwellSeconds += Mathf.Max(0f, wp.DwellSeconds);
                    if (wp.Kind == WaypointKind.Stop) prev.Kind = WaypointKind.Stop;
                    if (wp.PinFacing) { prev.PinFacing = true; prev.FacingDegrees = wp.FacingDegrees; }
                    continue;
                }

                control.Add(points[i]);
                meta.Add(wp);
            }
        }

        // ── pass 1 ─────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Centripetal Catmull-Rom through the control points, resampled at a uniform arc-length step.
        /// Centripetal (alpha = 0.5) rather than uniform because uniform Catmull-Rom forms cusps and loops on
        /// the tight, unevenly spaced corners a route diagram is full of.
        /// </summary>
        static List<Vector3> ResampleByArcLength(List<Vector3> control, out int[] nodeOfControl)
        {
            // Dense spline samples, remembering where each control point landed.
            var dense = new List<Vector3>(control.Count * SegmentSubdivisions);
            var denseOfControl = new int[control.Count];

            for (int i = 0; i < control.Count - 1; i++)
            {
                Vector3 p0 = i == 0 ? control[0] * 2f - control[1] : control[i - 1];
                Vector3 p1 = control[i];
                Vector3 p2 = control[i + 1];
                Vector3 p3 = i + 2 < control.Count
                    ? control[i + 2]
                    : control[control.Count - 1] * 2f - control[control.Count - 2];

                denseOfControl[i] = dense.Count;
                for (int s = 0; s < SegmentSubdivisions; s++)
                    dense.Add(CatmullRom(p0, p1, p2, p3, s / (float)SegmentSubdivisions));
            }
            denseOfControl[control.Count - 1] = dense.Count;
            dense.Add(control[control.Count - 1]);

            // Uniform arc-length resample, tracking which output node each control point maps to.
            var path = new List<Vector3> { dense[0] };
            nodeOfControl = new int[control.Count];
            nodeOfControl[0] = 0;

            int nextControl = 1;
            float carry = 0f;

            for (int i = 1; i < dense.Count; i++)
            {
                Vector3 a = dense[i - 1], b = dense[i];
                float seg = Vector3.Distance(a, b);

                if (nextControl < control.Count && i >= denseOfControl[nextControl])
                {
                    path.Add(control[nextControl]);              // land exactly on the authored point
                    nodeOfControl[nextControl] = path.Count - 1;
                    nextControl++;
                    carry = 0f;
                    continue;
                }

                if (seg <= Eps) continue;

                float travelled = carry;
                while (travelled + ArcStepMeters <= seg)
                {
                    travelled += ArcStepMeters;
                    path.Add(Vector3.Lerp(a, b, travelled / seg));
                }
                carry = travelled - seg + ArcStepMeters;
                if (carry >= ArcStepMeters) carry = 0f;
            }

            for (; nextControl < control.Count; nextControl++)
            {
                path.Add(control[nextControl]);
                nodeOfControl[nextControl] = path.Count - 1;
            }

            if (path.Count < 2)
                path.Add(control[control.Count - 1]);

            return path;
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            // Barry-Goldman with centripetal knot spacing, so tight corners stay cusp-free.
            float t0 = 0f;
            float t1 = t0 + Mathf.Pow(Mathf.Max(Vector3.Distance(p0, p1), Eps), 0.5f);
            float t2 = t1 + Mathf.Pow(Mathf.Max(Vector3.Distance(p1, p2), Eps), 0.5f);
            float t3 = t2 + Mathf.Pow(Mathf.Max(Vector3.Distance(p2, p3), Eps), 0.5f);

            float tt = Mathf.Lerp(t1, t2, t);

            Vector3 a1 = ((t1 - tt) * p0 + (tt - t0) * p1) / Mathf.Max(t1 - t0, Eps);
            Vector3 a2 = ((t2 - tt) * p1 + (tt - t1) * p2) / Mathf.Max(t2 - t1, Eps);
            Vector3 a3 = ((t3 - tt) * p2 + (tt - t2) * p3) / Mathf.Max(t3 - t2, Eps);
            Vector3 b1 = ((t2 - tt) * a1 + (tt - t0) * a2) / Mathf.Max(t2 - t0, Eps);
            Vector3 b2 = ((t3 - tt) * a2 + (tt - t1) * a3) / Mathf.Max(t3 - t1, Eps);
            return ((t2 - tt) * b1 + (tt - t1) * b2) / Mathf.Max(t2 - t1, Eps);
        }

        /// <summary>Discrete curvature from the circumradius of each consecutive triple, 1/m.</summary>
        static float[] Curvature(List<Vector3> path)
        {
            int n = path.Count;
            var kappa = new float[n];

            for (int k = 1; k < n - 1; k++)
            {
                Vector3 p0 = path[k - 1], p1 = path[k], p2 = path[k + 1];
                float a = Vector3.Distance(p0, p1);
                float b = Vector3.Distance(p1, p2);
                float c = Vector3.Distance(p0, p2);
                float area = Vector3.Cross(p1 - p0, p2 - p0).magnitude * 0.5f;

                kappa[k] = area <= Eps ? 0f : (4f * area) / Mathf.Max(a * b * c, Eps);
            }

            if (n > 2) { kappa[0] = kappa[1]; kappa[n - 1] = kappa[n - 2]; }
            return kappa;
        }

        // ── pass 4 ─────────────────────────────────────────────────────────────────────────────────

        static float[] SolveHeadings(
            List<Vector3> poses, List<float> times,
            List<Vector3> control, int[] nodeOfControl, List<RouteWaypoint> meta,
            float initialFacingDegrees, float maxTurnRateDegPerSec)
        {
            int n = poses.Count;
            var headings = new float[n];
            headings[0] = initialFacingDegrees;

            // A pinned facing applies from its waypoint until the next one, so authoring "face the ball" at
            // the top of a backpedal holds it for that whole leg rather than for one node.
            var pinned = new float?[n];
            for (int c = 0; c < control.Count; c++)
            {
                if (!meta[c].PinFacing) continue;
                int from = NodeIndexInPoses(poses, control[c], nodeOfControl[c]);
                int to = c + 1 < control.Count ? NodeIndexInPoses(poses, control[c + 1], nodeOfControl[c + 1]) : n;
                for (int k = Mathf.Max(0, from); k < Mathf.Min(n, to); k++)
                    pinned[k] = meta[c].FacingDegrees;
            }

            for (int k = 1; k < n; k++)
            {
                float desired;
                if (pinned[k].HasValue)
                {
                    desired = pinned[k].Value;
                }
                else
                {
                    Vector3 d = poses[Mathf.Min(k + 1, n - 1)] - poses[k - 1];
                    d.y = 0f;
                    desired = d.sqrMagnitude <= Eps
                        ? headings[k - 1]                       // standing still: hold the facing
                        : Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                }

                float dt = Mathf.Max(times[k] - times[k - 1], Eps);
                float step = Mathf.Clamp(
                    Mathf.DeltaAngle(headings[k - 1], desired),
                    -maxTurnRateDegPerSec * dt,
                     maxTurnRateDegPerSec * dt);

                headings[k] = headings[k - 1] + step;
            }

            return headings;
        }

        /// <summary>
        /// Where a control point sits in the dwell-expanded pose list. The dwell insertions in pass 3 shift
        /// indices after the first dwell, so the pass-1 index is a starting guess rather than an answer.
        /// </summary>
        static int NodeIndexInPoses(List<Vector3> poses, Vector3 point, int hint)
        {
            if (hint >= 0 && hint < poses.Count && Vector3.Distance(poses[hint], point) <= CoincidentMeters)
                return hint;

            for (int k = 0; k < poses.Count; k++)
                if (Vector3.Distance(poses[k], point) <= CoincidentMeters)
                    return k;

            return Mathf.Clamp(hint, 0, poses.Count - 1);
        }

        // ── pass 5 ─────────────────────────────────────────────────────────────────────────────────

        static MotionTrack Resample(
            List<float> times, List<Vector3> poses, float[] headings, List<float> speeds, List<float> dists,
            float duration, float sampleHz, float initialFacingDegrees)
        {
            int count = Mathf.Max(2, Mathf.RoundToInt(duration * sampleHz) + 1);
            var samples = new MotionSample[count];

            int cursor = 0;
            float peak = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = i / sampleHz;

                if (i == 0)
                {
                    // Written from the authored waypoint directly, never interpolated. This is the bit-exact
                    // pre-snap guarantee the whole replay/loop story depends on.
                    samples[0] = new MotionSample(0f, poses[0], initialFacingDegrees, 0f, 0f, 0f);
                    continue;
                }

                while (cursor < times.Count - 2 && times[cursor + 1] < t) cursor++;

                int a = cursor, b = Mathf.Min(cursor + 1, times.Count - 1);
                float span = times[b] - times[a];
                float u = span <= Eps ? 0f : Mathf.Clamp01((t - times[a]) / span);

                Vector3 pos = Vector3.Lerp(poses[a], poses[b], u);
                float head = Mathf.LerpAngle(headings[a], headings[b], u);
                float spd = t >= times[times.Count - 1] ? 0f : Mathf.Lerp(speeds[a], speeds[b], u);
                float dist = Mathf.Lerp(dists[a], dists[b], u);

                if (t >= times[times.Count - 1])
                {
                    pos = poses[poses.Count - 1];               // terminal dwell out to the play's duration
                    head = headings[headings.Length - 1];
                    dist = dists[dists.Count - 1];
                }

                peak = Mathf.Max(peak, spd);
                samples[i] = new MotionSample(t, pos, head, spd, dist, peak);
            }

            return new MotionTrack(samples, sampleHz);
        }
    }
}
