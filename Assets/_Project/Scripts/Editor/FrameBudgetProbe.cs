using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Stadium3D.Football;
using Stadium3D.Pitch;
using Debug = UnityEngine.Debug;

namespace Stadium3D.EditorTools
{
    /// <summary>
    /// Measures what one CAVE frame actually costs: the sim and marker work on the CPU, the four pitch
    /// render targets, and a stand-in for the eight display loops.
    /// </summary>
    /// <remarks>
    /// <b>Why this exists at all.</b> §4.4 of the plan gives the pixel count as arithmetic — 41.1 MPix and
    /// twelve full URP loops per frame — and deliberately refuses to put a millisecond figure next to it,
    /// because a number invented without a GPU is worse than no number. M2 was supposed to supply the real
    /// one and could not: the stress test ran before the render chain worked end to end, so whatever it
    /// timed, it was not the scene. M4 has since added a stadium bowl, six renderers per player and four
    /// line renderers, which is exactly when an unmeasured budget stops being a footnote.
    ///
    /// <para><b>What it measures honestly, and what it does not.</b> The four pitch cameras are the real
    /// ones out of the built scene, rendering the real drive at their shipped resolutions — that quarter of
    /// the frame is measured, not modelled. The eight display loops cannot be: MiddleVR builds those cameras
    /// at runtime from the <c>.vrx</c>, and nothing here is running MiddleVR. They are stood in for by a
    /// camera in the room carrying the mask <see cref="PitchIsolation"/> leaves behind, rendered at the
    /// viewport sizes the <c>.vrx</c> declares, twice per surface for two eyes. Same pixels, same geometry,
    /// same pipeline; missing MiddleVR's own per-frame work and the driver's stereo path. Read it as a floor
    /// on the display cost, not a forecast.</para>
    ///
    /// <para><b>The whole-frame number is the budget number.</b> Per-camera timings need a GPU sync after
    /// each camera to mean anything, and that sync stops the pipeline — so the sum of the parts overstates
    /// the whole. Both are reported: the twelve-loop total with one sync at the end is the figure to compare
    /// against a refresh interval, and the breakdown is for finding which surface is expensive.</para>
    ///
    /// <para><b>Median, not mean.</b> A first frame carries shader compilation and resource creation, and an
    /// editor process is not a quiet machine. Warm-up frames are discarded and the median is quoted, with
    /// min and p95 alongside so a noisy run is visible as one rather than averaged into a plausible lie.</para>
    /// </remarks>
    public static class FrameBudgetProbe
    {
        /// <summary>Discarded. Shader compilation and RT allocation land here, not in the sample.</summary>
        const int WarmupFrames = 30;

        /// <summary>Timed frames. Five seconds of drive at 60 Hz.</summary>
        const int SampleFrames = 300;

        const double FrameStep = 1.0 / 60.0;

        /// <summary>The play worth measuring: four verticals, everything on the field moving.</summary>
        const int StressPlay = 5;

        [MenuItem("Stadium3D/Measure/Frame Budget", priority = 40)]
        public static void Measure() => Run();

        /// <summary>
        /// Batch entry point.
        /// <c>-executeMethod Stadium3D.EditorTools.FrameBudgetProbe.MeasureBatch</c>
        /// </summary>
        public static void MeasureBatch() => EditorApplication.Exit(Run() ? 0 : 2);

        static bool Run()
        {
            int restoreQuality = QualitySettings.GetQualityLevel();
            SelectQualityLevel();

            // Measure the scene that is open if it is already a CAVE scene. Rebuilding would be the safer
            // default for a batch run and the wrong one from a menu item, which is the case where somebody
            // has the scene open and unsaved — and a perf probe that discards your work to get a cleaner
            // number is not a tool anyone runs twice.
            var cameras = UnityEngine.Object.FindObjectsOfType<PitchCameraBase>(true);
            if (cameras.Length == 0)
            {
                Debug.Log("[Stadium3D] No pitch rig in the open scene — building the CAVE scene to measure.");
                StadiumCaveBuilder.BuildScene();
                cameras = UnityEngine.Object.FindObjectsOfType<PitchCameraBase>(true);
            }

            var sim = new FootballSim(ScriptedDrive.Build());
            var markers = UnityEngine.Object.FindObjectsOfType<PlayerMarker>(true);
            var ball = UnityEngine.Object.FindObjectOfType<BallMarker>(true);
            var overlays = UnityEngine.Object.FindObjectsOfType<FieldStateOverlay>(true);
            var trails = UnityEngine.Object.FindObjectsOfType<RouteTrail>(true);

            if (cameras.Length == 0)
            {
                Debug.LogError("[Stadium3D] No pitch cameras in the CAVE scene — nothing to measure.");
                return false;
            }

            foreach (var cam in cameras) cam.InitializeForPreview();

            var display = BuildDisplayProxy(out var proxyTargets);
            var sync = new Texture2D(1, 1, TextureFormat.RGBA32, false);

            var cpu = new Sample("CPU: sim, markers, cameras, trails");
            var frame = new Sample("GPU: whole frame, 12 loops, one sync");
            var perCamera = new Dictionary<string, Sample>();

            sim.Director.SeekToPlay(StressPlay, 0f);

            for (int i = 0; i < WarmupFrames + SampleFrames; i++)
            {
                bool timed = i >= WarmupFrames;

                var clock = Stopwatch.StartNew();
                sim.Tick(FrameStep);
                UpdateScene(sim, markers, ball, overlays, trails, cameras);
                clock.Stop();
                if (timed) cpu.Add(clock.Elapsed.TotalMilliseconds);

                // The frame as the CAVE draws it: everything submitted, then a single sync. Nothing in
                // between, because a sync between cameras measures a pipeline that has been stopped.
                clock.Restart();
                foreach (var cam in cameras) cam.Camera.Render();
                foreach (var target in proxyTargets)
                {
                    display.targetTexture = target.Texture;
                    display.transform.rotation = target.Rotation;
                    RenderBothEyes(display, target);
                }
                Sync(proxyTargets[proxyTargets.Count - 1].Texture, sync);
                clock.Stop();
                if (timed) frame.Add(clock.Elapsed.TotalMilliseconds);

                // Breakdown, each with its own sync. Inflated by that sync, and only ever compared to
                // its siblings.
                foreach (var cam in cameras)
                {
                    clock.Restart();
                    cam.Camera.Render();
                    Sync(cam.View, sync);
                    clock.Stop();
                    if (timed) Bucket(perCamera, cam.name, $"pitch: {cam.name}").Add(clock.Elapsed.TotalMilliseconds);
                }

                foreach (var target in proxyTargets)
                {
                    display.targetTexture = target.Texture;
                    display.transform.rotation = target.Rotation;

                    clock.Restart();
                    RenderBothEyes(display, target);
                    Sync(target.Texture, sync);
                    clock.Stop();
                    if (timed)
                        Bucket(perCamera, target.Name, $"display proxy: {target.Name} ×2 eyes").Add(clock.Elapsed.TotalMilliseconds);
                }
            }

            Report(sim, cameras, proxyTargets, display, cpu, frame, perCamera);

            display.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(display.gameObject);
            UnityEngine.Object.DestroyImmediate(sync);
            foreach (var target in proxyTargets) target.Texture.Release();

            // Put the level back. Unity persists the active quality level into ProjectSettings, so a probe
            // that measures Ultra and walks away has quietly changed which pipeline the next Play-mode run
            // uses — a measuring instrument that alters the thing it measures, and a dirty settings file
            // in the bargain.
            if (QualitySettings.GetQualityLevel() != restoreQuality)
                QualitySettings.SetQualityLevel(restoreQuality, true);

            return true;
        }

        /// <summary>
        /// Switch quality level if <c>-budgetQuality</c> asked for one, and say which pipeline that means.
        /// </summary>
        /// <remarks>
        /// <b>The level is not a detail; it is which render pipeline gets measured.</b> This project carries
        /// two URP assets, and the quality levels split between them: the editor sits on <i>Low</i>, which is
        /// <c>Performance URP Config</c> — HDR off, main-light shadows off, soft shadows off — while a
        /// Standalone player build defaults to <i>Ultra</i>, which is <c>Quality URP Config</c>: HDR on,
        /// main-light shadows on at 4096², soft shadows on. Twelve loops of that is a different budget
        /// entirely, and §4.4's shadow argument only holds on one of them. Measuring whichever level the
        /// process happens to boot with and calling it "the frame budget" is how a number becomes a story.
        /// </remarks>
        static void SelectQualityLevel()
        {
            string wanted = ArgValue("-budgetQuality");
            if (!string.IsNullOrEmpty(wanted))
            {
                var names = QualitySettings.names;
                int index = -1;

                for (int i = 0; i < names.Length; i++)
                    if (string.Equals(names[i], wanted, System.StringComparison.OrdinalIgnoreCase)) index = i;

                if (index < 0 && int.TryParse(wanted, out int parsed) && parsed >= 0 && parsed < names.Length)
                    index = parsed;

                if (index < 0) Debug.LogWarning($"[Stadium3D] No quality level called \"{wanted}\" — measuring the current one.");
                else QualitySettings.SetQualityLevel(index, true);
            }
        }

        static string ArgValue(string flag)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == flag) return args[i + 1];
            return null;
        }

        /// <summary>Everything the driver does per frame in play mode, which is not running here.</summary>
        static void UpdateScene(FootballSim sim, PlayerMarker[] markers, BallMarker ball,
                                FieldStateOverlay[] overlays, RouteTrail[] trails, PitchCameraBase[] cameras)
        {
            var frames = sim.Frames;
            for (int i = 0; i < frames.Count; i++)
            {
                for (int m = 0; m < markers.Length; m++)
                {
                    if (markers[m].Identity == null || markers[m].Identity.Id != frames[i].PlayerId) continue;
                    markers[m].Apply(frames[i]);
                    break;
                }
            }

            if (ball != null) ball.Apply(sim.Ball);
            foreach (var overlay in overlays) overlay.Apply(sim.Situation);

            foreach (var cam in cameras) cam.TrackForPreview(sim);

            int homeFocus = 0, awayFocus = 0;
            foreach (var cam in cameras)
            {
                if (!(cam is TeamIsoPitchCamera iso)) continue;
                if (iso.Side == TeamSide.Home) homeFocus = iso.SubjectPlayerId;
                else awayFocus = iso.SubjectPlayerId;
            }

            foreach (var trail in trails)
                trail.Apply(sim, trail.Side == TeamSide.Home ? homeFocus : awayFocus);

            foreach (var cam in cameras)
            {
                JerseyNumbers.Orient(cam.Camera, markers);

                if (cam is TeamIsoPitchCamera wall)
                    foreach (var marker in markers)
                        marker.SetWallFocus(wall.Side, marker.PlayerId == wall.SubjectPlayerId);
            }
        }

        // ── the display side ───────────────────────────────────────────────────────────────────────

        struct ProxyTarget
        {
            public string Name;
            public RenderTexture Texture;
            public Quaternion Rotation;
        }

        /// <summary>
        /// One camera standing in for MiddleVR's eight, with the mask <see cref="PitchIsolation"/> leaves.
        /// </summary>
        /// <remarks>
        /// Pointed at each wall in turn rather than four cameras built at once, because the cost being
        /// measured is pixels and geometry, and one camera cannot accidentally differ from itself. The
        /// <c>.vrx</c> declares <c>VerticalFOV="60"</c> on all four <c>CameraStereo</c> nodes; MiddleVR
        /// replaces that with an off-axis frustum solved from the screen corners, which changes what lands
        /// in frame but not how many pixels are shaded.
        /// </remarks>
        static Camera BuildDisplayProxy(out List<ProxyTarget> targets)
        {
            var go = new GameObject("FrameBudgetDisplayProxy") { hideFlags = HideFlags.HideAndDontSave };

            // Seated eye height at the room centre. The head node is tracked in the CAVE and untracked here.
            go.transform.position = new Vector3(0f, 1.6f, 0f);

            var cam = go.AddComponent<Camera>();
            cam.enabled = false;                      // rendered by hand, never by the loop
            cam.cullingMask = ~PitchLayers.PitchMask; // exactly what PitchIsolation leaves behind
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            cam.fieldOfView = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            targets = new List<ProxyTarget>
            {
                new ProxyTarget { Name = "Center", Texture = Target(2560, 1600), Rotation = Quaternion.identity },
                new ProxyTarget { Name = "Floor", Texture = Target(2560, 1600), Rotation = Quaternion.Euler(90f, 0f, 0f) },
                new ProxyTarget { Name = "Left", Texture = Target(1719, 1600), Rotation = Quaternion.Euler(0f, -90f, 0f) },
                new ProxyTarget { Name = "Right", Texture = Target(1719, 1600), Rotation = Quaternion.Euler(0f, 90f, 0f) },
            };
            return cam;
        }

        /// <summary>
        /// Both eyes, offset by half the <c>.vrx</c>'s 0.065 m inter-eye distance.
        /// </summary>
        /// <remarks>
        /// Two identical renders would be the same work, but they would also be the one case a driver or a
        /// cache could collapse — and the point of the proxy is to be a floor on the cost, not a best case.
        /// </remarks>
        static void RenderBothEyes(Camera display, ProxyTarget target)
        {
            Vector3 centre = display.transform.position;
            Vector3 offset = display.transform.right * (0.065f * 0.5f);

            display.transform.position = centre - offset;
            display.Render();

            display.transform.position = centre + offset;
            display.Render();

            display.transform.position = centre;
        }

        static RenderTexture Target(int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = $"proxy_{width}x{height}",
                antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing),
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
            };
            rt.Create();
            return rt;
        }

        /// <summary>
        /// Block until the GPU has finished. One pixel, because the readback is a barrier, not a measurement.
        /// </summary>
        static void Sync(RenderTexture rt, Texture2D into)
        {
            if (rt == null) return;

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            into.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
            into.Apply();
            RenderTexture.active = prev;
        }

        // ── statistics ─────────────────────────────────────────────────────────────────────────────

        class Sample
        {
            public readonly string Label;
            readonly List<double> m_Values = new List<double>(SampleFrames);

            public Sample(string label) => Label = label;
            public void Add(double ms) => m_Values.Add(ms);

            public double Median => Percentile(0.5);
            public double P95 => Percentile(0.95);
            public double Min { get { var s = Sorted(); return s.Count == 0 ? 0 : s[0]; } }

            List<double> Sorted()
            {
                var copy = new List<double>(m_Values);
                copy.Sort();
                return copy;
            }

            double Percentile(double p)
            {
                var s = Sorted();
                if (s.Count == 0) return 0;
                return s[Mathf.Clamp(Mathf.RoundToInt((float)(p * (s.Count - 1))), 0, s.Count - 1)];
            }
        }

        static Sample Bucket(Dictionary<string, Sample> into, string key, string label)
        {
            if (!into.TryGetValue(key, out var sample)) into[key] = sample = new Sample(label);
            return sample;
        }

        // ── the report ─────────────────────────────────────────────────────────────────────────────

        static void Report(FootballSim sim, PitchCameraBase[] cameras, List<ProxyTarget> proxies,
                           Camera display, Sample cpu, Sample frame, Dictionary<string, Sample> perCamera)
        {
            var text = new StringBuilder();
            text.AppendLine("[Stadium3D] ── frame budget ──────────────────────────────────────────────");
            text.AppendLine($"  device      {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})");
            text.AppendLine($"  unity       {Application.unityVersion}, {(Application.isBatchMode ? "batch" : "editor")}");
            var pipeline = QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline;
            text.AppendLine($"  msaa        {Mathf.Max(1, QualitySettings.antiAliasing)}x, quality level " +
                            $"\"{QualitySettings.names[QualitySettings.GetQualityLevel()]}\"");
            text.AppendLine($"  pipeline    {(pipeline != null ? pipeline.name : "none")}");
            text.AppendLine($"  frames      {SampleFrames} timed, {WarmupFrames} discarded, " +
                            $"play {StressPlay} from the snap at {1.0 / FrameStep:0} Hz");
            text.AppendLine();

            text.AppendLine("  per frame, milliseconds          median      min      p95");
            text.AppendLine($"  {Row(cpu)}");
            text.AppendLine($"  {Row(frame)}");
            text.AppendLine();

            text.AppendLine("  breakdown (each synced separately — sums high on purpose)");
            foreach (var entry in perCamera) text.AppendLine($"  {Row(entry.Value)}");
            text.AppendLine();

            double total = cpu.Median + frame.Median;
            text.AppendLine($"  CPU + GPU, median: {total:0.00} ms");
            text.AppendLine($"    vs 60 Hz  (16.67 ms): {Verdict(total, 16.667)}");
            text.AppendLine($"    vs 120 Hz  (8.33 ms): {Verdict(total, 8.333)}");
            text.AppendLine();

            long pitchPixels = 0;
            foreach (var cam in cameras) pitchPixels += (long)cam.View.width * cam.View.height;

            long displayPixels = 0;
            foreach (var proxy in proxies) displayPixels += (long)proxy.Texture.width * proxy.Texture.height * 2;

            text.AppendLine($"  pixels      pitch {pitchPixels / 1e6:0.0} MPix + display {displayPixels / 1e6:0.0} MPix " +
                            $"= {(pitchPixels + displayPixels) / 1e6:0.0} MPix per frame");
            text.AppendLine($"  geometry    {Visible(cameras, display, proxies)}");
            text.AppendLine("  note        the four pitch loops are the real cameras; the eight display loops are a " +
                            "proxy with no MiddleVR in the process. Desk GPU, not the CAVE PC.");

            Debug.Log(text.ToString());

            // Also to disk. A console entry is fine to read once and impossible to diff against the run
            // before it, and the whole point of a budget is the direction it moves between milestones.
            try
            {
                string name = ArgValue("-budgetOut") ?? "FrameBudget.txt";
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../" + name));
                File.WriteAllText(path, text.ToString());
                Debug.Log($"[Stadium3D] Frame budget written to {path}");
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[Stadium3D] Could not write the frame budget report: {e.Message}");
            }
        }

        static string Row(Sample s) => $"{s.Label,-40} {s.Median,7:0.00} {s.Min,8:0.00} {s.P95,8:0.00}";

        static string Verdict(double ms, double budget) =>
            ms <= budget
                ? $"fits, {budget - ms:0.00} ms spare ({ms / budget * 100:0}% used)"
                : $"OVER by {ms - budget:0.00} ms ({ms / budget * 100:0}% of budget)";

        /// <summary>
        /// Renderers actually submitted, counted from the frustum rather than from a draw-call counter.
        /// </summary>
        /// <remarks>
        /// Scene complexity is the half of a perf number that survives a change of GPU, so it is worth
        /// recording next to the milliseconds: a figure that doubles between milestones explains a
        /// regression that a bare millisecond total only reports.
        /// </remarks>
        static string Visible(PitchCameraBase[] cameras, Camera display, List<ProxyTarget> proxies)
        {
            var parts = new List<string>();
            foreach (var cam in cameras) parts.Add($"{cam.name} {CountVisible(cam.Camera)}");

            display.targetTexture = proxies[0].Texture;
            display.transform.rotation = proxies[0].Rotation;
            parts.Add($"display-proxy {CountVisible(display)}");

            return string.Join(", ", parts) + " renderers in frustum";
        }

        static int CountVisible(Camera camera)
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            int n = 0;

            foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if ((camera.cullingMask & (1 << r.gameObject.layer)) == 0) continue;
                if (GeometryUtility.TestPlanesAABB(planes, r.bounds)) n++;
            }
            return n;
        }
    }
}
