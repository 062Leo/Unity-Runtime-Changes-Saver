using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Trackt Play Mode Änderungen und zeigt sie wie Prefab Overrides an
/// </summary>
[InitializeOnLoad]
public static class PlayModeChangesTracker
{
    private static Dictionary<int, TransformSnapshot> snapshots = new Dictionary<int, TransformSnapshot>();
    private static Dictionary<int, HashSet<string>> selectedProperties = new Dictionary<int, HashSet<string>>();

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

            // Nur tatsächlich veränderte Properties übernehmen (Schnittmenge)
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

    // Wird vom Inspector-UI aufgerufen, wenn der Benutzer auf "Apply" klickt
    public static void PersistSelectedChangesForAll()
    {
        RecordSelectedChangesToStore();
    }

    private static void ApplyChangesFromStoreToEditMode()
    {
        var store = AssetDatabase.LoadAssetAtPath<PlayModeTransformChangesStore>("Assets/01_Skripts/Editor/PlayModeTransformChangesStore.asset");
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
        return snapshots.ContainsKey(instanceID) ? snapshots[instanceID] : null;
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

        // Alle geänderten Properties selektieren
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

[System.Serializable]
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