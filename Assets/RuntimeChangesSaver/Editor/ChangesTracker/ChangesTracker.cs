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

            var transformOriginalStore = TransformOriginalStore.LoadExisting();
            if (transformOriginalStore != null)
                transformOriginalStore.Clear();

            var componentOriginalStore = ComponentOriginalStore.LoadExisting();
            if (componentOriginalStore != null)
                componentOriginalStore.Clear();
        }

        private static void RecordTransformChangeToStore(GameObject go, TransformSnapshot original, TransformSnapshot current)
        {
            var store = TransformChangesStore.LoadOrCreate();
            TransformOriginalStore originalStore = null;
            bool originalStored = false;

            string scenePath = go.scene.path;
            string objectPath = SceneAndPathUtilities.GetGameObjectPath(go.transform);

            List<string> modifiedProps = original != null 
                ? SnapshotManager.GetChangedProperties(original, current)
                : new List<string> { "position", "rotation", "scale" };
            if (original != null)
            {
                originalStore = TransformOriginalStore.LoadOrCreate();
                int originalIndex = originalStore.entries.FindIndex(e => e.scenePath == scenePath && e.objectPath == objectPath);

                if (originalIndex < 0)
                {
                    var entry = new TransformOriginalStore.TransformOriginal
                    {
                        scenePath = scenePath,
                        objectPath = objectPath,
                        isRectTransform = original.isRectTransform,
                        position = original.position,
                        rotation = original.rotation,
                        scale = original.scale,
                        anchoredPosition = original.isRectTransform ? original.anchoredPosition : Vector2.zero,
                        anchoredPosition3D = original.isRectTransform ? original.anchoredPosition3D : Vector3.zero,
                        anchorMin = original.isRectTransform ? original.anchorMin : Vector2.zero,
                        anchorMax = original.isRectTransform ? original.anchorMax : Vector2.one,
                        pivot = original.isRectTransform ? original.pivot : new Vector2(0.5f, 0.5f),
                        sizeDelta = original.isRectTransform ? original.sizeDelta : Vector2.zero,
                        offsetMin = original.isRectTransform ? original.offsetMin : Vector2.zero,
                        offsetMax = original.isRectTransform ? original.offsetMax : Vector2.zero
                    };

                    originalStore.entries.Add(entry);
                    EditorUtility.SetDirty(originalStore);
                    originalStored = true;
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
                modifiedProperties = modifiedProps
            };

            var existingIndex = store.changes.FindIndex(c => c.scenePath == scenePath && c.objectPath == objectPath);
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
            ComponentOriginalStore originalStore = null;
            bool originalStored = false;

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

            if (originalSnapshot != null)
            {
                originalStore = ComponentOriginalStore.LoadOrCreate();
                bool alreadyStored = originalStore.entries.Exists(e =>
                    e.scenePath == scenePath &&
                    e.objectPath == objectPath &&
                    e.componentType == comp.GetType().AssemblyQualifiedName &&
                    e.componentIndex == index);

                if (!alreadyStored)
                {
                    var originalValues = new List<string>();
                    var originalTypes = new List<string>();

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

                    var originalEntry = new ComponentOriginalStore.ComponentOriginal
                    {
                        scenePath = scenePath,
                        objectPath = objectPath,
                        componentType = comp.GetType().AssemblyQualifiedName,
                        componentIndex = index,
                        propertyPaths = new List<string>(propertyPaths),
                        serializedValues = originalValues,
                        valueTypes = originalTypes
                    };

                    originalStore.entries.Add(originalEntry);
                    EditorUtility.SetDirty(originalStore);
                    originalStored = true;
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
                valueTypes = typeNames
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
