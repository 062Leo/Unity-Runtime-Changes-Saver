using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;




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
            var store = AssetDatabase.LoadAssetAtPath<PlayModeTransformChangesStore>(path);
            Debug.Log($"[TransformDebug][Store.LoadExisting] Found existing store at '{path}', changeCount={store.changes.Count}");
            return store;
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
        else
        {
            string existingPath = AssetDatabase.GetAssetPath(store);
            Debug.Log($"[TransformDebug][Store.LoadOrCreate] Using existing store at '{existingPath}', changeCount={store.changes.Count}");
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
        Debug.Log("[TransformDebug][Store.Clear] Cleared all stored transform changes");
    }
}