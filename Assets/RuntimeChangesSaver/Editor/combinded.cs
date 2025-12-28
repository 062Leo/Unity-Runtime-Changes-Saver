using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

//
// Kombiniertes PlayMode-Changes-Script (alle relevanten Klassen in einer Datei)
//

// -------------------------
// PlayModeTransformChangesStore
// -------------------------

/// <summary>
/// Speichert Transform/RectTransform-Änderungen zwischen Play Mode und Edit Mode.
/// </summary>
public class PlayModeTransformChangesStore : ScriptableObject
{
    [Serializable]
    public class TransformChange
    {
        public string scenePath;
        public string objectPath;
        public bool isRectTransform;

        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        public Vector2 anchoredPosition;
        public Vector3 anchoredPosition3D;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 sizeDelta;
        public Vector2 offsetMin;
        public Vector2 offsetMax;

        public List<string> modifiedProperties = new List<string>();
    }

    public List<TransformChange> changes = new List<TransformChange>();

    public static PlayModeTransformChangesStore LoadExisting()
    {
        string[] guids = AssetDatabase.FindAssets("t:PlayModeTransformChangesStore");
        if (guids != null && guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<PlayModeTransformChangesStore>(path);
        }

        return null;
    }

    public static PlayModeTransformChangesStore LoadOrCreate()
    {
        var store = LoadExisting();
        if (store == null)
        {
            string assetPath = GetDefaultAssetPath();
            store = CreateInstance<PlayModeTransformChangesStore>();
            AssetDatabase.CreateAsset(store, assetPath);
            AssetDatabase.SaveAssets();
        }
        return store;
    }

    private static string GetDefaultAssetPath()
    {
        var tempInstance = CreateInstance<PlayModeTransformChangesStore>();
        MonoScript script = MonoScript.FromScriptableObject(tempInstance);
        string scriptPath = AssetDatabase.GetAssetPath(script);
        DestroyImmediate(tempInstance);

        string directory = string.IsNullOrEmpty(scriptPath)
            ? "Assets"
            : Path.GetDirectoryName(scriptPath);

        string assetPath = Path.Combine(directory, "PlayModeTransformChangesStore.asset");
        return assetPath.Replace("\\", "/");
    }

    public void Clear()
    {
        changes.Clear();
        EditorUtility.SetDirty(this);
    }
}

// -------------------------
// TransformSnapshot & PlayModeChangesTracker
// -------------------------

[Serializable]
public class TransformSnapshot
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

    public TransformSnapshot(GameObject go)
    {
        Transform t = go.transform;
        position = t.localPosition;
        rotation = t.localRotation;
        scale = t.localScale;

        RectTransform rt = t as RectTransform;
        isRectTransform = rt != null;

        if (isRectTransform)
        {
            anchoredPosition = rt.anchoredPosition;
            anchoredPosition3D = rt.anchoredPosition3D;
            anchorMin = rt.anchorMin;
            anchorMax = rt.anchorMax;
            pivot = rt.pivot;
            sizeDelta = rt.sizeDelta;
            offsetMin = rt.offsetMin;
            offsetMax = rt.offsetMax;
        }
    }
}

/// <summary>
/// Trackt Play Mode Änderungen und zeigt sie wie Prefab Overrides an
/// </summary>
[InitializeOnLoad]
public static class PlayModeChangesTracker
{
    private static Dictionary<int, TransformSnapshot> snapshots = new();
    private static Dictionary<int, HashSet<string>> selectedProperties = new();

    static PlayModeChangesTracker()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            CaptureSnapshots();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            ApplyChangesFromStoreToEditMode();
        }
    }

    private static void CaptureSnapshots()
    {
        snapshots.Clear();
        selectedProperties.Clear();

        foreach (GameObject go in GameObject.FindObjectsOfType<GameObject>())
        {
            int id = go.GetInstanceID();
            snapshots[id] = new TransformSnapshot(go);
        }
    }

    private static void RecordSelectedChangesToStore()
    {
        var store = PlayModeTransformChangesStore.LoadOrCreate();
        store.Clear();

        if (selectedProperties.Count == 0)
            return;

        foreach (var kvp in selectedProperties)
        {
            int id = kvp.Key;
            GameObject go = EditorUtility.InstanceIDToObject(id) as GameObject;

            if (go == null || !snapshots.ContainsKey(id))
                continue;

            TransformSnapshot original = snapshots[id];
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

            string path = GetGameObjectPath(go.transform);

            var change = new PlayModeTransformChangesStore.TransformChange
            {
                scenePath = go.scene.path,
                objectPath = path,
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

    private static void ApplyChangesFromStoreToEditMode()
    {
        var store = PlayModeTransformChangesStore.LoadExisting();
        if (store == null || store.changes == null || store.changes.Count == 0)
            return;

        foreach (var change in store.changes)
        {
            if (string.IsNullOrEmpty(change.scenePath))
                continue;

            var scene = EditorSceneManager.GetSceneByPath(change.scenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(change.scenePath, OpenSceneMode.Additive);
            }

            var go = FindInSceneByPath(scene, change.objectPath);
            if (go == null)
                continue;

            var t = go.transform;
            var rt = t as RectTransform;

            Undo.RecordObject(t, "Apply Play Mode Transform Changes");

            foreach (var prop in change.modifiedProperties)
            {
                ApplyPropertyToTransform(t, rt, change, prop);
            }

            EditorUtility.SetDirty(go);
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        store.Clear();
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

    public static TransformSnapshot GetSnapshot(int instanceID)
    {
        return snapshots.TryGetValue(instanceID, out var snap) ? snap : null;
    }

    public static void SetSnapshot(int instanceID, TransformSnapshot snapshot)
    {
        if (snapshot == null)
            return;
        snapshots[instanceID] = snapshot;
    }

    public static void ToggleProperty(int instanceID, string property)
    {
        if (!selectedProperties.ContainsKey(instanceID))
            selectedProperties[instanceID] = new HashSet<string>();

        if (selectedProperties[instanceID].Contains(property))
            selectedProperties[instanceID].Remove(property);
        else
            selectedProperties[instanceID].Add(property);
    }

    public static bool IsPropertySelected(int instanceID, string property)
    {
        return selectedProperties.ContainsKey(instanceID) &&
               selectedProperties[instanceID].Contains(property);
    }

    public static void RevertAll(int instanceID)
    {
        if (selectedProperties.ContainsKey(instanceID))
            selectedProperties[instanceID].Clear();
    }

    public static void ApplyAll(int instanceID, GameObject go)
    {
        if (!snapshots.ContainsKey(instanceID))
            return;

        TransformSnapshot original = snapshots[instanceID];
        TransformSnapshot current = new TransformSnapshot(go);

        if (!selectedProperties.ContainsKey(instanceID))
            selectedProperties[instanceID] = new HashSet<string>();

        selectedProperties[instanceID].Clear();

        foreach (var change in GetChangedProperties(original, current))
        {
            selectedProperties[instanceID].Add(change);
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
}

// -------------------------
// PlayModeChangesInspector (Header-Button + Transform-Vergleich)
// -------------------------

/// <summary>
/// Inspector GUI genau wie Prefab Overrides - ganz oben im Inspector
/// </summary>
[InitializeOnLoad]
public class PlayModeChangesInspector
{
    static PlayModeChangesInspector()
    {
        Editor.finishedDefaultHeaderGUI += OnPostHeaderGUI;
    }

    private static void OnPostHeaderGUI(Editor editor)
    {
        if (!Application.isPlaying)
            return;

        if (editor == null || editor.target == null)
            return;

        GameObject go = editor.target as GameObject;
        if (go == null)
        {
            var comp = editor.target as Component;
            if (comp != null)
            {
                go = comp.gameObject;
            }
        }

        if (go == null)
            return;

        int id = go.GetInstanceID();

        TransformSnapshot original = PlayModeChangesTracker.GetSnapshot(id);
        TransformSnapshot current = null;
        List<string> changes = null;

        if (original == null)
        {
            original = new TransformSnapshot(go);
            PlayModeChangesTracker.SetSnapshot(id, original);
        }

        current = new TransformSnapshot(go);
        changes = PlayModeChangesTracker.GetChangedProperties(original, current);

        DrawPlayModeOverridesHeader(go, id, original, current, changes);
    }

    private static void DrawPlayModeOverridesHeader(GameObject go, int id, TransformSnapshot original, TransformSnapshot current, List<string> changes)
    {
        bool hasChanges = changes != null && changes.Count > 0;

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        using (new EditorGUI.DisabledScope(!hasChanges))
        {
            GUIContent buttonContent = new GUIContent("Play Mode Overrides");
            Rect buttonRect = GUILayoutUtility.GetRect(buttonContent, EditorStyles.miniButton, GUILayout.Width(140f));
            if (GUI.Button(buttonRect, buttonContent, EditorStyles.miniButton))
            {
                PlayModeOverridesWindow.Open(go, id, original, current, changes);
                //dualinspector
            }
        }
        GUILayout.FlexibleSpace();

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(2);
    }

    //internal static void DrawTransformBeforeAfter(GameObject go, int id, TransformSnapshot original, TransformSnapshot current, List<string> changes)
    //{
    //    EditorGUILayout.BeginHorizontal();

    //    EditorGUILayout.BeginVertical();
    //    EditorGUILayout.LabelField("Before", EditorStyles.boldLabel);
    //    using (new EditorGUI.DisabledScope(true))
    //    {
    //        DrawTransformSnapshotGUI(original, changes);
    //    }
    //    EditorGUILayout.EndVertical();

    //    EditorGUILayout.Space(10);

    //    EditorGUILayout.BeginVertical();
    //    EditorGUILayout.BeginHorizontal();
    //    EditorGUILayout.LabelField("After", EditorStyles.boldLabel);
    //    GUILayout.FlexibleSpace();
    //    if (GUILayout.Button("Revert", GUILayout.Width(70)))
    //    {
    //        RevertTransform(go.transform, original, changes);
    //    }
    //    if (GUILayout.Button("Apply", GUILayout.Width(70)))
    //    {
    //        PlayModeChangesTracker.ApplyAll(id, go);
    //        PlayModeChangesTracker.PersistSelectedChangesForAll();
    //    }
    //    EditorGUILayout.EndHorizontal();

    //    DrawTransformCurrentGUI(go.transform, changes);

    //    EditorGUILayout.EndVertical();

    //    EditorGUILayout.EndHorizontal();
    //}

    private static void DrawTransformSnapshotGUI(TransformSnapshot snapshot, List<string> changedProps)
    {
        if (snapshot == null)
            return;

        bool positionChanged = changedProps.Contains("position");
        bool rotationChanged = changedProps.Contains("rotation");
        bool scaleChanged = changedProps.Contains("scale");

        Color oldColor = GUI.color;

        if (positionChanged)
            GUI.color = new Color(0.3f, 0.6f, 1f);
        EditorGUILayout.Vector3Field("Position", snapshot.position);
        GUI.color = oldColor;

        if (rotationChanged)
            GUI.color = new Color(0.3f, 0.6f, 1f);
        Vector3 beforeEuler = snapshot.rotation.eulerAngles;
        EditorGUILayout.Vector3Field("Rotation", beforeEuler);
        GUI.color = oldColor;

        if (scaleChanged)
            GUI.color = new Color(0.3f, 0.6f, 1f);
        EditorGUILayout.Vector3Field("Scale", snapshot.scale);
        GUI.color = oldColor;

        if (snapshot.isRectTransform)
        {
            bool anchoredPositionChanged = changedProps.Contains("anchoredPosition");
            bool anchoredPosition3DChanged = changedProps.Contains("anchoredPosition3D");
            bool anchorMinChanged = changedProps.Contains("anchorMin");
            bool anchorMaxChanged = changedProps.Contains("anchorMax");
            bool pivotChanged = changedProps.Contains("pivot");
            bool sizeDeltaChanged = changedProps.Contains("sizeDelta");
            bool offsetMinChanged = changedProps.Contains("offsetMin");
            bool offsetMaxChanged = changedProps.Contains("offsetMax");

            if (anchoredPositionChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            EditorGUILayout.Vector2Field("Anchored Position", snapshot.anchoredPosition);
            GUI.color = oldColor;

            if (anchoredPosition3DChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            EditorGUILayout.Vector3Field("Anchored Position 3D", snapshot.anchoredPosition3D);
            GUI.color = oldColor;

            if (anchorMinChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            EditorGUILayout.Vector2Field("Anchor Min", snapshot.anchorMin);
            GUI.color = oldColor;

            if (anchorMaxChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            EditorGUILayout.Vector2Field("Anchor Max", snapshot.anchorMax);
            GUI.color = oldColor;

            if (pivotChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            EditorGUILayout.Vector2Field("Pivot", snapshot.pivot);
            GUI.color = oldColor;

            if (sizeDeltaChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            EditorGUILayout.Vector2Field("Size Delta", snapshot.sizeDelta);
            GUI.color = oldColor;

            if (offsetMinChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            EditorGUILayout.Vector2Field("Offset Min", snapshot.offsetMin);
            GUI.color = oldColor;

            if (offsetMaxChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            EditorGUILayout.Vector2Field("Offset Max", snapshot.offsetMax);
            GUI.color = oldColor;
        }
    }

    private static void DrawTransformCurrentGUI(Transform transform, List<string> changedProps)
    {
        if (transform == null)
            return;

        bool positionChanged = changedProps.Contains("position");
        bool rotationChanged = changedProps.Contains("rotation");
        bool scaleChanged = changedProps.Contains("scale");

        Color oldColor = GUI.color;

        if (positionChanged)
            GUI.color = new Color(0.3f, 0.6f, 1f);
        Vector3 newPosition = EditorGUILayout.Vector3Field("Position", transform.localPosition);
        if (newPosition != transform.localPosition)
            transform.localPosition = newPosition;
        GUI.color = oldColor;

        if (rotationChanged)
            GUI.color = new Color(0.3f, 0.6f, 1f);
        Vector3 newEuler = EditorGUILayout.Vector3Field("Rotation", transform.localRotation.eulerAngles);
        if (newEuler != transform.localRotation.eulerAngles)
            transform.localRotation = Quaternion.Euler(newEuler);
        GUI.color = oldColor;

        if (scaleChanged)
            GUI.color = new Color(0.3f, 0.6f, 1f);
        Vector3 newScale = EditorGUILayout.Vector3Field("Scale", transform.localScale);
        if (newScale != transform.localScale)
            transform.localScale = newScale;
        GUI.color = oldColor;

        RectTransform rt = transform as RectTransform;
        if (rt != null)
        {
            bool anchoredPositionChanged = changedProps.Contains("anchoredPosition");
            bool anchoredPosition3DChanged = changedProps.Contains("anchoredPosition3D");
            bool anchorMinChanged = changedProps.Contains("anchorMin");
            bool anchorMaxChanged = changedProps.Contains("anchorMax");
            bool pivotChanged = changedProps.Contains("pivot");
            bool sizeDeltaChanged = changedProps.Contains("sizeDelta");
            bool offsetMinChanged = changedProps.Contains("offsetMin");
            bool offsetMaxChanged = changedProps.Contains("offsetMax");

            if (anchoredPositionChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            Vector2 newAnchoredPosition = EditorGUILayout.Vector2Field("Anchored Position", rt.anchoredPosition);
            if (newAnchoredPosition != rt.anchoredPosition)
                rt.anchoredPosition = newAnchoredPosition;
            GUI.color = oldColor;

            if (anchoredPosition3DChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            Vector3 newAnchoredPosition3D = EditorGUILayout.Vector3Field("Anchored Position 3D", rt.anchoredPosition3D);
            if (newAnchoredPosition3D != rt.anchoredPosition3D)
                rt.anchoredPosition3D = newAnchoredPosition3D;
            GUI.color = oldColor;

            if (anchorMinChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            Vector2 newAnchorMin = EditorGUILayout.Vector2Field("Anchor Min", rt.anchorMin);
            if (newAnchorMin != rt.anchorMin)
                rt.anchorMin = newAnchorMin;
            GUI.color = oldColor;

            if (anchorMaxChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            Vector2 newAnchorMax = EditorGUILayout.Vector2Field("Anchor Max", rt.anchorMax);
            if (newAnchorMax != rt.anchorMax)
                rt.anchorMax = newAnchorMax;
            GUI.color = oldColor;

            if (pivotChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            Vector2 newPivot = EditorGUILayout.Vector2Field("Pivot", rt.pivot);
            if (newPivot != rt.pivot)
                rt.pivot = newPivot;
            GUI.color = oldColor;

            if (sizeDeltaChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            Vector2 newSizeDelta = EditorGUILayout.Vector2Field("Size Delta", rt.sizeDelta);
            if (newSizeDelta != rt.sizeDelta)
                rt.sizeDelta = newSizeDelta;
            GUI.color = oldColor;

            if (offsetMinChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            Vector2 newOffsetMin = EditorGUILayout.Vector2Field("Offset Min", rt.offsetMin);
            if (newOffsetMin != rt.offsetMin)
                rt.offsetMin = newOffsetMin;
            GUI.color = oldColor;

            if (offsetMaxChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            Vector2 newOffsetMax = EditorGUILayout.Vector2Field("Offset Max", rt.offsetMax);
            if (newOffsetMax != rt.offsetMax)
                rt.offsetMax = newOffsetMax;
            GUI.color = oldColor;
        }
    }

    internal static string GetPropertyDisplayName(string property)
    {
        switch (property)
        {
            case "position": return "Position";
            case "rotation": return "Rotation";
            case "scale": return "Scale";
            case "anchoredPosition": return "Anchored Position";
            case "anchoredPosition3D": return "Anchored Position 3D";
            case "anchorMin": return "Anchor Min";
            case "anchorMax": return "Anchor Max";
            case "pivot": return "Pivot";
            case "sizeDelta": return "Size Delta";
            case "offsetMin": return "Offset Min";
            case "offsetMax": return "Offset Max";
            default: return property;
        }
    }

    internal static string GetPropertyTooltip(string property, TransformSnapshot original, TransformSnapshot current)
    {
        string originalValue = GetValueDisplay(property, original);
        string currentValue = GetValueDisplay(property, current);
        return $"Original: {originalValue}\nCurrent: {currentValue}";
    }

    private static string GetValueDisplay(string property, TransformSnapshot snapshot)
    {
        switch (property)
        {
            case "position": return FormatVector3(snapshot.position);
            case "rotation": return FormatQuaternion(snapshot.rotation);
            case "scale": return FormatVector3(snapshot.scale);
            case "anchoredPosition": return FormatVector2(snapshot.anchoredPosition);
            case "anchoredPosition3D": return FormatVector3(snapshot.anchoredPosition3D);
            case "anchorMin": return FormatVector2(snapshot.anchorMin);
            case "anchorMax": return FormatVector2(snapshot.anchorMax);
            case "pivot": return FormatVector2(snapshot.pivot);
            case "sizeDelta": return FormatVector2(snapshot.sizeDelta);
            case "offsetMin": return FormatVector2(snapshot.offsetMin);
            case "offsetMax": return FormatVector2(snapshot.offsetMax);
            default: return "";
        }
    }

    private static string FormatVector3(Vector3 v)
    {
        return $"({v.x:F2}, {v.y:F2}, {v.z:F2})";
    }

    private static string FormatVector2(Vector2 v)
    {
        return $"({v.x:F2}, {v.y:F2})";
    }

    private static string FormatQuaternion(Quaternion q)
    {
        Vector3 euler = q.eulerAngles;
        return $"({euler.x:F1}°, {euler.y:F1}°, {euler.z:F1}°)";
    }

    internal static void RevertTransform(Transform transform, TransformSnapshot original, List<string> changedProps)
    {
        if (transform == null || original == null || changedProps == null)
            return;

        RectTransform rt = transform as RectTransform;

        foreach (string prop in changedProps)
        {
            switch (prop)
            {
                case "position":
                    transform.localPosition = original.position;
                    break;
                case "rotation":
                    transform.localRotation = original.rotation;
                    break;
                case "scale":
                    transform.localScale = original.scale;
                    break;
                case "anchoredPosition":
                    if (rt) rt.anchoredPosition = original.anchoredPosition;
                    break;
                case "anchoredPosition3D":
                    if (rt) rt.anchoredPosition3D = original.anchoredPosition3D;
                    break;
                case "anchorMin":
                    if (rt) rt.anchorMin = original.anchorMin;
                    break;
                case "anchorMax":
                    if (rt) rt.anchorMax = original.anchorMax;
                    break;
                case "pivot":
                    if (rt) rt.pivot = original.pivot;
                    break;
                case "sizeDelta":
                    if (rt) rt.sizeDelta = original.sizeDelta;
                    break;
                case "offsetMin":
                    if (rt) rt.offsetMin = original.offsetMin;
                    break;
                case "offsetMax":
                    if (rt) rt.offsetMax = original.offsetMax;
                    break;
            }
        }
    }
}
public class PlayModeOverridesWindow : EditorWindow
{
    private GameObject _gameObject;
    private int _instanceId;
    private TransformSnapshot _original;
    private TransformSnapshot _current;
    private List<string> _changedProperties;

    private bool _showTransformComparison;

    public static void Open(GameObject gameObject, int instanceId, TransformSnapshot original, TransformSnapshot current, List<string> changedProperties)
    {
        var window = GetWindow<PlayModeOverridesWindow>("Play Mode Overrides");
        window._gameObject = gameObject;
        window._instanceId = instanceId;
        window._original = original;
        window._current = current;
        window._changedProperties = changedProperties;
        window.position = new Rect(200, 200, 360f, 420f);
        window.Show();
    }

    void OnGUI()
    {
        // Obere Hälfte: Overrides-Header + Liste
        DrawHeader();

        GUILayout.Space(4);
        GUILayout.Space(4);
        DrawDemoTransformRow();

        // Trennlinie zwischen oberem und unterem Bereich
        GUILayout.Space(4);
        Rect splitterRect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(splitterRect, new Color(0.2f, 0.2f, 0.2f, 0.8f));

        // Untere Hälfte: Transform-Vergleich (nur wenn angefordert und Daten vorhanden)
        if (_showTransformComparison && _gameObject != null && _original != null && _current != null && _changedProperties != null && _changedProperties.Count > 0)
        {
            GUILayout.Space(4);

            // Gleiche Darstellung wie an anderer Stelle: Before/After Transform
            PlayModeChangesInspector.DrawTransformBeforeAfter(_gameObject, _instanceId, _original, _current, _changedProperties);
        }

        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Revert All", GUILayout.Width(120f)))
        {
            // Frontend-only
        }

        if (GUILayout.Button("Apply All", GUILayout.Width(120f)))
        {
            // Frontend-only
        }

        GUILayout.EndHorizontal();
        GUILayout.Space(6);
    }

    void DrawHeader()
    {
        const float headerHeight = 60f;
        const float leftMargin = 6f;

        Rect headerRect = GUILayoutUtility.GetRect(20, 10000, headerHeight, headerHeight);

        Color bgColor = EditorGUIUtility.isProSkin
            ? new Color(0.5f, 0.5f, 0.5f, 0.2f)
            : new Color(0.9f, 0.9f, 0.9f, 0.6f);
        EditorGUI.DrawRect(headerRect, bgColor);

        GUIStyle boldRight = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleRight
        };

        GUIContent titleContent = new GUIContent("Review, Revert or Apply Overrides");
        float titleWidth = boldRight.CalcSize(titleContent).x;
        Rect titleRect = new Rect(headerRect.x + leftMargin, headerRect.y, titleWidth, headerRect.height);
        titleRect.height = EditorGUIUtility.singleLineHeight;
        GUI.Label(titleRect, titleContent, boldRight);

        float labelWidth = EditorStyles.label.CalcSize(new GUIContent("on")).x;

        Rect lineRect = headerRect;
        lineRect.height = EditorGUIUtility.singleLineHeight;
        lineRect.y += 20f;

        Rect labelRect = new Rect(headerRect.x + leftMargin, lineRect.y, labelWidth, lineRect.height);
        Rect contentRect = lineRect;
        contentRect.xMin = labelRect.xMax;

        GUI.Label(labelRect, "on", EditorStyles.label);
        GUI.Label(contentRect, _gameObject != null ? _gameObject.name : "<none>", EditorStyles.label);

        labelRect.y += EditorGUIUtility.singleLineHeight;
        contentRect.y += EditorGUIUtility.singleLineHeight;

        string stageName = "Play Mode";
        if (_gameObject != null && _gameObject.scene.IsValid())
            stageName = string.IsNullOrEmpty(_gameObject.scene.name) ? _gameObject.scene.path : _gameObject.scene.name;

        GUI.Label(labelRect, "in", EditorStyles.label);
        GUI.Label(contentRect, stageName, EditorStyles.label);
    }

    void DrawDemoTransformRow()
    {
        const float rowHeight = 18f;

        Rect rowRect = GUILayoutUtility.GetRect(100, 10000, rowHeight, rowHeight);

        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(rowRect, new Color(0.22f, 0.22f, 0.22f, EditorGUIUtility.isProSkin ? 0.6f : 0.2f));
        }

        Rect labelRect = rowRect;
        labelRect.xMin += 16f;
        GUI.Label(labelRect, "Transform", EditorStyles.label);

        if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
        {
            if (Event.current.button == 0)
            {
                Event.current.Use();
                _showTransformComparison = true;
            }
        }
    }
}



// -------------------------
// PlayModeOverridesPopup (reines Frontend-Popup)
// -------------------------

internal class PlayModeOverridesPopup : PopupWindowContent
{
    private readonly GameObject _gameObject;

    public PlayModeOverridesPopup(GameObject gameObject, int instanceId, TransformSnapshot original, TransformSnapshot current, List<string> changedProperties)
    {
        _gameObject = gameObject;
    }

    public override Vector2 GetWindowSize()
    {
        return new Vector2(360f, 420f);
    }

    public override void OnGUI(Rect rect)
    {
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            editorWindow.Close();
            GUIUtility.ExitGUI();
        }

        DrawHeader(rect);

        GUILayout.Space(4);
        GUILayout.Space(4);
        DrawDemoTransformRow();

        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Revert All", GUILayout.Width(120f)))
        {
        }

        if (GUILayout.Button("Apply All", GUILayout.Width(120f)))
        {
        }

        GUILayout.EndHorizontal();
        GUILayout.Space(6);
    }

    private void DrawHeader(Rect totalRect)
    {
        const float headerHeight = 60f;
        const float leftMargin = 6f;

        Rect headerRect = GUILayoutUtility.GetRect(20, 10000, headerHeight, headerHeight);

        Color bgColor = EditorGUIUtility.isProSkin
            ? new Color(0.5f, 0.5f, 0.5f, 0.2f)
            : new Color(0.9f, 0.9f, 0.9f, 0.6f);
        EditorGUI.DrawRect(headerRect, bgColor);

        GUIStyle boldRight = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleRight
        };

        GUIContent titleContent = new GUIContent("Review, Revert or Apply Overrides");
        float titleWidth = boldRight.CalcSize(titleContent).x;
        Rect titleRect = new Rect(headerRect.x + leftMargin, headerRect.y, titleWidth, headerRect.height);
        titleRect.height = EditorGUIUtility.singleLineHeight;
        GUI.Label(titleRect, titleContent, boldRight);

        float labelWidth = EditorStyles.label.CalcSize(new GUIContent("on")).x;

        Rect lineRect = headerRect;
        lineRect.height = EditorGUIUtility.singleLineHeight;
        lineRect.y += 20f;

        Rect labelRect = new Rect(headerRect.x + leftMargin, lineRect.y, labelWidth, lineRect.height);
        Rect contentRect = lineRect;
        contentRect.xMin = labelRect.xMax;

        GUI.Label(labelRect, "on", EditorStyles.label);
        GUI.Label(contentRect, _gameObject != null ? _gameObject.name : "<none>", EditorStyles.label);

        labelRect.y += EditorGUIUtility.singleLineHeight;
        contentRect.y += EditorGUIUtility.singleLineHeight;

        string stageName = "Play Mode";
        if (_gameObject != null && _gameObject.scene.IsValid())
            stageName = string.IsNullOrEmpty(_gameObject.scene.name) ? _gameObject.scene.path : _gameObject.scene.name;

        GUI.Label(labelRect, "in", EditorStyles.label);
        GUI.Label(contentRect, stageName, EditorStyles.label);
    }

    private void DrawDemoTransformRow()
    {
        const float rowHeight = 18f;

        Rect rowRect = GUILayoutUtility.GetRect(100, 10000, rowHeight, rowHeight);

        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(rowRect, new Color(0.22f, 0.22f, 0.22f, EditorGUIUtility.isProSkin ? 0.6f : 0.2f));
        }

        Rect labelRect = rowRect;
        labelRect.xMin += 16f;
        GUI.Label(labelRect, "Transform", EditorStyles.label);

        if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
        {
            if (Event.current.button == 0)
            {
                Event.current.Use();
                if (_gameObject != null)
                {
                    var transform = _gameObject.transform;
                    if (transform != null)
                    {
                        DualInspectorWindow.ShowForComponent(transform);
                    }
                }
            }
        }
    }
}

// -------------------------
// DualInspectorWindow
// -------------------------

public class DualInspectorWindow : EditorWindow
{
    private GameObject targetGO;
    private GameObject snapshotGO;

    private bool singleComponentMode;
    private Component singleTargetComponent;
    private Component singleSnapshotComponent;

    private readonly List<Editor> leftEditors = new();
    private readonly List<Editor> rightEditors = new();

    private Vector2 sharedScroll;

    private const float ToolbarHeight = 20f;
    private const float Splitter = 6f;
    private const float ScrollbarWidth = 16f;

    [MenuItem("Tools/Dual Inspector")]
    static void Open()
    {
        GetWindow<DualInspectorWindow>("Dual Inspector");
    }

    void OnEnable()
    {
        Selection.selectionChanged += Rebuild;
        Rebuild();
    }

    void OnDisable()
    {
        Selection.selectionChanged -= Rebuild;
        Cleanup();
    }

    void Cleanup()
    {
        foreach (var e in leftEditors) DestroyImmediate(e);
        foreach (var e in rightEditors) DestroyImmediate(e);
        leftEditors.Clear();
        rightEditors.Clear();

        if (snapshotGO != null)
            DestroyImmediate(snapshotGO);
    }

    void Rebuild()
    {
        if (singleComponentMode)
        {
            if (singleTargetComponent != null)
                BuildForSingleComponent(singleTargetComponent);
            return;
        }

        Cleanup();

        targetGO = Selection.activeGameObject;
        if (targetGO == null)
            return;

        snapshotGO = Instantiate(targetGO);
        snapshotGO.hideFlags = HideFlags.HideAndDontSave;

        BuildEditors(snapshotGO, leftEditors);
        BuildEditors(targetGO, rightEditors);

        sharedScroll = Vector2.zero;
        Repaint();
    }

    public static void ShowForComponent(Component component)
    {
        if (component == null)
            return;

        var window = GetWindow<DualInspectorWindow>("Dual Inspector");
        window.singleComponentMode = true;
        window.singleTargetComponent = component;
        window.BuildForSingleComponent(component);
        window.Focus();
    }

    void BuildForSingleComponent(Component component)
    {
        Cleanup();

        singleTargetComponent = component;
        targetGO = component != null ? component.gameObject : null;

        if (targetGO == null || component == null)
            return;

        snapshotGO = Instantiate(targetGO);
        snapshotGO.hideFlags = HideFlags.HideAndDontSave;

        var type = component.GetType();
        var targetComponents = targetGO.GetComponents(type);
        var snapshotComponents = snapshotGO.GetComponents(type);
        int index = Array.IndexOf(targetComponents, component);

        singleSnapshotComponent = null;
        if (index >= 0 && index < snapshotComponents.Length)
        {
            singleSnapshotComponent = snapshotComponents[index];
        }

        if (singleSnapshotComponent != null)
            leftEditors.Add(Editor.CreateEditor(singleSnapshotComponent));

        rightEditors.Add(Editor.CreateEditor(component));

        sharedScroll = Vector2.zero;
        Repaint();
    }

    void BuildEditors(GameObject go, List<Editor> list)
    {
        foreach (var c in go.GetComponents<Component>())
        {
            if (c == null) continue;
            list.Add(Editor.CreateEditor(c));
        }
    }

    void OnGUI()
    {
        if (targetGO == null)
        {
            EditorGUILayout.LabelField("No GameObject selected.");
            return;
        }

        DrawToolbar();

        Rect contentRect = new Rect(
            0,
            ToolbarHeight,
            position.width,
            position.height - ToolbarHeight
        );

        float columnWidth = (contentRect.width - Splitter) * 0.5f;

        Rect leftRect = new Rect(
            contentRect.x,
            contentRect.y,
            columnWidth,
            contentRect.height
        );

        Rect rightRect = new Rect(
            contentRect.x + columnWidth + Splitter,
            contentRect.y,
            columnWidth,
            contentRect.height
        );

        DrawInspectorColumn(leftRect, leftEditors, editable: false);
        DrawInspectorColumn(rightRect, rightEditors, editable: true);
    }

    void DrawToolbar()
    {
        Rect rect = new Rect(0, 0, position.width, ToolbarHeight);
        GUILayout.BeginArea(rect, EditorStyles.toolbar);

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Revert", EditorStyles.toolbarButton))
        {
            Undo.RecordObject(targetGO, "Revert Changes");
            EditorUtility.CopySerialized(snapshotGO, targetGO);
            Rebuild();
        }

        if (GUILayout.Button("Apply", EditorStyles.toolbarButton))
        {
            Rebuild();
        }

        GUILayout.EndArea();
    }

    void DrawInspectorColumn(
        Rect rect,
        List<Editor> editors,
        bool editable
    )
    {
        GUI.Box(rect, GUIContent.none);

        Rect viewRect = new Rect(
            0,
            0,
            rect.width - ScrollbarWidth,
            CalculateViewHeight(editors)
        );

        sharedScroll = GUI.BeginScrollView(rect, sharedScroll, viewRect);

        GUI.enabled = editable;
        GUILayout.BeginArea(viewRect);

        foreach (var ed in editors)
        {
            if (ed == null) continue;

            DrawHeader(ed.target);
            ed.OnInspectorGUI();
            GUILayout.Space(8);
        }

        GUILayout.EndArea();
        GUI.enabled = true;

        GUI.EndScrollView();
    }

    float CalculateViewHeight(List<Editor> editors)
    {
        return Mathf.Max(1000, editors.Count * 120);
    }

    void DrawHeader(UnityEngine.Object target)
    {
        var content = EditorGUIUtility.ObjectContent(target, target.GetType());
        Rect rect = GUILayoutUtility.GetRect(16, 22, GUILayout.ExpandWidth(true));

        GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

        if (content.image)
            GUI.DrawTexture(
                new Rect(rect.x + 6, rect.y + 3, 16, 16),
                content.image
            );

        EditorGUI.LabelField(
            new Rect(rect.x + 26, rect.y + 3, rect.width, 16),
            content.text,
            EditorStyles.boldLabel
        );
    }
}

// -------------------------
// PlayModeChangesLogWindow
// -------------------------

public class PlayModeChangesLogWindow : EditorWindow
{
    private Vector2 _scrollPos;

    [MenuItem("Tools/Play Mode Changes Log")]
    public static void ShowWindow()
    {
        var window = GetWindow<PlayModeChangesLogWindow>(false, "Play Mode Changes Log", true);
        window.Show();
    }

    private void OnGUI()
    {
        var store = PlayModeTransformChangesStore.LoadExisting();

        if (store == null)
        {
            EditorGUILayout.HelpBox("Kein PlayModeTransformChangesStore gefunden. Klicke im Play Mode im Inspector auf 'Apply', um Änderungen zu speichern.", MessageType.Info);
            return;
        }

        if (store.changes == null || store.changes.Count == 0)
        {
            EditorGUILayout.HelpBox("Der Store enthält aktuell keine gespeicherten Änderungen.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Gespeicherte Play-Mode-Änderungen", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        for (int i = 0; i < store.changes.Count; i++)
        {
            var change = store.changes[i];
            DrawChangeEntry(change, i);
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawChangeEntry(PlayModeTransformChangesStore.TransformChange change, int index)
    {
        EditorGUILayout.BeginVertical("HelpBox");

        string sceneName = string.IsNullOrEmpty(change.scenePath)
            ? "(Keine Szene)"
            : Path.GetFileNameWithoutExtension(change.scenePath);

        EditorGUILayout.LabelField($"#{index + 1}  {sceneName}", EditorStyles.boldLabel);

        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.LabelField("GameObject-Pfad:", change.objectPath);

            if (change.modifiedProperties == null || change.modifiedProperties.Count == 0)
            {
                EditorGUILayout.LabelField("(Keine Properties vermerkt)", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("Geänderte Properties:", EditorStyles.miniBoldLabel);
                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (var prop in change.modifiedProperties)
                    {
                        EditorGUILayout.LabelField(PlayModeChangesInspector.GetPropertyDisplayName(prop));
                    }
                }
            }
        }

        EditorGUILayout.EndVertical();
    }
}