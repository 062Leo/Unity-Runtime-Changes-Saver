using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;




[InitializeOnLoad]
public static class PlayModeChangesTracker
{
    // Use SerializeField to survive domain reload
    [System.Serializable]
    private class SnapshotData
    {
        public List<string> keys = new List<string>();
        public List<TransformSnapshotData> transforms = new List<TransformSnapshotData>();
    }

    [System.Serializable]
    private class TransformSnapshotData
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public bool isRectTransform;
        public Vector2 anchoredPosition;
        public Vector3 anchoredPosition3D;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 sizeDelta;
        public Vector2 offsetMin;
        public Vector2 offsetMax;
    }

    private static Dictionary<string, TransformSnapshot> snapshots = new();
    private static Dictionary<string, HashSet<string>> selectedProperties = new();
    private static Dictionary<string, Dictionary<string, ComponentSnapshot>> componentSnapshots = new();
    private static HashSet<string> markedForPersistence = new HashSet<string>();

    private const string PREFS_KEY = "PlayModeChangesTracker_CaptureNeeded";

    static PlayModeChangesTracker()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += OnEditorUpdate;
    }

    private static void OnEditorUpdate()
    {
        // Check if we need to capture on first play mode frame
        if (Application.isPlaying && snapshots.Count == 0)
        {
            bool needsCapture = EditorPrefs.GetBool(PREFS_KEY, false);
            if (needsCapture)
            {
                EditorPrefs.DeleteKey(PREFS_KEY);
                // Wait one frame for scene to be ready
                EditorApplication.delayCall += CaptureSnapshotsInPlayMode;
            }
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
                // Beim Start in den Play Mode immer den persistenten Store leeren,
                // damit wir nur Änderungen aus der aktuellen Session übernehmen.
                var storeOnEnterPlay = PlayModeTransformChangesStore.LoadExisting();
                if (storeOnEnterPlay != null)
                {
                    storeOnEnterPlay.Clear();
                }

                CaptureSnapshotsInEditMode();
                // Also set flag in case ExitingEditMode doesn't capture properly
                EditorPrefs.SetBool(PREFS_KEY, snapshots.Count == 0);
                break;

            case PlayModeStateChange.EnteredPlayMode:
                // If no snapshots, schedule capture for next frame
                if (snapshots.Count == 0)
                {
                    EditorPrefs.SetBool(PREFS_KEY, true);
                }
                break;

            case PlayModeStateChange.EnteredEditMode:
                EditorPrefs.DeleteKey(PREFS_KEY);
                ApplyChangesFromStoreToEditMode();
                // Clear snapshots when returning to edit mode
                snapshots.Clear();
                componentSnapshots.Clear();
                selectedProperties.Clear();
                markedForPersistence.Clear();
                break;
        }
    }

    private static void CaptureSnapshotsInPlayMode()
    {
        snapshots.Clear();
        selectedProperties.Clear();
        componentSnapshots.Clear();

        int sceneCount = SceneManager.sceneCount;
        int totalObjects = 0;
        for (int i = 0; i < sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject rootGO in roots)
            {
                totalObjects += CaptureGameObjectRecursive(rootGO);
            }
        }
    }

    private static void CaptureSnapshotsInEditMode()
    {
        snapshots.Clear();
        selectedProperties.Clear();
        componentSnapshots.Clear();

        int sceneCount = SceneManager.sceneCount;
        if (sceneCount == 0)
        {
            return;
        }

        // Find all GameObjects in all loaded scenes
        int totalObjects = 0;
        for (int i = 0; i < sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject rootGO in roots)
            {
                totalObjects += CaptureGameObjectRecursive(rootGO);
            }
        }
    }

    private static int CaptureGameObjectRecursive(GameObject go)
    {
        int count = 1;

        string key = GetGameObjectKey(go);
        snapshots[key] = new TransformSnapshot(go);

        // Capture all components
        var compDict = new Dictionary<string, ComponentSnapshot>();
        Component[] components = go.GetComponents<Component>();
        foreach (var comp in components)
        {
            if (comp == null)
            {
                continue;
            }

            if (comp is Transform)
            {
                continue;
            }

            string compKey = GetComponentKey(comp);
            ComponentSnapshot compSnapshot = CaptureComponentSnapshot(comp);
            compDict[compKey] = compSnapshot;
        }

        componentSnapshots[key] = compDict;

        // Recurse to children
        foreach (Transform child in go.transform)
        {
            count += CaptureGameObjectRecursive(child.gameObject);
        }

        return count;
    }

    private static void CaptureSnapshots()
    {
        // This method is no longer used, but kept for backward compatibility
        CaptureSnapshotsInEditMode();
    }

    private static ComponentSnapshot CaptureComponentSnapshot(Component comp)
    {
        var snapshot = new ComponentSnapshot
        {
            componentType = comp.GetType().AssemblyQualifiedName
        };

        SerializedObject so = new SerializedObject(comp);
        SerializedProperty prop = so.GetIterator();

        bool enterChildren = true;
        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;

            // Skip script reference
            if (prop.name == "m_Script") continue;

            try
            {
                object value = GetPropertyValue(prop);
                if (value != null)
                {
                    snapshot.properties[prop.propertyPath] = value;
                }
            }
            catch { }
        }

        return snapshot;
    }

    private static object GetPropertyValue(SerializedProperty prop)
    {
        switch (prop.propertyType)
        {
            case SerializedPropertyType.Integer: return prop.intValue;
            case SerializedPropertyType.Boolean: return prop.boolValue;
            case SerializedPropertyType.Float: return prop.floatValue;
            case SerializedPropertyType.String: return prop.stringValue;
            case SerializedPropertyType.Color: return prop.colorValue;
            case SerializedPropertyType.Vector2: return prop.vector2Value;
            case SerializedPropertyType.Vector3: return prop.vector3Value;
            case SerializedPropertyType.Vector4: return prop.vector4Value;
            case SerializedPropertyType.Quaternion: return prop.quaternionValue;
            case SerializedPropertyType.Enum: return prop.enumValueIndex;
            default: return null;
        }
    }

    public static string GetGameObjectKey(GameObject go)
    {
        if (go == null) return "";

        // Use scene path + GameObject path as unique identifier
        string scenePath = go.scene.path;
        if (string.IsNullOrEmpty(scenePath))
            scenePath = go.scene.name;

        string goPath = GetGameObjectPath(go.transform);
        return $"{scenePath}|{goPath}";
    }

    public static string GetComponentKey(Component comp)
    {
        var allComps = comp.gameObject.GetComponents(comp.GetType());
        int index = Array.IndexOf(allComps, comp);
        return $"{comp.GetType().Name}_{index}";
    }

    public static List<Component> GetChangedComponents(GameObject go)
    {
        if (go == null) return new List<Component>();

        string key = GetGameObjectKey(go);

        // If no snapshots exist, this GameObject wasn't captured
        if (!componentSnapshots.ContainsKey(key))
        {
            return new List<Component>();
        }

        var changed = new List<Component>();
        var compSnapshots = componentSnapshots[key];

        // Check Transform changes separately
        if (snapshots.ContainsKey(key))
        {
            TransformSnapshot originalTransform = snapshots[key];
            TransformSnapshot currentTransform = new TransformSnapshot(go);
            var transformChanges = GetChangedProperties(originalTransform, currentTransform);

            if (transformChanges.Count > 0)
            {
                Debug.Log($"[TransformDebug][Tracker.GetChangedComponents] GO='{go.name}', TransformChanged=True, changedProps={string.Join(",", transformChanges)}");
                changed.Add(go.transform);
            }
            else
            {
                Debug.Log($"[TransformDebug][Tracker.GetChangedComponents] GO='{go.name}', TransformChanged=False");
            }
        }

        // Check other components
        foreach (var comp in go.GetComponents<Component>())
        {
            if (comp == null || comp is Transform) continue;

            string compKey = GetComponentKey(comp);

            if (!compSnapshots.ContainsKey(compKey))
            {
                continue;
            }

            bool hasChanged = HasComponentChanged(comp, compSnapshots[compKey]);

            if (hasChanged)
            {
                changed.Add(comp);
            }
        }

        return changed;
    }

    private static bool HasComponentChanged(Component comp, ComponentSnapshot snapshot)
    {
        SerializedObject so = new SerializedObject(comp);
        SerializedProperty prop = so.GetIterator();

        bool enterChildren = true;
        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;

            // Skip script reference
            if (prop.name == "m_Script") continue;

            if (!snapshot.properties.ContainsKey(prop.propertyPath))
                continue;

            try
            {
                object currentValue = GetPropertyValue(prop);
                object originalValue = snapshot.properties[prop.propertyPath];

                if (currentValue == null && originalValue == null) continue;
                if (currentValue == null || originalValue == null) return true;

                // Special handling for different types
                if (currentValue is Vector2 v2Current && originalValue is Vector2 v2Original)
                {
                    if (Vector2.Distance(v2Current, v2Original) > 0.0001f) return true;
                }
                else if (currentValue is Vector3 v3Current && originalValue is Vector3 v3Original)
                {
                    if (Vector3.Distance(v3Current, v3Original) > 0.0001f) return true;
                }
                else if (currentValue is Quaternion qCurrent && originalValue is Quaternion qOriginal)
                {
                    if (Quaternion.Angle(qCurrent, qOriginal) > 0.0001f) return true;
                }
                else if (currentValue is float fCurrent && originalValue is float fOriginal)
                {
                    if (Mathf.Abs(fCurrent - fOriginal) > 0.0001f) return true;
                }
                else
                {
                    if (!currentValue.Equals(originalValue)) return true;
                }
            }
            catch { }
        }

        return false;
    }

    private static void RecordSelectedChangesToStore()
    {
        var store = PlayModeTransformChangesStore.LoadOrCreate();
        store.Clear();

        if (selectedProperties.Count == 0)
            return;

        foreach (var kvp in selectedProperties)
        {
            string goKey = kvp.Key;

            if (!snapshots.ContainsKey(goKey))
                continue;

            // Parse the key to find the GameObject
            // Key format: "scenePath|goPath"
            var parts = goKey.Split('|');
            if (parts.Length != 2) continue;

            string scenePath = parts[0];
            string goPath = parts[1];

            var scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid())
                scene = SceneManager.GetSceneByName(scenePath);

            if (!scene.IsValid()) continue;

            GameObject go = FindInSceneByPath(scene, goPath);
            if (go == null) continue;

            TransformSnapshot original = snapshots[goKey];
            TransformSnapshot current = new TransformSnapshot(go);

            var changedProps = GetChangedProperties(original, current);
            var selectedAndChanged = new List<string>();
            foreach (var prop in kvp.Value)
            {
                if (changedProps.Contains(prop))
                    selectedAndChanged.Add(prop);
            }

            if (selectedAndChanged.Count == 0)
                continue;

            var change = new PlayModeTransformChangesStore.TransformChange
            {
                scenePath = go.scene.path,
                objectPath = goPath,
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
                modifiedProperties = selectedAndChanged
            };

            store.changes.Add(change);
        }

        EditorUtility.SetDirty(store);
        AssetDatabase.SaveAssets();
    }

    public static void PersistSelectedChangesForAll()
    {
        RecordSelectedChangesToStore();
    }

    public static void MarkForPersistence(GameObject go)
    {
        string key = GetGameObjectKey(go);
        markedForPersistence.Add(key);
        Debug.Log($"[TransformDebug][Tracker.MarkForPersistence] GO='{go.name}', Key='{key}'");
    }

    private static void ApplyChangesFromStoreToEditMode()
    {
        // Transform-Änderungen werden über das ScriptableObject
        // PlayModeTransformChangesStore persistiert. Dies überlebt
        // Domain Reloads besser als statische Dictionaries.

        var store = PlayModeTransformChangesStore.LoadExisting();
        if (store != null && store.changes.Count > 0)
        {
            foreach (var change in store.changes)
            {
                var scene = EditorSceneManager.GetSceneByPath(change.scenePath);
                if (!scene.IsValid())
                    scene = EditorSceneManager.GetSceneByName(change.scenePath);

                if (!scene.IsValid())
                    continue;

                GameObject go = FindInSceneByPath(scene, change.objectPath);
                if (go == null)
                    continue;

                Transform t = go.transform;
                RectTransform rt = t as RectTransform;

                Undo.RecordObject(t, "Apply Play Mode Transform Changes");

                // Wenn modifiedProperties gesetzt ist, nur diese anwenden,
                // sonst alle Transform-Werte.
                if (change.modifiedProperties != null && change.modifiedProperties.Count > 0)
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
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                }

                Debug.Log($"[TransformDebug][Tracker.ApplyChanges] Applied Transform changes to GO='{go.name}', scene='{scene.path}'");
            }

            // Nach dem Anwenden leeren wir den Store.
            store.Clear();
            AssetDatabase.SaveAssets();
        }
    }

    // Nimmt die aktuellen Transform-Werte eines GameObjects an (Apply/Apply All im Play Mode):
    // 1) Baseline-Snapshot wird auf den aktuellen Zustand gesetzt, damit das Objekt aus der
    //    Changed-Liste verschwindet.
    // 2) Die aktuellen Werte werden in den ScriptableObject-Store geschrieben, um sie beim
    //    Verlassen des Play Modes im Edit Mode zu übernehmen.
    public static void AcceptTransformChanges(GameObject go)
    {
        if (go == null)
            return;

        // Baseline verschieben: aktueller Zustand wird neuer Snapshot.
        SetSnapshot(go, new TransformSnapshot(go));

        // Persistente Speicherung im ScriptableObject.
        RecordTransformChangeToStore(go);
    }

    private static void RecordTransformChangeToStore(GameObject go)
    {
        var store = PlayModeTransformChangesStore.LoadOrCreate();

        string scenePath = go.scene.path;
        string objectPath = GetGameObjectPath(go.transform);

        TransformSnapshot current = new TransformSnapshot(go);
        TransformSnapshot original = GetSnapshot(go);

        List<string> modifiedProps;
        if (original != null)
        {
            modifiedProps = GetChangedProperties(original, current);
        }
        else
        {
            // Falls kein Original-Snapshot existiert, alle Eigenschaften als geändert markieren.
            modifiedProps = new List<string> { "position", "rotation", "scale" };
            if (current.isRectTransform)
            {
                modifiedProps.AddRange(new[]
                {
                    "anchoredPosition", "anchoredPosition3D", "anchorMin", "anchorMax",
                    "pivot", "sizeDelta", "offsetMin", "offsetMax"
                });
            }
        }

        // Existierenden Eintrag für dieses Objekt suchen und überschreiben oder neuen anlegen.
        var existing = store.changes.FindIndex(c => c.scenePath == scenePath && c.objectPath == objectPath);
        var change = new PlayModeTransformChangesStore.TransformChange
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

        if (existing >= 0)
        {
            store.changes[existing] = change;
        }
        else
        {
            store.changes.Add(change);
        }

        EditorUtility.SetDirty(store);
        AssetDatabase.SaveAssets();
    }

    private static void ApplyPropertyToTransform(Transform t, RectTransform rt, PlayModeTransformChangesStore.TransformChange change, string prop)
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

    private static string GetGameObjectPath(Transform transform)
    {
        var path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }

    private static GameObject FindInSceneByPath(Scene scene, string path)
    {
        if (!scene.IsValid())
            return null;

        var parts = path.Split('/');
        if (parts.Length == 0)
            return null;

        GameObject current = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == parts[0])
            {
                current = root;
                break;
            }
        }

        if (current == null)
            return null;

        for (int i = 1; i < parts.Length; i++)
        {
            var childName = parts[i];
            Transform child = null;
            foreach (Transform t in current.transform)
            {
                if (t.name == childName)
                {
                    child = t;
                    break;
                }
            }

            if (child == null)
                return null;

            current = child.gameObject;
        }

        return current;
    }

    public static TransformSnapshot GetSnapshot(GameObject go)
    {
        if (go == null)
            return null;

        string key = GetGameObjectKey(go);
        bool found = snapshots.TryGetValue(key, out var snap);
        Debug.Log($"[TransformDebug][Tracker.GetSnapshot] GO='{go.name}', Key='{key}', Found={found}");
        return found ? snap : null;
    }

    public static void SetSnapshot(GameObject go, TransformSnapshot snapshot)
    {
        if (snapshot == null) return;
        string key = GetGameObjectKey(go);
        snapshots[key] = snapshot;
    }

    public static void ToggleProperty(GameObject go, string property)
    {
        string key = GetGameObjectKey(go);
        if (!selectedProperties.ContainsKey(key))
            selectedProperties[key] = new HashSet<string>();

        if (selectedProperties[key].Contains(property))
            selectedProperties[key].Remove(property);
        else
            selectedProperties[key].Add(property);
    }

    public static bool IsPropertySelected(GameObject go, string property)
    {
        string key = GetGameObjectKey(go);
        return selectedProperties.ContainsKey(key) &&
               selectedProperties[key].Contains(property);
    }

    public static void RevertAll(GameObject go)
    {
        string key = GetGameObjectKey(go);
        if (selectedProperties.ContainsKey(key))
            selectedProperties[key].Clear();
    }

    public static void ApplyAll(GameObject go)
    {
        string key = GetGameObjectKey(go);
        if (!snapshots.ContainsKey(key))
            return;

        TransformSnapshot original = snapshots[key];
        TransformSnapshot current = new TransformSnapshot(go);

        if (!selectedProperties.ContainsKey(key))
            selectedProperties[key] = new HashSet<string>();

        selectedProperties[key].Clear();

        foreach (var change in GetChangedProperties(original, current))
        {
            selectedProperties[key].Add(change);
        }
    }

    public static List<string> GetChangedProperties(TransformSnapshot original, TransformSnapshot current)
    {
        List<string> changed = new List<string>();

        if (original.position != current.position) changed.Add("position");
        if (original.rotation != current.rotation) changed.Add("rotation");
        if (original.scale != current.scale) changed.Add("scale");

        if (original.isRectTransform)
        {
            if (original.anchoredPosition != current.anchoredPosition) changed.Add("anchoredPosition");
            if (original.anchoredPosition3D != current.anchoredPosition3D) changed.Add("anchoredPosition3D");
            if (original.anchorMin != current.anchorMin) changed.Add("anchorMin");
            if (original.anchorMax != current.anchorMax) changed.Add("anchorMax");
            if (original.pivot != current.pivot) changed.Add("pivot");
            if (original.sizeDelta != current.sizeDelta) changed.Add("sizeDelta");
            if (original.offsetMin != current.offsetMin) changed.Add("offsetMin");
            if (original.offsetMax != current.offsetMax) changed.Add("offsetMax");
        }

        return changed;
    }

    public static ComponentSnapshot GetComponentSnapshot(GameObject go, string componentKey)
    {
        string goKey = GetGameObjectKey(go);
        if (!componentSnapshots.ContainsKey(goKey))
            return null;

        return componentSnapshots[goKey].TryGetValue(componentKey, out var snapshot)
            ? snapshot
            : null;
    }
}
