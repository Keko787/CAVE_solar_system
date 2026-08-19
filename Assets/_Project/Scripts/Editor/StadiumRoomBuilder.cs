using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Stadium3D.EditorTools
{
    /// <summary>
    /// One-click builders for the CAVE room shell, based on the existing <c>CAVE_Room</c>
    /// geometry (walls + floor) authored in the BootMiddleVR scene. Prefer the prefab path: run
    /// "Save CAVE_Room As Prefab" once, then "CAVE Room Walls" stamps it into any scene.
    /// </summary>
    public static class StadiumRoomBuilder
    {
        const string SourceName = "CAVE_Room";
        const string PrefabDir = "Assets/_Project/Prefabs/Environment";
        const string PrefabPath = PrefabDir + "/CAVE_Room.prefab";

        [MenuItem("Stadium3D/Build/CAVE Room Walls", priority = 0)]
        public static void BuildCaveRoomWalls()
        {
            GameObject room;
            string via;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
            {
                room = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                via = "prefab";
            }
            else
            {
                var source = FindInOpenScenes(SourceName);
                if (source == null)
                {
                    EditorUtility.DisplayDialog(
                        "Stadium3D — CAVE Room",
                        "Couldn't find a CAVE_Room to build from.\n\n" +
                        $"No prefab at:\n{PrefabPath}\n\n" +
                        $"and no GameObject named \"{SourceName}\" in an open scene.\n\n" +
                        "Open a scene that has it (e.g. BootMiddleVR) and run\n" +
                        "Stadium3D ▸ Environment ▸ Save \"CAVE_Room\" As Prefab first.",
                        "OK");
                    return;
                }

                room = Object.Instantiate(source);
                via = "clone of the open-scene CAVE_Room (tip: run Stadium3D ▸ Environment ▸ Save \"CAVE_Room\" As Prefab to reuse it in other scenes)";
            }

            room.name = "CAVE_Room";
            Undo.RegisterCreatedObjectUndo(room, "Build CAVE Room Walls");
            Selection.activeGameObject = room;
            EditorGUIUtility.PingObject(room);
            SceneView.FrameLastActiveSceneView();
            EditorSceneManager.MarkSceneDirty(room.scene);
            Debug.Log($"[Stadium3D] Built 'CAVE_Room' from {via}.", room);
        }

        [MenuItem("Stadium3D/Environment/Save \"CAVE_Room\" As Prefab", priority = 100)]
        public static void SaveCaveRoomAsPrefab()
        {
            var source = FindInOpenScenes(SourceName);
            if (source == null)
            {
                EditorUtility.DisplayDialog("Stadium3D",
                    $"No GameObject named \"{SourceName}\" in an open scene.\n\n" +
                    "Open BootMiddleVR (or any scene with the CAVE room) first.", "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder(PrefabDir))
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Environment");

            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(source, PrefabPath, InteractionMode.UserAction);
            if (prefab != null)
            {
                EditorGUIUtility.PingObject(prefab);
                Debug.Log($"[Stadium3D] Saved {SourceName} → {PrefabPath}.", prefab);
            }
            else
            {
                Debug.LogError($"[Stadium3D] Failed to save {SourceName} as a prefab.");
            }
        }

        static GameObject FindInOpenScenes(string name)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name)
                    return t.gameObject;
            return null;
        }
    }
}
