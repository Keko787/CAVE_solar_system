using Stadium3D.CaveBox;
using Stadium3D.Operators;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Stadium3D.EditorTools
{
    /// <summary>
    /// One-click rigs for the secondary operator: a <see cref="GamepadProbe"/> for measuring a pad, and a
    /// <see cref="GamepadFocusCursor"/> for actually driving the menus with one.
    ///
    /// Both attach to a <b>single shared</b> pad source. Only one source may hold the secondary role — a
    /// second one displaces the first and warns — so the rigs deliberately reuse whatever is already in the
    /// scene rather than each bringing its own.
    /// </summary>
    public static class GamepadProbeBuilder
    {
        [MenuItem("Stadium3D/Build/Gamepad Probe Rig", priority = 33)]
        public static void BuildGamepadProbeRig()
        {
            const string undo = "Build Gamepad Probe Rig";

            var probe = Object.FindAnyObjectByType<GamepadProbe>();
            if (probe == null)
            {
                var host = EnsureGamepadSource(undo);
                probe = Undo.AddComponent<GamepadProbe>(host);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            Selection.activeObject = probe.gameObject;
            EditorGUIUtility.PingObject(probe.gameObject);
            Debug.Log("[Stadium3D] Gamepad probe rig ready. Press Play, click ONCE inside the Game view to give " +
                      "it focus, then press every button and push every stick and trigger. The Console " +
                      "prints 'button N PRESSED' and 'axis N = v' — those numbers are what a GamepadMap " +
                      "gets filled in with.\n" +
                      "Watch for two things in particular: whether the D-pad shows up as four buttons or as " +
                      "one axis that jumps as you rotate it (a POV hat), and whether the two triggers move " +
                      "one axis each or fight over a single shared axis.\n" +
                      "If nothing at all appears, read the 'heartbeat' line the probe logs every couple of " +
                      "seconds — it says whether the Input System can see the pad move at all.", probe);
        }

        [MenuItem("Stadium3D/Build/Gamepad Operator Rig", priority = 34)]
        public static void BuildGamepadOperatorRig()
        {
            const string undo = "Build Gamepad Operator Rig";

            var cursor = Object.FindAnyObjectByType<GamepadFocusCursor>();
            if (cursor == null)
            {
                var host = EnsureGamepadSource(undo);
                cursor = Undo.AddComponent<GamepadFocusCursor>(host);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            Selection.activeObject = cursor.gameObject;
            EditorGUIUtility.PingObject(cursor.gameObject);
            Debug.Log("[Stadium3D] Gamepad operator rig ready — press Play, click ONCE inside the Game view to " +
                      "give it focus, then steer with the D-pad or left stick. An amber ring marks the " +
                      "focused chip; A clicks it, B drops focus, and the bumpers move between panels. The " +
                      "wand keeps working at the same time and neither cursor clears the other's " +
                      "highlight.\n" +
                      "Nothing appears until the first direction press — focus is brought in deliberately " +
                      "rather than parking a ring on a chip you did not choose.", cursor);
        }

        /// <summary>
        /// Read the pad through MiddleVR instead of the Input System.
        /// </summary>
        /// <remarks>
        /// Needed when MiddleVR's <c>vrDriverDirectInput</c> has acquired the pad — which it does in this
        /// project, and which is visible as gamepad presses arriving as <b>wand</b> buttons (a swarm goal
        /// issued, a chip clicked through <c>WandPointer</c>) while the Input System reports the device
        /// present, enabled, and sending nothing. Two subsystems in one process cannot both own a
        /// DirectInput device, so the fix is to stop competing and read it from the side that won.
        ///
        /// Expect different numbers afterwards: DirectInput indices are not the canonical ones, so run the
        /// probe again and fill in a <see cref="GamepadMap"/> from what it logs.
        /// </remarks>
        [MenuItem("Stadium3D/Build/Gamepad Backend — MiddleVR (DirectInput)", priority = 35)]
        public static void SwitchToMiddleVrBackend()
        {
            var unity = Object.FindAnyObjectByType<UnityGamepadOperatorInput>();
            var host = unity != null ? unity.gameObject
                                     : Object.FindAnyObjectByType<MvrJoystickOperatorInput>()?.gameObject;
            if (host == null)
            {
                Debug.LogWarning("[Stadium3D] No gamepad rig in this scene — run Stadium3D/Build/Gamepad Probe Rig first.");
                return;
            }

            if (unity != null)
                Undo.DestroyObjectImmediate(unity);
            if (host.GetComponent<MvrJoystickOperatorInput>() == null)
                Undo.AddComponent<MvrJoystickOperatorInput>(host);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeObject = host;
            Debug.Log("[Stadium3D] Secondary operator now reads the pad through MiddleVR. Press Play and press " +
                      "each control — the indices will differ from the Input System's, so this is a fresh " +
                      "measurement, not the one already taken.", host);
        }

        /// <summary>Read the pad through Unity's Input System — the desk default, when nothing else holds it.</summary>
        [MenuItem("Stadium3D/Build/Gamepad Backend — Unity Input System", priority = 36)]
        public static void SwitchToUnityBackend()
        {
            var mvr = Object.FindAnyObjectByType<MvrJoystickOperatorInput>();
            var host = mvr != null ? mvr.gameObject
                                   : Object.FindAnyObjectByType<UnityGamepadOperatorInput>()?.gameObject;
            if (host == null)
            {
                Debug.LogWarning("[Stadium3D] No gamepad rig in this scene — run Stadium3D/Build/Gamepad Probe Rig first.");
                return;
            }

            if (mvr != null)
                Undo.DestroyObjectImmediate(mvr);
            if (host.GetComponent<UnityGamepadOperatorInput>() == null)
                Undo.AddComponent<UnityGamepadOperatorInput>(host);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeObject = host;
            Debug.Log("[Stadium3D] Secondary operator now reads the pad through Unity's Input System.", host);
        }

        /// <summary>
        /// The one GameObject carrying the secondary operator's pad source, created if this scene has none.
        /// Reused by both rigs: two sources claiming one role means the second silently displaces the first.
        /// </summary>
        static GameObject EnsureGamepadSource(string undo)
        {
            var mvr = Object.FindAnyObjectByType<MvrJoystickOperatorInput>();
            if (mvr != null)
                return mvr.gameObject;

            var existing = Object.FindAnyObjectByType<UnityGamepadOperatorInput>();
            if (existing != null)
                return existing.gameObject;

            var go = new GameObject("GamepadOperator", typeof(UnityGamepadOperatorInput));
            Undo.RegisterCreatedObjectUndo(go, undo);
            return go;
        }
    }
}
