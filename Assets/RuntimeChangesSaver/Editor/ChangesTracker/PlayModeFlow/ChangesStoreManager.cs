using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RuntimeChangesSaver.Editor.ChangesTracker.PlayModeFlow
{
    public static class ChangesStoreManager
    {
        public static void RemoveChangesForSceneFromStore(string targetScenePath, TransformChangesStore tStore, ComponentChangesStore cStore)
        {
            targetScenePath = SceneAndPathUtilities.NormalizeScenePath(targetScenePath);
            
            Debug.Log($"[PlayOverrides][RemoveChangesForSceneFromStore] Removing changes for scene '{targetScenePath}'");

            if (tStore != null && tStore.changes.Count > 0)
            {
                var transformChangesToRemove = tStore.changes.FindAll(c => 
                    string.Equals(SceneAndPathUtilities.NormalizeScenePath(c.scenePath), targetScenePath, StringComparison.OrdinalIgnoreCase));
                
                foreach (var change in transformChangesToRemove)
                {
                    tStore.changes.Remove(change);
                    Debug.Log($"[PlayOverrides][RemoveChangesForSceneFromStore] Removed Transform change for '{change.objectPath}'");
                }
            }

            if (cStore != null && cStore.changes.Count > 0)
            {
                var componentChangesToRemove = cStore.changes.FindAll(c => 
                    string.Equals(SceneAndPathUtilities.NormalizeScenePath(c.scenePath), targetScenePath, StringComparison.OrdinalIgnoreCase));
                
                foreach (var change in componentChangesToRemove)
                {
                    cStore.changes.Remove(change);
                    Debug.Log($"[PlayOverrides][RemoveChangesForSceneFromStore] Removed Component change for '{change.objectPath}' (type: {change.componentType})");
                }
            }

            if (tStore != null)
                EditorUtility.SetDirty(tStore);
            if (cStore != null)
                EditorUtility.SetDirty(cStore);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[PlayOverrides][RemoveChangesForSceneFromStore] Changes for scene '{targetScenePath}' have been removed and saved");
        }
    }
}
