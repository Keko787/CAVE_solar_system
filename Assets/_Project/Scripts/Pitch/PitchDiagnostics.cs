using System.Collections;
using System.Text;
using UnityEngine;
using Stadium3D.CaveBox;

namespace Stadium3D.Pitch
{
    /// <summary>
    /// Dumps the real state of the wall-rendering chain a few frames into play mode.
    /// </summary>
    /// <remarks>
    /// Written because a blank wall has too many possible causes to guess at, and guessing had already cost
    /// two wrong fixes. Every link in the chain can fail silently and they all look identical from the room:
    /// the camera disabled, the render target never created, the culling mask empty, the panel unbound, the
    /// material missing its texture, the panel facing the wrong way, or the MiddleVR display cameras not
    /// carrying the panel's layer at all.
    ///
    /// <para>It installs itself at runtime rather than living in the scene, so diagnosing a build needs no
    /// scene edit and no rebuild — just press Play and read the console. It logs once and then removes
    /// itself.</para>
    /// </remarks>
    public class PitchDiagnostics : MonoBehaviour
    {
        /// <summary>Frames to wait before sampling, so every Awake and the first render have happened.</summary>
        const int SettleFrames = 5;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var go = new GameObject("~PitchDiagnostics") { hideFlags = HideFlags.DontSave };
            go.AddComponent<PitchDiagnostics>();
            DontDestroyOnLoad(go);
        }

        IEnumerator Start()
        {
            for (int i = 0; i < SettleFrames; i++) yield return null;

            var sb = new StringBuilder(2048);
            sb.AppendLine("[Stadium3D] ── pitch render chain ─────────────────────────────────────");

            // ── layers ─────────────────────────────────────────────────────────────────────────────
            sb.AppendLine($"layers: Shared={L(PitchLayers.Shared)} Players={L(PitchLayers.Players)} " +
                          $"Numbers={L(PitchLayers.Numbers)} CaveScreen={L(PitchLayers.CaveScreen)} " +
                          $"Hologram={L(PitchLayers.Hologram)}");

            // ── the sim ────────────────────────────────────────────────────────────────────────────
            var driver = FootballSimDriver.Instance;
            sb.AppendLine(driver == null
                ? "driver: MISSING - no FootballSimDriver.Instance, so no camera ever tracks"
                : $"driver: ok, sim={(driver.Sim == null ? "NULL" : "bound")}, " +
                  $"frames={(driver.Sim == null ? 0 : driver.Sim.Frames.Count)}");

            // ── the pitch cameras ──────────────────────────────────────────────────────────────────
            var cams = FindObjectsOfType<PitchCameraBase>(true);
            sb.AppendLine($"pitch cameras: {cams.Length}");
            foreach (var c in cams)
            {
                var cam = c.Camera;
                var rt = c.View;
                sb.AppendLine(
                    $"  {c.name,-22} behaviour={(c.enabled ? "on " : "OFF")} " +
                    $"camera={(cam == null ? "NULL" : cam.enabled ? "on " : "OFF")} " +
                    $"go={(c.gameObject.activeInHierarchy ? "active" : "INACTIVE")} " +
                    $"rt={(rt == null ? "NULL" : $"{rt.width}x{rt.height} created={rt.IsCreated()}")} " +
                    $"target={(cam != null && cam.targetTexture != null ? "set" : "NULL")} " +
                    $"mask=0x{(cam == null ? 0 : cam.cullingMask):X8} " +
                    $"local={Fmt(c.transform.localPosition)} world={Fmt(c.transform.position)}");
            }

            // ── the panels ─────────────────────────────────────────────────────────────────────────
            var panels = FindObjectsOfType<WallPanel>(true);
            sb.AppendLine($"wall panels: {panels.Length}");
            foreach (var p in panels)
            {
                var screen = p.GetComponentInChildren<Renderer>(true);
                var mat = screen != null ? screen.sharedMaterial : null;
                sb.AppendLine(
                    $"  {p.name,-22} source={(p.Source == null ? "NULL" : p.Source.name)} " +
                    $"screen={(screen == null ? "NULL" : screen.enabled ? "on " : "OFF")} " +
                    $"layer={(screen == null ? "-" : LayerMask.LayerToName(screen.gameObject.layer))} " +
                    $"shader={(mat == null ? "NO MATERIAL" : mat.shader != null ? mat.shader.name : "NULL SHADER")} " +
                    $"tex={(mat == null ? "-" : mat.mainTexture == null ? "NONE" : mat.mainTexture.name)} " +
                    $"scale={Fmt(screen != null ? screen.transform.localScale : Vector3.zero)}");
            }

            // ── the MiddleVR display cameras ───────────────────────────────────────────────────────
            int screenLayer = LayerMask.NameToLayer(PitchLayers.CaveScreen);
            int n = 0, blind = 0;
            foreach (var cam in MvrDisplay.Cameras())
            {
                if (cam == null) continue;
                n++;
                bool sees = screenLayer >= 0 && (cam.cullingMask & (1 << screenLayer)) != 0;
                if (!sees) blind++;
                sb.AppendLine($"  display[{n}] {cam.name,-18} mask=0x{cam.cullingMask:X8} " +
                              $"seesPanels={(sees ? "YES" : "NO")}");
            }
            sb.AppendLine(n == 0
                ? "display cameras: NONE - expected outside the CAVE; in the CAVE it means MiddleVR is not up"
                : $"display cameras: {n}, of which {blind} cannot see the panel layer");

            sb.AppendLine("[Stadium3D] ───────────────────────────────────────────────────────────");
            Debug.Log(sb.ToString());

            Destroy(gameObject);
        }

        static string L(string name)
        {
            int i = LayerMask.NameToLayer(name);
            return i < 0 ? $"{name}=MISSING" : i.ToString();
        }

        static string Fmt(Vector3 v) => $"({v.x:0.0},{v.y:0.0},{v.z:0.0})";
    }
}
