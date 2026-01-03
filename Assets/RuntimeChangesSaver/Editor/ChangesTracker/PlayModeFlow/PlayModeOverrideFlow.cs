using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RuntimeChangesSaver.Editor.ChangesTracker.PlayModeFlow
{
    public static class PlayModeOverrideFlow
    {
        private static bool isProcessingPlayExitPopups = false;

        public static bool IsProcessing => isProcessingPlayExitPopups;

        public static void StopProcessing()
        {
            isProcessingPlayExitPopups = false;
        }

        public static void HandleApplyChangesFromStoreOnPlayExit()
        {
            if (Application.isPlaying || isProcessingPlayExitPopups) return;

            var transformStore = TransformChangesStore.LoadExisting();
            var compStore = ComponentChangesStore.LoadExisting();

            bool hasTransformChanges = transformStore != null && transformStore.changes.Count > 0;
            bool hasComponentChanges = compStore != null && compStore.changes.Count > 0;

            if (!hasTransformChanges && !hasComponentChanges) return;

            isProcessingPlayExitPopups = true;

            Debug.Log($"[PlayOverrides][HandleApplyChangesFromStoreOnPlayExit] ENTER hasTransformChanges={hasTransformChanges}, hasComponentChanges={hasComponentChanges}");

            var allScenePaths = new HashSet<string>();
            if (hasTransformChanges) 
                foreach (var c in transformStore.changes) 
                    allScenePaths.Add(SceneAndPathUtilities.NormalizeScenePath(c.scenePath));
            
            if (hasComponentChanges) 
                foreach (var c in compStore.changes) 
                    allScenePaths.Add(SceneAndPathUtilities.NormalizeScenePath(c.scenePath));

            var startScene = SceneManager.GetActiveScene();
            string startScenePath = startScene.IsValid() ? SceneAndPathUtilities.NormalizeScenePath(startScene.path) : null;

            var orderedScenePaths = new List<string>(allScenePaths);
            if (!string.IsNullOrEmpty(startScenePath) && orderedScenePaths.Contains(startScenePath))
            {
                orderedScenePaths.Remove(startScenePath);
                orderedScenePaths.Insert(0, startScenePath);
            }

            Debug.Log($"[PlayOverrides][HandleApplyChangesFromStoreOnPlayExit] startScenePath='{startScenePath}', orderedScenePaths=[{string.Join(", ", orderedScenePaths)}]");

            SceneApplyProcessor.ProcessNextSceneInQueue(orderedScenePaths, startScenePath, transformStore, compStore);
        }
    }
}
