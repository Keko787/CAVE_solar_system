using System.IO;
using UnityEditor;
using UnityEngine;
using Stadium3D.Football;
using Stadium3D.Pitch;

namespace Stadium3D.EditorTools
{
    /// <summary>
    /// Renders stills of the pitch from the editor, including in batch mode.
    /// </summary>
    /// <remarks>
    /// This exists because the M2 acceptance question — "does the play actually read?" — is the one thing no
    /// EditMode test can answer. The geometry tests prove the paint does not overlap and the sim tests prove
    /// the bodies are where the drive says; neither tells you whether a viewer can follow the football. A
    /// still from the broadcast angle at a known instant does, and it can be produced headlessly, which
    /// matters because <c>MVRManagerScript</c> disables the Game-view camera in the CAVE scene.
    ///
    /// <para>It also doubles as a regression surface later: the same three shots at the same three play
    /// times, before and after a content edit, make an unintended change to a route obvious.</para>
    /// </remarks>
    public static class PitchCapture
    {
        const int Width = 1600;
        const int Height = 1000;   // 1.600, the front-wall and floor aspect

        /// <summary>
        /// Batch entry point: build the rig, advance the drive to a few interesting instants, and write a PNG
        /// for each. Invoked as <c>-executeMethod Stadium3D.EditorTools.PitchCapture.CaptureBatch</c>.
        /// </summary>
        public static void CaptureBatch()
        {
            string outDir = ArgValue("-captureOut") ?? Path.Combine(Application.dataPath, "../CaptureOutput");
            Directory.CreateDirectory(outDir);

            PitchRigBuilder.BuildSandboxScene();

            var driver = Object.FindObjectOfType<FootballSimDriver>();
            if (driver == null)
            {
                Debug.LogError("[Stadium3D] No FootballSimDriver in the sandbox scene — nothing to capture.");
                EditorApplication.Exit(2);
                return;
            }

            // Awake does not run on a scene object outside play mode, so bind the sim by hand.
            var sim = new FootballSim(ScriptedDrive.Build());
            var markers = Object.FindObjectsOfType<PlayerMarker>(true);
            var ball = Object.FindObjectOfType<BallMarker>(true);

            var cam = BuildCaptureCamera();

            // Three moments that say different things: the formation before the snap, the ball in the air on
            // the hero play, and the catch.
            Shoot(sim, markers, ball, cam, playIndex: 0, time: 0.0f, outDir, "01-presnap-inside-zone");
            Shoot(sim, markers, ball, cam, playIndex: 1, time: 2.9f, outDir, "02-dagger-ball-in-air");
            Shoot(sim, markers, ball, cam, playIndex: 5, time: 3.4f, outDir, "03-four-verticals-deep-ball");
            Shoot(sim, markers, ball, cam, playIndex: 5, time: 5.4f, outDir, "04-four-verticals-catch");

            Object.DestroyImmediate(cam.gameObject);
            Debug.Log($"[Stadium3D] Wrote pitch captures to {outDir}");
            EditorApplication.Exit(0);
        }

        static Camera BuildCaptureCamera()
        {
            var go = new GameObject("~PitchCaptureCamera");
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.07f);
            cam.fieldOfView = 34f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 500f;
            cam.cullingMask = PitchLayers.BroadcastMask | PitchLayers.Mask(PitchLayers.Tactical);
            return cam;
        }

        static void Shoot(FootballSim sim, PlayerMarker[] markers, BallMarker ball, Camera cam,
                          int playIndex, float time, string outDir, string label)
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

            // Frame the ball from the near sideline, the way the broadcast camera will in M3.
            Vector3 target = sim.Ball.Position;
            cam.transform.position = target + new Vector3(-14f, 13f, -30f);
            cam.transform.LookAt(target + Vector3.up * 1.2f);

            // In edit mode the driver's OnEnable has not run, so nothing is subscribed to Camera.onPreCull.
            // Orient the numbers by hand for this shot, or every player facing away renders mirrored.
            for (int m = 0; m < markers.Length; m++)
                markers[m].FaceNumberAt(cam);

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4,
                name = "PitchCapture"
            };
            cam.targetTexture = rt;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var png = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            png.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            png.Apply();
            RenderTexture.active = prev;

            string path = Path.Combine(outDir, label + ".png");
            File.WriteAllBytes(path, png.EncodeToPNG());

            cam.targetTexture = null;
            Object.DestroyImmediate(png);
            rt.Release();
            Object.DestroyImmediate(rt);

            var s = sim.Situation;
            Debug.Log($"[Stadium3D] {label}: {s.DownAndDistance()} at {s.BallOn()}, ball {sim.Ball.Carriage} -> {path}");
        }

        static string ArgValue(string flag)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == flag) return args[i + 1];
            return null;
        }
    }
}
