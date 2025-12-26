using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Speichert Transform/RectTransform-Änderungen zwischen Play Mode und Edit Mode.
/// </summary>
public class PlayModeTransformChangesStore : ScriptableObject
{
    [System.Serializable]
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

    private const string AssetPath = "Assets/01_Skripts/Editor/PlayModeTransformChangesStore.asset";

    public static PlayModeTransformChangesStore LoadOrCreate()
    {
        var store = AssetDatabase.LoadAssetAtPath<PlayModeTransformChangesStore>(AssetPath);
        if (store == null)
        {
            store = ScriptableObject.CreateInstance<PlayModeTransformChangesStore>();
            AssetDatabase.CreateAsset(store, AssetPath);
            AssetDatabase.SaveAssets();
        }
        return store;
    }

    public void Clear()
    {
        changes.Clear();
        EditorUtility.SetDirty(this);
    }
}
