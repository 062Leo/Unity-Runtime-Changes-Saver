using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RuntimeChangesSaver.Editor
{
    public class TransformOriginalStore : ScriptableObject
    {
        [Serializable]
        public class TransformOriginal
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
        }

        public List<TransformOriginal> entries = new List<TransformOriginal>();

        public static TransformOriginalStore LoadExisting()
        {
            string[] guids = AssetDatabase.FindAssets("t:TransformOriginalStore");
            if (guids is { Length: > 0 })
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<TransformOriginalStore>(path);
            }

            return null;
        }

        public static TransformOriginalStore LoadOrCreate()
        {
            var store = LoadExisting();
            if (store == null)
            {
                string assetPath = GetDefaultAssetPath();
                store = CreateInstance<TransformOriginalStore>();
                AssetDatabase.CreateAsset(store, assetPath);
                AssetDatabase.SaveAssets();
            }

            return store;
        }

        private static string GetRuntimeChangesSaverRootFolder()
        {
            string[] scriptGuids = AssetDatabase.FindAssets("TransformOriginalStore t:Script");

            if (scriptGuids is { Length: > 0 })
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuids[0]);
                if (!string.IsNullOrEmpty(scriptPath))
                {
                    string dir = Path.GetDirectoryName(scriptPath)?.Replace("\\", "/");

                    while (!string.IsNullOrEmpty(dir) && dir.StartsWith("Assets"))
                    {
                        string folderName = Path.GetFileName(dir);
                        if (folderName == "RuntimeChangesSaver")
                        {
                            return dir;
                        }

                        string parent = Path.GetDirectoryName(dir);
                        if (string.IsNullOrEmpty(parent))
                            break;

                        dir = parent.Replace("\\", "/");
                    }

                    return Path.GetDirectoryName(scriptPath)?.Replace("\\", "/");
                }
            }

            return "Assets";
        }

        private static string GetDefaultAssetPath()
        {
            string runtimeFolder = GetRuntimeChangesSaverRootFolder();
            string soFolder = runtimeFolder + "/Scriptable_Objects";

            if (!AssetDatabase.IsValidFolder(soFolder))
            {
                AssetDatabase.CreateFolder(runtimeFolder, "Scriptable_Objects");
            }

            string assetPath = Path.Combine(soFolder, "TransformOriginalStore.asset");
            return assetPath.Replace("\\", "/");
        }

        public void Clear()
        {
            entries.Clear();
            EditorUtility.SetDirty(this);
        }
    }
}
