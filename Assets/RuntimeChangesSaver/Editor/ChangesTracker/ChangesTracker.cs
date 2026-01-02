using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using RuntimeChangesSaver.Editor.ChangesTracker.Serialization;
using RuntimeChangesSaver.Editor.ChangesTracker.PlayModeFlow;

namespace RuntimeChangesSaver.Editor.ChangesTracker
{
    /// <summary>
    /// Main entry point for tracking play mode changes.
    /// Coordinates snapshot management, serialization, and apply/discard workflow.
    /// </summary>
    [InitializeOnLoad]
    public static class ChangesTrackerCore
    {
        private const string PREFS_KEY = "PlayModeChangesTracker_CaptureNeeded";
        private static string startScenePathAtPlayEnter;
        private static readonly Dictionary<string, HashSet<string>> selectedProperties = new();

        static ChangesTrackerCore()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OnEditorUpdate;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnEditorUpdate()
        {
            if (!Application.isPlaying) return;
            
            bool needsCapture = EditorPrefs.GetBool(PREFS_KEY, false);
            if (!needsCapture) return;
            
            EditorPrefs.DeleteKey(PREFS_KEY);
            EditorApplication.delayCall += SnapshotManager.CaptureSnapshotsInPlayMode;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            Debug.Log($"[PlayOverrides][OnPlayModeStateChanged] state={state}, isPlaying={Application.isPlaying}");
            
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    ClearStoresOnPlayEnter();
                    var startScene = SceneManager.GetActiveScene();
                    startScenePathAtPlayEnter = startScene.IsValid() ? startScene.path : null;
                    
                    SnapshotManager.CaptureSnapshotsInEditMode();
                    EditorPrefs.SetBool(PREFS_KEY, true);
                    break;

                case PlayModeStateChange.EnteredPlayMode:
                    EditorPrefs.SetBool(PREFS_KEY, true);
                    break;

                case PlayModeStateChange.EnteredEditMode:
                    EditorPrefs.DeleteKey(PREFS_KEY);
                    HandlePlayExitFlow();
                    break;
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Application.isPlaying || !scene.isLoaded)
                return;

            ApplyRuntimeOverridesForScene(scene);
            
            // Re-capture snapshots when a new scene is loaded in play mode
            EditorApplication.delayCall += SnapshotManager.CaptureSnapshotsInPlayMode;
        }

        private static void ApplyRuntimeOverridesForScene(Scene scene)
        {
            var transformStore = TransformChangesStore.LoadExisting();
            if (transformStore != null && transformStore.changes.Count > 0)
            {
                foreach (var change in transformStore.changes)
                {
                    var changeScene = SceneManager.GetSceneByPath(change.scenePath);
                    if (!changeScene.IsValid())
                        changeScene = SceneManager.GetSceneByName(change.scenePath);

                    if (!changeScene.IsValid() || changeScene != scene)
                        continue;

                    GameObject go = SceneAndPathUtilities.FindInSceneByPath(scene, change.objectPath);
                    if (go == null)
                        continue;

                    Transform t = go.transform;
                    RectTransform rt = t as RectTransform;

                    if (change.modifiedProperties is { Count: > 0 })
                    {
                        foreach (var prop in change.modifiedProperties)
                            ApplyPropertyToTransform(t, rt, change, prop);
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
                }
            }

            var compStore = ComponentChangesStore.LoadExisting();
            if (compStore != null && compStore.changes.Count > 0)
            {
                foreach (var change in compStore.changes)
                {
                    var changeScene = SceneManager.GetSceneByPath(change.scenePath);
                    if (!changeScene.IsValid())
                        changeScene = SceneManager.GetSceneByName(change.scenePath);

                    if (!changeScene.IsValid() || changeScene != scene)
                        continue;

                    GameObject go = SceneAndPathUtilities.FindInSceneByPath(scene, change.objectPath);
                    if (go == null)
                        continue;

                    var type = Type.GetType(change.componentType);
                    if (type == null)
                        continue;

                    var allComps = go.GetComponents(type);
                    if (change.componentIndex < 0 || change.componentIndex >= allComps.Length)
                        continue;

                    var comp = allComps[change.componentIndex];
                    if (comp == null)
                        continue;

                    SerializedObject so = new SerializedObject(comp);

                    for (int i = 0; i < change.propertyPaths.Count; i++)
                    {
                        string path = change.propertyPaths[i];
                        string value = change.serializedValues[i];
                        string typeName = change.valueTypes[i];

                        SerializedProperty prop = so.FindProperty(path);
                        if (prop == null)
                            continue;

                        Serialization.ComponentPropertySerializer.ApplyPropertyValue(prop, typeName, value);
                    }

                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static void HandlePlayExitFlow()
        {
            if (Application.isPlaying) return;

            var activeAfterPlay = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(startScenePathAtPlayEnter))
            {
                startScenePathAtPlayEnter = activeAfterPlay.IsValid() ? activeAfterPlay.path : null;
                Debug.Log($"[PlayOverrides][EnteredEditMode] Fallback startScenePathAtPlayEnter='{startScenePathAtPlayEnter}'");
            }

            var activePathNow = activeAfterPlay.IsValid() ? SceneAndPathUtilities.NormalizeScenePath(activeAfterPlay.path) : string.Empty;
            var startPathNow = SceneAndPathUtilities.NormalizeScenePath(startScenePathAtPlayEnter);

            if (!string.IsNullOrEmpty(startPathNow) &&
                string.Equals(activePathNow, startPathNow, StringComparison.OrdinalIgnoreCase))
            {
                EditorApplication.delayCall += TriggerApplyFlowAfterPlayExit;
            }
            else
            {
                Debug.Log($"[PlayOverrides][EnteredEditMode] Active scene '{activePathNow}' != start scene '{startPathNow}', not starting apply flow");
            }
        }

        private static void TriggerApplyFlowAfterPlayExit()
        {
            if (Application.isPlaying || PlayModeFlow.PlayModeOverrideFlow.IsProcessing) 
                return;

            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.isLoaded || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TriggerApplyFlowAfterPlayExit;
                return;
            }

            EditorApplication.delayCall += () => 
            {
                if (PlayModeFlow.PlayModeOverrideFlow.IsProcessing || Application.isPlaying) 
                    return;

                Debug.Log("[PlayOverrides] All stable, opening popups...");
                PlayModeFlow.PlayModeOverrideFlow.HandleApplyChangesFromStoreOnPlayExit();
            };
        }

        // ===== PUBLIC API =====

        public static void AcceptTransformChanges(GameObject go)
        {
            if (go == null) return;

            TransformSnapshot original = SnapshotManager.GetSnapshot(go);
            TransformSnapshot current = new TransformSnapshot(go);

            SnapshotManager.SetSnapshot(go, current);
            RecordTransformChangeToStore(go, original, current);
        }

        public static void AcceptComponentChanges(Component comp)
        {
            if (comp == null) return;

            GameObject go = comp.gameObject;
            string compKey = SceneAndPathUtilities.GetComponentKey(comp);

            ComponentSnapshot originalSnapshot = SnapshotManager.GetComponentSnapshot(go, compKey);
            SnapshotManager.ResetComponentBaseline(comp);
            RecordComponentChangeToStore(comp, originalSnapshot);
        }

        public static List<Component> GetChangedComponents(GameObject go)
        {
            return SnapshotManager.GetChangedComponents(go);
        }

        public static TransformSnapshot GetSnapshot(GameObject go)
        {
            return SnapshotManager.GetSnapshot(go);
        }

        public static string GetComponentKey(Component comp)
        {
            return SceneAndPathUtilities.GetComponentKey(comp);
        }

        public static ComponentSnapshot GetComponentSnapshot(GameObject go, string componentKey)
        {
            return SnapshotManager.GetComponentSnapshot(go, componentKey);
        }

        public static void ResetTransformBaseline(GameObject go)
        {
            SnapshotManager.ResetTransformBaseline(go);
        }

        public static void ResetComponentBaseline(Component comp)
        {
            SnapshotManager.ResetComponentBaseline(comp);
        }

        public static void RevertAll(GameObject go)
        {
            string key = SceneAndPathUtilities.GetGameObjectKey(go);
            if (selectedProperties.TryGetValue(key, out var property))
                property.Clear();
        }

        public static void ApplyAll(GameObject go)
        {
            TransformSnapshot original = SnapshotManager.GetSnapshot(go);
            if (original == null) return;

            TransformSnapshot current = new TransformSnapshot(go);
            string key = SceneAndPathUtilities.GetGameObjectKey(go);

            if (!selectedProperties.ContainsKey(key))
                selectedProperties[key] = new HashSet<string>();

            selectedProperties[key].Clear();

            foreach (var change in SnapshotManager.GetChangedProperties(original, current))
                selectedProperties[key].Add(change);
        }

        public static void ToggleProperty(GameObject go, string property)
        {
            string key = SceneAndPathUtilities.GetGameObjectKey(go);
            if (!selectedProperties.ContainsKey(key))
                selectedProperties[key] = new HashSet<string>();

            if (selectedProperties[key].Contains(property))
                selectedProperties[key].Remove(property);
            else
                selectedProperties[key].Add(property);
        }

        public static bool IsPropertySelected(GameObject go, string property)
        {
            string key = SceneAndPathUtilities.GetGameObjectKey(go);
            return selectedProperties.ContainsKey(key) &&
                   selectedProperties[key].Contains(property);
        }

        // ===== PRIVATE HELPERS =====

        private static void ClearStoresOnPlayEnter()
        {
            var transformStore = TransformChangesStore.LoadExisting();
            if (transformStore != null)
                transformStore.Clear();

            var componentStore = ComponentChangesStore.LoadExisting();
            if (componentStore != null)
                componentStore.Clear();
        }

        private static void RecordTransformChangeToStore(GameObject go, TransformSnapshot original, TransformSnapshot current)
        {
            var store = TransformChangesStore.LoadOrCreate();

            string scenePath = go.scene.path;
            string objectPath = SceneAndPathUtilities.GetGameObjectPath(go.transform);

            List<string> modifiedProps = original != null 
                ? SnapshotManager.GetChangedProperties(original, current)
                : new List<string> { "position", "rotation", "scale" };

            var existingIndex = store.changes.FindIndex(c => c.scenePath == scenePath && c.objectPath == objectPath);

            bool hasOriginal = false;
            Vector3 originalPosition = Vector3.zero;
            Quaternion originalRotation = Quaternion.identity;
            Vector3 originalScale = Vector3.one;
            Vector2 originalAnchoredPosition = Vector2.zero;
            Vector3 originalAnchoredPosition3D = Vector3.zero;
            Vector2 originalAnchorMin = Vector2.zero;
            Vector2 originalAnchorMax = Vector2.one;
            Vector2 originalPivot = new Vector2(0.5f, 0.5f);
            Vector2 originalSizeDelta = Vector2.zero;
            Vector2 originalOffsetMin = Vector2.zero;
            Vector2 originalOffsetMax = Vector2.zero;

            if (existingIndex >= 0 && store.changes[existingIndex].hasOriginalValues)
            {
                var existing = store.changes[existingIndex];
                hasOriginal = true;
                originalPosition = existing.originalPosition;
                originalRotation = existing.originalRotation;
                originalScale = existing.originalScale;
                originalAnchoredPosition = existing.originalAnchoredPosition;
                originalAnchoredPosition3D = existing.originalAnchoredPosition3D;
                originalAnchorMin = existing.originalAnchorMin;
                originalAnchorMax = existing.originalAnchorMax;
                originalPivot = existing.originalPivot;
                originalSizeDelta = existing.originalSizeDelta;
                originalOffsetMin = existing.originalOffsetMin;
                originalOffsetMax = existing.originalOffsetMax;
            }
            else if (original != null)
            {
                hasOriginal = true;
                originalPosition = original.position;
                originalRotation = original.rotation;
                originalScale = original.scale;

                if (original.isRectTransform)
                {
                    originalAnchoredPosition = original.anchoredPosition;
                    originalAnchoredPosition3D = original.anchoredPosition3D;
                    originalAnchorMin = original.anchorMin;
                    originalAnchorMax = original.anchorMax;
                    originalPivot = original.pivot;
                    originalSizeDelta = original.sizeDelta;
                    originalOffsetMin = original.offsetMin;
                    originalOffsetMax = original.offsetMax;
                }
            }

            var change = new TransformChangesStore.TransformChange
            {
                scenePath = scenePath,
                objectPath = objectPath,
                isRectTransform = current.isRectTransform,
                position = current.position,
                rotation = current.rotation,
                scale = current.scale,
                anchoredPosition = current.anchoredPosition,
                anchoredPosition3D = current.anchoredPosition3D,
                anchorMin = current.anchorMin,
                anchorMax = current.anchorMax,
                pivot = current.pivot,
                sizeDelta = current.sizeDelta,
                offsetMin = current.offsetMin,
                offsetMax = current.offsetMax,
                modifiedProperties = modifiedProps,
                hasOriginalValues = hasOriginal,
                originalPosition = originalPosition,
                originalRotation = originalRotation,
                originalScale = originalScale,
                originalAnchoredPosition = originalAnchoredPosition,
                originalAnchoredPosition3D = originalAnchoredPosition3D,
                originalAnchorMin = originalAnchorMin,
                originalAnchorMax = originalAnchorMax,
                originalPivot = originalPivot,
                originalSizeDelta = originalSizeDelta,
                originalOffsetMin = originalOffsetMin,
                originalOffsetMax = originalOffsetMax
            };

            if (existingIndex >= 0)
                store.changes[existingIndex] = change;
            else
                store.changes.Add(change);

            EditorUtility.SetDirty(store);
            AssetDatabase.SaveAssets();
        }

        private static void RecordComponentChangeToStore(Component comp, ComponentSnapshot originalSnapshot)
        {
            var store = ComponentChangesStore.LoadOrCreate();

            string scenePath = comp.gameObject.scene.path;
            string objectPath = SceneAndPathUtilities.GetGameObjectPath(comp.transform);
            var allOfType = comp.gameObject.GetComponents(comp.GetType());
            int index = Array.IndexOf(allOfType, comp);

            SerializedObject so = new SerializedObject(comp);
            SerializedProperty prop = so.GetIterator();

            var propertyPaths = new List<string>();
            var values = new List<string>();
            var typeNames = new List<string>();

            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.name == "m_Script")
                    continue;

                propertyPaths.Add(prop.propertyPath);
                Serialization.ComponentPropertySerializer.SerializeProperty(prop, out string typeName, out string serializedValue);
                typeNames.Add(typeName);
                values.Add(serializedValue);
            }

            int existing = store.changes.FindIndex(c => c.scenePath == scenePath && c.objectPath == objectPath && c.componentType == comp.GetType().AssemblyQualifiedName && c.componentIndex == index);

            bool hasOriginal = false;
            List<string> originalValues = new List<string>();
            List<string> originalTypes = new List<string>();

            if (existing >= 0 && store.changes[existing].hasOriginalValues)
            {
                var existingChange = store.changes[existing];
                hasOriginal = true;
                if (existingChange.originalSerializedValues != null)
                    originalValues.AddRange(existingChange.originalSerializedValues);
                if (existingChange.originalValueTypes != null)
                    originalTypes.AddRange(existingChange.originalValueTypes);
            }
            else if (originalSnapshot != null)
            {
                hasOriginal = true;

                foreach (var path in propertyPaths)
                {
                    if (originalSnapshot.properties != null && originalSnapshot.properties.TryGetValue(path, out var originalValue) && originalValue != null)
                    {
                        Serialization.SnapshotSerializer.SerializeValue(originalValue, out string typeName, out string serializedValue);
                        originalTypes.Add(typeName);
                        originalValues.Add(serializedValue);
                    }
                    else
                    {
                        originalTypes.Add(string.Empty);
                        originalValues.Add(string.Empty);
                    }
                }
            }

            var change = new ComponentChangesStore.ComponentChange
            {
                scenePath = scenePath,
                objectPath = objectPath,
                componentType = comp.GetType().AssemblyQualifiedName,
                componentIndex = index,
                propertyPaths = propertyPaths,
                serializedValues = values,
                valueTypes = typeNames,
                hasOriginalValues = hasOriginal,
                originalSerializedValues = originalValues,
                originalValueTypes = originalTypes
            };

            if (existing >= 0)
                store.changes[existing] = change;
            else
                store.changes.Add(change);

            EditorUtility.SetDirty(store);
            AssetDatabase.SaveAssets();
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
    }
}
