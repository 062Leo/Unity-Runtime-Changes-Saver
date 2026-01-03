using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RuntimeChangesSaver.Editor.ChangesTracker.Serialization;

namespace RuntimeChangesSaver.Editor.ChangesTracker.PlayModeFlow
{
    public static class SceneApplyProcessor
    {
        public static void ProcessNextSceneInQueue(List<string> remainingScenes, string startScenePath, TransformChangesStore tStore, ComponentChangesStore cStore, GameObjectNameChangesStore nameStore)
        {
            Debug.Log($"[PlayOverrides][ProcessNextSceneInQueue] ENTER remainingScenes=[{string.Join(", ", remainingScenes)}], startScenePath='{startScenePath}'");

            if (remainingScenes.Count == 0)
            {
                Debug.Log("[PlayOverrides][ProcessNextSceneInQueue] No remaining scenes, calling CheckReturnToStartScene...");
                CheckReturnToStartScene(startScenePath);
                return;
            }

            string currentPath = remainingScenes[0];
            remainingScenes.RemoveAt(0);

            string activePath = SceneAndPathUtilities.NormalizeScenePath(SceneManager.GetActiveScene().path);
            Debug.Log($"[PlayOverrides][ProcessNextSceneInQueue] activePath='{activePath}', currentPath='{currentPath}'");

            // If we are not in the target scene, ask if we want to switch
            if (!string.Equals(activePath, currentPath, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[PlayOverrides][ProcessNextSceneInQueue] Active scene != target. Showing Scene Switch dialog for '{currentPath}'");
                bool switchScene = EditorUtility.DisplayDialog("Scene Switch", 
                    $"Switch to scene?\n\n{currentPath}", "Yes", "Discard remaining");

                Debug.Log($"[PlayOverrides][ProcessNextSceneInQueue] Scene Switch dialog result switchScene={switchScene}");

                if (switchScene)
                {
                    Debug.Log($"[PlayOverrides][ProcessNextSceneInQueue] Saving open scenes and opening '{currentPath}'...");
                    EditorSceneManager.SaveOpenScenes();
                    EditorSceneManager.OpenScene(currentPath, OpenSceneMode.Single);
                    
                    EditorApplication.delayCall += () => 
                    {
                        Debug.Log("[PlayOverrides][ProcessNextSceneInQueue] Delay 1 after scene switch");
                        EditorApplication.delayCall += () => 
                        {
                            Debug.Log("[PlayOverrides][ProcessNextSceneInQueue] Delay 2 after scene switch");
                            EditorApplication.delayCall += () => 
                            {
                                Debug.Log("[PlayOverrides][ProcessNextSceneInQueue] Delay 3 after scene switch, repainting SceneView");
                                SceneView.RepaintAll();
                                
                                EditorApplication.delayCall += () => 
                                {
                                    Debug.Log("[PlayOverrides][ProcessNextSceneInQueue] Delay 4 after scene switch, continuing queue...");
                                    ProcessNextSceneInQueue(new List<string> { currentPath }.Concat(remainingScenes).ToList(), 
                                        startScenePath, tStore, cStore, nameStore);
                                };
                            };
                        };
                    };
                    return;
                }
                else
                {
                    Debug.Log("[PlayOverrides][ProcessNextSceneInQueue] User chose 'Discard remaining' on Scene Switch dialog. Stopping flow.");
                    PlayModeOverrideFlow.StopProcessing();
                    return;
                }
            }

            string msg = $"Apply play mode overrides for scene?\n\n{currentPath}";
            Debug.Log($"[PlayOverrides][ProcessNextSceneInQueue] Showing Apply Overrides dialog for '{currentPath}'");
            if (EditorUtility.DisplayDialog("Apply Overrides", msg, "Apply", "Discard"))
            {
                Debug.Log($"[PlayOverrides][ProcessNextSceneInQueue] User chose APPLY for '{currentPath}'. Calling ApplyChangesFromStoreToEditModeForScene...");
                ApplyChangesFromStoreToEditModeForScene(currentPath, tStore, cStore);
                ApplyNameChangesFromStoreToEditModeForScene(currentPath);
                
                EditorApplication.delayCall += () => 
                {
                    Debug.Log("[PlayOverrides][ProcessNextSceneInQueue] Apply delay 1 (after ApplyChangesFromStoreToEditModeForScene)");
                    EditorApplication.delayCall += () => 
                    {
                        Debug.Log("[PlayOverrides][ProcessNextSceneInQueue] Apply delay 2 (SaveAssets/Refresh)");
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                        
                        EditorApplication.delayCall += () => 
                        {
                            Debug.Log("[PlayOverrides][ProcessNextSceneInQueue] Apply delay 3 (RepaintAll/DirtyHierarchy)");
                            SceneView.RepaintAll();
                            EditorApplication.DirtyHierarchyWindowSorting();
                            
                            EditorApplication.delayCall += () => 
                            {
                                Debug.Log("[PlayOverrides][ProcessNextSceneInQueue] Apply delay 4 (continue with remaining scenes)");
                                ProcessNextSceneInQueue(remainingScenes, startScenePath, tStore, cStore, nameStore);
                            };
                        };
                    };
                };
            }
            else
            {
                Debug.Log($"[PlayOverrides][ProcessNextSceneInQueue] User chose DISCARD for '{currentPath}'. Removing changes from store for this scene...");
                ChangesStoreManager.RemoveChangesForSceneFromStore(currentPath, tStore, cStore, nameStore);
                EditorApplication.delayCall += () => ProcessNextSceneInQueue(remainingScenes, startScenePath, tStore, cStore, nameStore);
            }
        }

        public static void ApplyChangesFromStoreToEditModeForScene(string targetScenePath, TransformChangesStore transformStore, ComponentChangesStore compStore)
        {
            targetScenePath = SceneAndPathUtilities.NormalizeScenePath(targetScenePath);

            if (transformStore == null)
            {
                transformStore = TransformChangesStore.LoadExisting();
                Debug.Log($"[PlayOverrides][ApplyChangesFromStoreToEditModeForScene] transformStore was null, reloaded");
            }

            if (compStore == null)
            {
                compStore = ComponentChangesStore.LoadExisting();
                Debug.Log($"[PlayOverrides][ApplyChangesFromStoreToEditModeForScene] compStore was null, reloaded");
            }

            if (transformStore != null && transformStore.changes.Count > 0)
            {
                foreach (var change in transformStore.changes)
                {
                    var normalizedChangePath = SceneAndPathUtilities.NormalizeScenePath(change.scenePath);

                    if (!string.Equals(normalizedChangePath, targetScenePath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    Debug.Log($"[RCS][Apply][Transform] Begin scene='{normalizedChangePath}' guid='{change.globalObjectId}' path='{change.objectPath}' props={string.Join(",", change.modifiedProperties ?? new List<string>())}");

                    var scene = SceneManager.GetSceneByPath(normalizedChangePath);
                    if (!scene.IsValid())
                        scene = SceneManager.GetSceneByName(change.scenePath);

                    if (!scene.IsValid())
                    {
                        Debug.LogWarning($"[RCS][Apply][Transform] Scene not valid for path '{change.scenePath}'");
                        continue;
                    }

                    GameObject go = SceneAndPathUtilities.FindGameObjectByGuidOrPath(scene, change.globalObjectId, change.objectPath);
                    if (go == null)
                    {
                        Debug.LogWarning($"[RCS][Apply][Transform] Target not found (guid='{change.globalObjectId}', path='{change.objectPath}')");
                        continue;
                    }

                    Debug.Log($"[RCS][Apply][Transform] Target='{go.name}' scene='{go.scene.path}' path='{SceneAndPathUtilities.GetGameObjectPath(go.transform)}'");

                    Transform t = go.transform;
                    RectTransform rt = t as RectTransform;

                    Undo.RecordObject(t, "Apply Play Mode Transform Changes");

                    if (change.modifiedProperties is { Count: > 0 })
                    {
                        foreach (var prop in change.modifiedProperties)
                        {
                            ApplyPropertyToTransform(t, rt, change, prop);
                        }
                    }
                    else
                    {
                        t.localPosition = change.position;
                        t.localRotation = change.rotation;
                        t.localScale = change.scale;

                        if (rt != null && change.isRectTransform)
                        {
                            rt.anchoredPosition = change.anchoredPosition;
                            rt.anchoredPosition3D = change.anchoredPosition3D;
                            rt.anchorMin = change.anchorMin;
                            rt.anchorMax = change.anchorMax;
                            rt.pivot = change.pivot;
                            rt.sizeDelta = change.sizeDelta;
                            rt.offsetMin = change.offsetMin;
                            rt.offsetMax = change.offsetMax;
                        }
                    }

                    EditorUtility.SetDirty(go);
                    if (scene.IsValid())
                        EditorSceneManager.MarkSceneDirty(scene);

                    Debug.Log($"[RCS][Apply][Transform] Applied to '{go.name}'");
                }
            }

            if (compStore != null && compStore.changes.Count > 0)
            {
                foreach (var change in compStore.changes)
                {
                    var normalizedChangePath = SceneAndPathUtilities.NormalizeScenePath(change.scenePath);

                    if (!string.Equals(normalizedChangePath, targetScenePath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    Debug.Log($"[RCS][Apply][Component] Begin scene='{normalizedChangePath}' guid='{change.globalObjectId}' path='{change.objectPath}' type='{change.componentType}' idx={change.componentIndex}");

                    var scene = SceneManager.GetSceneByPath(normalizedChangePath);
                    if (!scene.IsValid())
                        scene = SceneManager.GetSceneByName(change.scenePath);

                    if (!scene.IsValid())
                    {
                        Debug.LogWarning($"[RCS][Apply][Component] Scene not valid for path '{change.scenePath}'");
                        continue;
                    }

                    GameObject go = SceneAndPathUtilities.FindGameObjectByGuidOrPath(scene, change.globalObjectId, change.objectPath);
                    if (go == null)
                    {
                        Debug.LogWarning($"[RCS][Apply][Component] Target not found (guid='{change.globalObjectId}', path='{change.objectPath}')");
                        continue;
                    }

                    Debug.Log($"[RCS][Apply][Component] Target='{go.name}' scene='{go.scene.path}' path='{SceneAndPathUtilities.GetGameObjectPath(go.transform)}'");

                    var type = Type.GetType(change.componentType);
                    if (type == null)
                    {
                        Debug.LogWarning($"[RCS][Apply][Component] Type not found '{change.componentType}'");
                        continue;
                    }

                    var allComps = go.GetComponents(type);
                    if (change.componentIndex < 0 || change.componentIndex >= allComps.Length)
                    {
                        Debug.LogWarning($"[RCS][Apply][Component] Component index out of range idx={change.componentIndex} len={allComps.Length}");
                        continue;
                    }

                    var comp = allComps[change.componentIndex];
                    if (comp == null)
                    {
                        Debug.LogWarning($"[RCS][Apply][Component] Component null at idx={change.componentIndex}");
                        continue;
                    }

                    SerializedObject so = new SerializedObject(comp);
                    Undo.RecordObject(comp, "Apply Play Mode Component Changes");

                    for (int i = 0; i < change.propertyPaths.Count; i++)
                    {
                        string path = change.propertyPaths[i];
                        string value = change.serializedValues[i];
                        string typeName = change.valueTypes[i];

                        SerializedProperty prop = so.FindProperty(path);
                        if (prop == null)
                            continue;

                        ComponentPropertySerializer.ApplyPropertyValue(prop, typeName, value);
                    }

                    so.ApplyModifiedProperties();

                    if (change.includeMaterialChanges && comp is Renderer renderer)
                    {
                        ApplyMaterials(renderer, change.materialGuids);
                    }
                    EditorUtility.SetDirty(comp);
                    if (scene.IsValid())
                        EditorSceneManager.MarkSceneDirty(scene);

                    Debug.Log($"[RCS][Apply][Component] Applied to '{go.name}' component='{comp.GetType().Name}' idx={change.componentIndex}");
                }
            }

            Debug.Log("[PlayOverrides][ApplyChangesFromStoreToEditModeForScene] LEAVE");
        }

        private static void ApplyPropertyToTransform(Transform t, RectTransform rt, TransformChangesStore.TransformChange change, string prop)
        {
            switch (prop)
            {
                case "position": t.localPosition = change.position; break;
                case "rotation": t.localRotation = change.rotation; break;
                case "scale": t.localScale = change.scale; break;
                case "anchoredPosition": if (rt) rt.anchoredPosition = change.anchoredPosition; break;
                case "anchoredPosition3D": if (rt) rt.anchoredPosition3D = change.anchoredPosition3D; break;
                case "anchorMin": if (rt) rt.anchorMin = change.anchorMin; break;
                case "anchorMax": if (rt) rt.anchorMax = change.anchorMax; break;
                case "pivot": if (rt) rt.pivot = change.pivot; break;
                case "sizeDelta": if (rt) rt.sizeDelta = change.sizeDelta; break;
                case "offsetMin": if (rt) rt.offsetMin = change.offsetMin; break;
                case "offsetMax": if (rt) rt.offsetMax = change.offsetMax; break;
            }
        }

        private static void CheckReturnToStartScene(string startPath)
        {
            string currentPath = SceneAndPathUtilities.NormalizeScenePath(SceneManager.GetActiveScene().path);
            if (!string.IsNullOrEmpty(startPath) && !string.Equals(currentPath, startPath, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[PlayOverrides][CheckReturnToStartScene] currentPath='{currentPath}', startPath='{startPath}' -> showing return dialog");
                if (EditorUtility.DisplayDialog("Return to start scene?", $"Do you want to return to:\n\n{startPath}", "Yes", "No"))
                {
                    Debug.Log("[PlayOverrides][CheckReturnToStartScene] User chose YES, opening start scene...");
                    EditorSceneManager.SaveOpenScenes();
                    EditorSceneManager.OpenScene(startPath, OpenSceneMode.Single);
                    
                    EditorApplication.delayCall += () => 
                    {
                        EditorApplication.delayCall += () => 
                        {
                            EditorApplication.delayCall += () => 
                            {
                                SceneView.RepaintAll();
                                Debug.Log("[PlayOverrides][CheckReturnToStartScene] Start scene loaded and repainted. Flow finished.");
                                PlayModeOverrideFlow.StopProcessing();
                            };
                        };
                    };
                    return;
                }
            }
            Debug.Log("[PlayOverrides][CheckReturnToStartScene] No return to start scene needed or user chose NO. Flow finished.");
            PlayModeOverrideFlow.StopProcessing();
        }

        private static void ApplyMaterials(Renderer renderer, List<string> materialGuids)
        {
            if (renderer == null || materialGuids == null)
                return;

            int targetCount = materialGuids.Count;
            if (targetCount == 0)
                return;

            var current = renderer.sharedMaterials;
            var applied = new Material[targetCount];

            for (int i = 0; i < targetCount; i++)
            {
                string guid = materialGuids[i];
                if (string.IsNullOrEmpty(guid))
                {
                    applied[i] = null;
                    continue;
                }

                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null)
                {
                    if (i < current.Length)
                        applied[i] = current[i];
                    Debug.LogWarning($"[RCS][Apply][Component] Material GUID '{guid}' could not be resolved for renderer '{renderer.name}'");
                }
                else
                {
                    applied[i] = material;
                }
            }

            renderer.sharedMaterials = applied;
        }

        public static void ApplyNameChangesFromStoreToEditModeForScene(string targetScenePath)
        {
            targetScenePath = SceneAndPathUtilities.NormalizeScenePath(targetScenePath);

            var nameStore = GameObjectNameChangesStore.LoadExisting();
            if (nameStore == null || nameStore.changes.Count == 0)
                return;

            foreach (var change in nameStore.changes)
            {
                var normalizedChangePath = SceneAndPathUtilities.NormalizeScenePath(change.scenePath);

                if (!string.Equals(normalizedChangePath, targetScenePath, StringComparison.OrdinalIgnoreCase))
                    continue;

                Debug.Log($"[RCS][Apply][Name] Begin scene='{normalizedChangePath}' guid='{change.globalObjectId}' path='{change.objectPath}' newName='{change.newName}'");

                var scene = SceneManager.GetSceneByPath(normalizedChangePath);
                if (!scene.IsValid())
                    scene = SceneManager.GetSceneByName(change.scenePath);

                if (!scene.IsValid())
                {
                    Debug.LogWarning($"[RCS][Apply][Name] Scene not valid for path '{change.scenePath}'");
                    continue;
                }

                GameObject go = SceneAndPathUtilities.FindGameObjectByGuidOrPath(scene, change.globalObjectId, change.objectPath);
                if (go == null)
                {
                    Debug.LogWarning($"[RCS][Apply][Name] Target not found (guid='{change.globalObjectId}', path='{change.objectPath}')");
                    continue;
                }

                Debug.Log($"[RCS][Apply][Name] Target='{go.name}' -> Renaming to '{change.newName}'");

                Undo.RecordObject(go, "Apply Play Mode Name Changes");
                go.name = change.newName;
                EditorUtility.SetDirty(go);
                if (scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(scene);

                Debug.Log($"[RCS][Apply][Name] Applied to '{go.name}'");
            }
        }
    }
}
