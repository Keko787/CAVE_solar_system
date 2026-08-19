using Stadium3D.CaveBox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Stadium3D.EditorTools
{
    /// <summary>
    /// Creates the <see cref="RegionPanelGroup"/> that makes the CAVE's front wall move as one surface: the map,
    /// the Routes overview, the Fleet manager, and the mission tables all travel to the floor together on a single
    /// wand gesture, instead of the map going alone and leaving the other three on the wall.
    ///
    /// Nothing is wired into it. The group adopts its members at runtime by the region each panel starts in, which
    /// matters because every one of those four panels is built by a separate rig that destroys and recreates it —
    /// a wired member list would silently lose a panel the first time its rig was re-run. So this builder only has
    /// to guarantee the group object exists, and <see cref="Ensure"/> is called from the Routes and Fleet rigs
    /// (the two that gained floor regions) so whichever the operator runs last brings it in.
    /// </summary>
    public static class FrontWallGroupBuilder
    {
        const string GroupName = "FrontWallPanelGroup";

        [MenuItem("Stadium3D/Build/Front Wall Panel Group", priority = 57)]
        public static void BuildMenu()
        {
            const string undo = "Build Front Wall Panel Group";
            var group = Ensure(undo);
            Selection.activeGameObject = group.gameObject;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Stadium3D] Front wall panel group ready. The front-wall panels now " +
                "move to the floor together on the wand's move button — press it aimed at any of them. Re-run " +
                "Stadium3D ▸ Build ▸ Routes Rig and Fleet Rig if those two still stay on the wall: their wall⇄floor " +
                "cycles live in their prefabs and are rebuilt there.", group);
        }

        /// <summary>Idempotent: returns the scene's group, creating it if there isn't one.</summary>
        internal static RegionPanelGroup Ensure(string undo)
        {
            var existing = Object.FindAnyObjectByType<RegionPanelGroup>(FindObjectsInactive.Include);
            if (existing != null)
                return existing;

            var go = new GameObject(GroupName, typeof(RegionPanelGroup));
            Undo.RegisterCreatedObjectUndo(go, undo);
            return go.GetComponent<RegionPanelGroup>();
        }
    }
}
