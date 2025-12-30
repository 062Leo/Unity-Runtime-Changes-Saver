using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RuntimeChangesSaver.Editor
{
    [InitializeOnLoad]
    public static class ChangesTracker
    {

        [Serializable]
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

        private static readonly Dictionary<string, TransformSnapshot> snapshots = new();
        private static readonly Dictionary<string, HashSet<string>> selectedProperties = new();
        private static readonly Dictionary<string, Dictionary<string, ComponentSnapshot>> componentSnapshots = new();

        private const string PREFS_KEY = "PlayModeChangesTracker_CaptureNeeded";

        static ChangesTracker()
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
                    var transformStoreOnEnterPlay = TransformChangesStore.LoadExisting();
                    if (transformStoreOnEnterPlay != null)
                    {
                        transformStoreOnEnterPlay.Clear();
                    }

                    var componentStoreOnEnterPlay = ComponentChangesStore.LoadExisting();
                    if (componentStoreOnEnterPlay != null)
                    {
                        componentStoreOnEnterPlay.Clear();
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
                    break;
            }
        }

        private static void CaptureSnapshotsInPlayMode()
        {
            snapshots.Clear();
            selectedProperties.Clear();
            componentSnapshots.Clear();

            int sceneCount = SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                GameObject[] roots = scene.GetRootGameObjects();
                foreach (GameObject rootGO in roots)
                {
                     CaptureGameObjectRecursive(rootGO);
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
                     CaptureGameObjectRecursive(rootGO);
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
                catch 
                {
                    // ignored
                }
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

        // Wird verwendet, um nach einem Revert im Compare-Popup im Play Mode
        // die Baseline für den Transform eines bestimmten GameObjects neu zu setzen,
        // damit GetChangedComponents dieses Transform nicht mehr als "geändert" meldet.
        public static void ResetTransformBaseline(GameObject go)
        {
            if (go == null) return;
            string key = GetGameObjectKey(go);
            snapshots[key] = new TransformSnapshot(go);
        }

        // Wird verwendet, um nach einem Revert im Compare-Popup im Play Mode
        // die Baseline nur für eine spezifische Nicht-Transform-Komponente neu zu setzen.
        public static void ResetComponentBaseline(Component comp)
        {
            if (comp == null) return;

            GameObject go = comp.gameObject;
            string goKey = GetGameObjectKey(go);

            if (!componentSnapshots.TryGetValue(goKey, out var dict))
            {
                dict = new Dictionary<string, ComponentSnapshot>();
                componentSnapshots[goKey] = dict;
            }

            string compKey = GetComponentKey(comp);
            dict[compKey] = CaptureComponentSnapshot(comp);
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
            if (snapshots.TryGetValue(key, out var originalTransform))
            {
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

                if (!compSnapshots.TryGetValue(compKey, out var snapshot))
                {
                    continue;
                }

                bool hasChanged = HasComponentChanged(comp, snapshot);

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

                if (!snapshot.properties.TryGetValue(prop.propertyPath, out var originalValue))
                    continue;

                try
                {
                    object currentValue = GetPropertyValue(prop);

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
                catch
                {
                    // ignored
                }
            }

            return false;
        }

        private static void RecordSelectedChangesToStore()
        {
            var store = TransformChangesStore.LoadOrCreate();
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

                var change = new TransformChangesStore.TransformChange
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

        public static void AcceptComponentChanges(Component comp)
        {
            if (comp == null)
                return;

            GameObject go = comp.gameObject;
            string goKey = GetGameObjectKey(go);
            string compKey = GetComponentKey(comp);

            // Ursprünglichen Snapshot sichern, bevor wir die Baseline verschieben.
            ComponentSnapshot originalSnapshot = null;

            if (!componentSnapshots.TryGetValue(goKey, out var dict))
            {
                dict = new Dictionary<string, ComponentSnapshot>();
                componentSnapshots[goKey] = dict;
            }
            else
            {
                if (dict.TryGetValue(compKey, out var existingSnapshot))
                {
                    originalSnapshot = existingSnapshot;
                }
            }

            // Baseline für diese Komponente auf aktuellen Zustand verschieben
            ComponentSnapshot currentSnapshot = CaptureComponentSnapshot(comp);
            dict[compKey] = currentSnapshot;

            // Änderungen im ScriptableObject speichern (inkl. Originalwerte, falls verfügbar).
            RecordComponentChangeToStore(comp, originalSnapshot);
        }

        private static void RecordComponentChangeToStore(Component comp, ComponentSnapshot originalSnapshot)
        {
            var store = ComponentChangesStore.LoadOrCreate();

            string scenePath = comp.gameObject.scene.path;
            string objectPath = GetGameObjectPath(comp.transform);
            var allOfType = comp.gameObject.GetComponents(comp.GetType());
            int index = Array.IndexOf(allOfType, comp);

            // Erzeuge einen Schnappschuss des aktuellen Zustands
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
                SerializeComponentProperty(prop, out string typeName, out string serializedValue);
                typeNames.Add(typeName);
                values.Add(serializedValue);
            }

            // Bestehenden Eintrag für diese Komponente suchen
            int existing = store.changes.FindIndex(c => c.scenePath == scenePath && c.objectPath == objectPath && c.componentType == comp.GetType().AssemblyQualifiedName && c.componentIndex == index);

            // Originalwerte bestimmen:
            // - Wenn bereits ein Eintrag mit Originalwerten existiert, diesen beibehalten.
            // - Andernfalls aus dem übergebenen Snapshot ableiten (falls vorhanden).
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
                        SerializeSnapshotValue(originalValue, out string typeName, out string serializedValue);
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

        private static void SerializeComponentProperty(SerializedProperty prop, out string typeName, out string serializedValue)
        {
            typeName = "";
            serializedValue = "";

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    typeName = "Integer";
                    serializedValue = prop.intValue.ToString();
                    break;
                case SerializedPropertyType.Boolean:
                    typeName = "Boolean";
                    serializedValue = prop.boolValue.ToString();
                    break;
                case SerializedPropertyType.Float:
                    typeName = "Float";
                    serializedValue = prop.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case SerializedPropertyType.String:
                    typeName = "String";
                    serializedValue = prop.stringValue ?? string.Empty;
                    break;
                case SerializedPropertyType.Color:
                    typeName = "Color";
                    serializedValue = "#" + ColorUtility.ToHtmlStringRGBA(prop.colorValue);
                    break;
                case SerializedPropertyType.Vector2:
                    typeName = "Vector2";
                    serializedValue = SerializeVector2(prop.vector2Value);
                    break;
                case SerializedPropertyType.Vector3:
                    typeName = "Vector3";
                    serializedValue = SerializeVector3(prop.vector3Value);
                    break;
                case SerializedPropertyType.Vector4:
                    typeName = "Vector4";
                    serializedValue = SerializeVector4(prop.vector4Value);
                    break;
                case SerializedPropertyType.Quaternion:
                    typeName = "Quaternion";
                    serializedValue = SerializeQuaternion(prop.quaternionValue);
                    break;
                case SerializedPropertyType.Enum:
                    typeName = "Enum";
                    serializedValue = prop.enumValueIndex.ToString();
                    break;
                default:
                    // Nicht unterstützte Typen ignorieren wir
                    typeName = string.Empty;
                    serializedValue = string.Empty;
                    break;
            }
        }

        private static void SerializeSnapshotValue(object value, out string typeName, out string serializedValue)
        {
            typeName = string.Empty;
            serializedValue = string.Empty;

            if (value == null)
                return;

            switch (value)
            {
                case int i:
                    typeName = "Integer";
                    serializedValue = i.ToString();
                    break;
                case bool b:
                    typeName = "Boolean";
                    serializedValue = b.ToString();
                    break;
                case float f:
                    typeName = "Float";
                    serializedValue = f.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case string s:
                    typeName = "String";
                    serializedValue = s;

                    break;
                case Color c:
                    typeName = "Color";
                    serializedValue = "#" + ColorUtility.ToHtmlStringRGBA(c);
                    break;
                case Vector2 v2:
                    typeName = "Vector2";
                    serializedValue = SerializeVector2(v2);
                    break;
                case Vector3 v3:
                    typeName = "Vector3";
                    serializedValue = SerializeVector3(v3);
                    break;
                case Vector4 v4:
                    typeName = "Vector4";
                    serializedValue = SerializeVector4(v4);
                    break;
                case Quaternion q:
                    typeName = "Quaternion";
                    serializedValue = SerializeQuaternion(q);
                    break;
                case Enum e:
                    typeName = "Enum";
                    serializedValue = Convert.ToInt32(e).ToString();
                    break;
                default:
                    // Nicht unterstützte Typen bleiben leer
                    typeName = string.Empty;
                    serializedValue = string.Empty;
                    break;
            }
        }

        private static string SerializeVector2(Vector2 v) => $"{v.x.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        private static string SerializeVector3(Vector3 v) => $"{v.x.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v.y.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        private static string SerializeVector4(Vector4 v) => $"{v.x.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v.y.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v.z.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v.w.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        private static string SerializeQuaternion(Quaternion q) => $"{q.x.ToString(System.Globalization.CultureInfo.InvariantCulture)},{q.y.ToString(System.Globalization.CultureInfo.InvariantCulture)},{q.z.ToString(System.Globalization.CultureInfo.InvariantCulture)},{q.w.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

        private static Vector2 DeserializeVector2(string s)
        {
            var parts = s.Split(',');
            if (parts.Length != 2) return Vector2.zero;
            float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
            float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y);
            return new Vector2(x, y);
        }

        private static Vector3 DeserializeVector3(string s)
        {
            var parts = s.Split(',');
            if (parts.Length != 3) return Vector3.zero;
            float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
            float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y);
            float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z);
            return new Vector3(x, y, z);
        }

        private static Vector4 DeserializeVector4(string s)
        {
            var parts = s.Split(',');
            if (parts.Length != 4) return Vector4.zero;
            float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
            float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y);
            float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z);
            float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w);
            return new Vector4(x, y, z, w);
        }

        private static Quaternion DeserializeQuaternion(string s)
        {
            var parts = s.Split(',');
            if (parts.Length != 4) return Quaternion.identity;
            float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
            float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y);
            float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z);
            float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w);
            return new Quaternion(x, y, z, w);
        }

        public static void PersistSelectedChangesForAll()
        {
            RecordSelectedChangesToStore();
        }

        public static void MarkForPersistence(GameObject go)
        {
            string key = GetGameObjectKey(go);
            Debug.Log($"[TransformDebug][Tracker.MarkForPersistence] GO='{go.name}', Key='{key}'");
        }

        private static void ApplyChangesFromStoreToEditMode()
        {
            // Transform-Änderungen werden über das ScriptableObject
            // PlayModeTransformChangesStore persistiert. Dies überlebt
            // Domain Reloads besser als statische Dictionaries.

            var store = TransformChangesStore.LoadExisting();
            if (store != null && store.changes.Count > 0)
            {
                foreach (var change in store.changes)
                {
                    var scene = SceneManager.GetSceneByPath(change.scenePath);
                    if (!scene.IsValid())
                        scene = SceneManager.GetSceneByName(change.scenePath);

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
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                    }

                    Debug.Log($"[TransformDebug][Tracker.ApplyChanges] Applied Transform changes to GO='{go.name}', scene='{scene.path}'");
                }
            }

            // Nicht-Transform-Komponenten über einen separaten Store anwenden
            var compStore = ComponentChangesStore.LoadExisting();
            if (compStore != null && compStore.changes.Count > 0)
            {
                foreach (var change in compStore.changes)
                {
                    var scene = SceneManager.GetSceneByPath(change.scenePath);
                    if (!scene.IsValid())
                        scene = SceneManager.GetSceneByName(change.scenePath);

                    if (!scene.IsValid())
                        continue;

                    GameObject go = FindInSceneByPath(scene, change.objectPath);
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
                    Undo.RecordObject(comp, "Apply Play Mode Component Changes");

                    for (int i = 0; i < change.propertyPaths.Count; i++)
                    {
                        string path = change.propertyPaths[i];
                        string value = change.serializedValues[i];
                        string typeName = change.valueTypes[i];

                        SerializedProperty prop = so.FindProperty(path);
                        if (prop == null)
                            continue;

                        ApplySerializedComponentValue(prop, typeName, value);
                    }

                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(comp);
                    if (scene.IsValid())
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                    }
                }
            }

            AssetDatabase.SaveAssets();
        }

        private static void ApplySerializedComponentValue(SerializedProperty prop, string typeName, string value)
        {
            switch (typeName)
            {
                case "Integer":
                    if (int.TryParse(value, out var iVal)) prop.intValue = iVal; break;
                case "Boolean":
                    if (bool.TryParse(value, out var bVal)) prop.boolValue = bVal; break;
                case "Float":
                    if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fVal)) prop.floatValue = fVal; break;
                case "String":
                    prop.stringValue = value; break;
                case "Color":
                    if (ColorUtility.TryParseHtmlString(value, out var col)) prop.colorValue = col; break;
                case "Vector2":
                    prop.vector2Value = DeserializeVector2(value); break;
                case "Vector3":
                    prop.vector3Value = DeserializeVector3(value); break;
                case "Vector4":
                    prop.vector4Value = DeserializeVector4(value); break;
                case "Quaternion":
                    prop.quaternionValue = DeserializeQuaternion(value); break;
                case "Enum":
                    if (int.TryParse(value, out var eVal)) prop.enumValueIndex = eVal; break;
            }
        }

        // Nimmt die aktuellen Transform-Werte eines GameObjects an (Apply/Apply All im Play Mode):
        // 1) Baseline-Snapshot wird auf den aktuellen Zustand gesetzt, damit das Objekt aus der
        //    Changed-Liste verschwindet.
        // 2) Die aktuellen Werte werden in den ScriptableObject-Store geschrieben, um sie beim
        //    Verlassen des Play Modes im Edit Mode zu übernehmen.
        // 3) Zusätzlich werden die ursprünglichen Werte (vor dem ersten Accept) mitgespeichert,
        //    damit der Browser im Edit Mode diese als "Original" anzeigen kann.
        public static void AcceptTransformChanges(GameObject go)
        {
            if (go == null)
                return;

            // Ursprünglichen Snapshot sichern, bevor wir die Baseline verschieben.
            TransformSnapshot original = GetSnapshot(go);
            TransformSnapshot current = new TransformSnapshot(go);

            // Baseline verschieben: aktueller Zustand wird neuer Snapshot.
            SetSnapshot(go, current);

            // Persistente Speicherung im ScriptableObject (inkl. Originalwerte).
            RecordTransformChangeToStore(go, original, current);
        }

        private static void RecordTransformChangeToStore(GameObject go, TransformSnapshot original, TransformSnapshot current)
        {
            var store = TransformChangesStore.LoadOrCreate();

            string scenePath = go.scene.path;
            string objectPath = GetGameObjectPath(go.transform);

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

            // Existierenden Eintrag für dieses Objekt suchen.
            var existingIndex = store.changes.FindIndex(c => c.scenePath == scenePath && c.objectPath == objectPath);

            // Originalwerte bestimmen:
            // - Wenn bereits ein Eintrag mit Originalwerten existiert, diesen beibehalten.
            // - Andernfalls Original aus dem übergebenen Snapshot ableiten (falls vorhanden).
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
            {
                store.changes[existingIndex] = change;
            }
            else
            {
                store.changes.Add(change);
            }

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
            if (selectedProperties.TryGetValue(key, out var property))
                property.Clear();
        }

        public static void ApplyAll(GameObject go)
        {
            string key = GetGameObjectKey(go);
            if (!snapshots.TryGetValue(key, out var original))
                return;

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
            if (!componentSnapshots.TryGetValue(goKey, out var componentSnapshot))
                return null;

            return componentSnapshot.GetValueOrDefault(componentKey);
        }
    }
}
