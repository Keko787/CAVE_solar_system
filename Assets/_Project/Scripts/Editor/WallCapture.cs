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

            // The result beat, which is a different picture rather than the same one a moment later: the
            // team walls swap their live telemetry for the player's card, and the card is the only place
            // the touchdown that has just been scored appears. A capture set with no PostPlay frame in it
            // would never show half of what §8 asks those walls for.
            Shoot(sim, markers, ball, cameras, playIndex: 5, time: 3.4f, outDir, "result", resultBeat: true);

            ShootRoom(outDir, "room-corner", yaw: -40f);
            ShootRoom(outDir, "room-front", yaw: 0f);

            Debug.Log($"[Stadium3D] Wrote wall captures to {outDir}");
            EditorApplication.Exit(0);
        }

        static void Shoot(FootballSim sim, PlayerMarker[] markers, BallMarker ball,
                          PitchCameraBase[] cameras, int playIndex, float time, string outDir, string label,
                          bool resultBeat = false)
        {
            sim.Director.SeekToPlay(playIndex, time);
            sim.Tick(0.0);

            if (resultBeat)
            {
                // Out to the whistle, then clear of the crossfade, so the card is fully up rather than
                // caught halfway through arriving.
                for (int i = 0; i < 1200 && sim.Director.Phase == PlayPhase.Live; i++) sim.Tick(1.0 / 60.0);
                for (int i = 0; i < 45; i++) sim.Tick(1.0 / 60.0);
            }

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

            // Same reason as the trails: a team wall's card is about whoever that wall settled on, and the
            // driver that would normally carry that is not running in edit mode.
            foreach (var hud in Object.FindObjectsOfType<StatHud>(true))
            {
                if (hud is TeamWallStatHud card)
                    card.Apply(sim, card.Side == TeamSide.Home ? homeFocus : awayFocus);
                else
                    hud.Apply(sim);

                // What the readout actually holds, in numbers. Reading a wall of stylised flat colour and
                // deciding whether a label is faint, mispositioned or simply absent is exactly the judgement
                // this project keeps getting wrong; the alpha and the font size are not a judgement.
                foreach (var element in hud.Elements)
                {
                    if (element.Label == null) continue;
                    Debug.Log($"[Stadium3D] {label}/{hud.name}@{hud.transform.parent.name}: " +
                              $"{element.Label.name} a={element.Label.alpha:0.00} " +
                              $"size={element.Label.fontSize:0.000} pos={element.Label.transform.localPosition} " +
                              $"\"{element.Label.text}\"");
                }
            }

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

        /// <summary>
        /// What somebody standing in the room sees, rather than what one surface contains.
        /// </summary>
        /// <remarks>
        /// Every capture until now has been a render target — the picture a surface shows, with the room
        /// itself out of frame. That is the right check for content and no check at all for anything
        /// <em>between</em> surfaces, which is why four views could meet at a corner with nothing dividing
        /// them and no image in the project would show it. This camera carries exactly the mask
        /// <see cref="PitchIsolation"/> leaves on a display camera, so it sees the room and the panels and
        /// none of the pitch.
        ///
        /// <para>It is not a substitute for MiddleVR: the real display cameras use an off-axis frustum
        /// solved from the screen corners, so this gets the geometry and the seams right and the projection
        /// only approximately. Corners either have a border between them or they do not, and that is what
        /// this is for.</para>
        /// </remarks>
        static void ShootRoom(string outDir, string label, float yaw)
        {
            // The panels bind their render targets in LateUpdate, which does not tick in edit mode — so
            // without this every wall shows the "no signal" slate and the picture the border is meant to
            // separate is not there to look at.
            foreach (var panel in Object.FindObjectsOfType<WallPanel>(true))
            {
                var view = panel.Source != null ? panel.Source.View : null;
                if (view != null) panel.Bind(view);
            }

            var go = new GameObject("RoomCapture") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.position = new Vector3(0f, 1.2f, 0f);
            go.transform.rotation = Quaternion.Euler(8f, yaw, 0f);

            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.cullingMask = ~PitchLayers.PitchMask;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 100f;
            cam.fieldOfView = 72f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);

            var rt = new RenderTexture(2560, 1600, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing),
            };
            rt.Create();

            cam.targetTexture = rt;
            cam.Render();
            Save(rt, Path.Combine(outDir, label + ".png"));

            int mask = cam.cullingMask;

            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(go);

            Debug.Log($"[Stadium3D] {label}: room at yaw {yaw:0}, mask 0x{mask:X8}");
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
