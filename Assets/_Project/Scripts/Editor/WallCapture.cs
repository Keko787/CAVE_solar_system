using System.IO;
using UnityEditor;
using UnityEngine;
using Stadium3D.Football;
using Stadium3D.Pitch;

namespace Stadium3D.EditorTools
{
    /// <summary>
    /// Renders what each of the four CAVE surfaces actually shows, at a chosen instant of the drive.
    /// </summary>
    /// <remarks>
    /// This is the M3 acceptance check. The framing arithmetic is unit-tested and the culling masks are
    /// declarative, but neither answers the only question that matters — <em>is this legible?</em> The floor
    /// in particular rests on a specific bet: that 1:22 with a sliding window keeps formations readable where
    /// fitting the whole field would not. A still settles that in seconds; an argument does not.
    ///
    /// <para>Each image is written at its surface's own render-target size, so the aspect you see here is the
    /// aspect the projector gets. A side wall that looks nearly square is correct — 2.3876 by 2.2225 metres
    /// is 1.074, and that near-squareness is what makes a life-size standing player fit.</para>
    /// </remarks>
    public static class WallCapture
    {
        /// <summary>
        /// Batch entry point: build the CAVE scene, seek the drive, and write one image per surface.
        /// <c>-executeMethod Stadium3D.EditorTools.WallCapture.CaptureBatch</c>
        /// </summary>
        public static void CaptureBatch()
        {
            string outDir = ArgValue("-captureOut") ?? Path.Combine(Application.dataPath, "../WallCaptures");
            Directory.CreateDirectory(outDir);

            StadiumCaveBuilder.BuildScene();

            var sim = new FootballSim(ScriptedDrive.Build());
            var markers = Object.FindObjectsOfType<PlayerMarker>(true);
            var ball = Object.FindObjectOfType<BallMarker>(true);
            var cameras = Object.FindObjectsOfType<PitchCameraBase>(true);

            if (cameras.Length == 0)
            {
                Debug.LogError("[Stadium3D] No pitch cameras in the CAVE scene — nothing to capture.");
                EditorApplication.Exit(2);
                return;
            }

            foreach (var cam in cameras) cam.InitializeForPreview();

            // The hero play, just after the deep ball is released: every surface has something to say, which
            // is exactly when a four-wall layout either works or does not.
            Shoot(sim, markers, ball, cameras, playIndex: 5, time: 3.4f, outDir, "deep-ball");
            Shoot(sim, markers, ball, cameras, playIndex: 0, time: 0.0f, outDir, "presnap");

            Debug.Log($"[Stadium3D] Wrote wall captures to {outDir}");
            EditorApplication.Exit(0);
        }

        static void Shoot(FootballSim sim, PlayerMarker[] markers, BallMarker ball,
                          PitchCameraBase[] cameras, int playIndex, float time, string outDir, string label)
        {
            sim.Director.SeekToPlay(playIndex, time);
            sim.Tick(0.0);

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

            // The floor overlay follows the situation, and nothing is ticking its LateUpdate in edit mode.
            foreach (var overlay in Object.FindObjectsOfType<FieldStateOverlay>(true))
                overlay.Apply(sim.Situation);

            // Settle every camera before drawing anything that depends on where they ended up. The trails
            // are per team, not per camera, and the floor draws both — so the floor cannot be given its
            // content until the two wall cameras have chosen who they are following.
            foreach (var cam in cameras) cam.TrackForPreview(sim);

            int homeFocus = 0, awayFocus = 0;
            foreach (var cam in cameras)
            {
                if (!(cam is TeamIsoPitchCamera iso)) continue;
                if (iso.Side == TeamSide.Home) homeFocus = iso.SubjectPlayerId;
                else awayFocus = iso.SubjectPlayerId;
            }

            foreach (var trail in Object.FindObjectsOfType<RouteTrail>(true))
                trail.Apply(sim, trail.Side == TeamSide.Home ? homeFocus : awayFocus);

            foreach (var cam in cameras)
            {
                cam.TrackForPreview(sim);

                // The driver's per-camera number pass is not running in edit mode. Calling the same
                // entry point rather than FaceNumberAt directly, so a capture cannot quietly disagree
                // with the room about where the numbers are.
                JerseyNumbers.Orient(cam.Camera, markers);

                // The driver is what carries focus to the markers in play mode, and it is not running.
                if (cam is TeamIsoPitchCamera wall)
                    foreach (var marker in markers)
                        marker.SetWallFocus(wall.Side, marker.PlayerId == wall.SubjectPlayerId);

                cam.Camera.Render();
                Save(cam.View, Path.Combine(outDir, $"{label}-{Sanitize(cam.name)}.png"));

                var s = sim.Situation;
                Debug.Log($"[Stadium3D] {label}/{cam.name}: {cam.View.width}x{cam.View.height}, " +
                          $"{s.DownAndDistance()} at {s.BallOn()}, fov {cam.Camera.fieldOfView:0.0}, " +
                          $"ortho {(cam.Camera.orthographic ? cam.Camera.orthographicSize.ToString("0.0") : "-")}");
            }
        }

        static void Save(RenderTexture rt, string path)
        {
            if (rt == null) return;

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var png = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            png.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            png.Apply();
            RenderTexture.active = prev;

            File.WriteAllBytes(path, png.EncodeToPNG());
            Object.DestroyImmediate(png);
        }

        static string Sanitize(string s) => s.Replace(" ", "-").ToLowerInvariant();

        static string ArgValue(string flag)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == flag) return args[i + 1];
            return null;
        }
    }
}
