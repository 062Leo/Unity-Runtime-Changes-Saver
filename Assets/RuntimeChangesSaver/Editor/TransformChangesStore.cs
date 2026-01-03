using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RuntimeChangesSaver.Editor
{
    public class TransformChangesStore : ScriptableObject
    {
        [Serializable] 
        public class TransformChange
        { 
            public string scenePath;
            public string objectPath;
            public string globalObjectId;
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

        public static TransformChangesStore LoadExisting()
        {
            string[] guids = AssetDatabase.FindAssets("t:TransformChangesStore");
            if (guids is { Length: > 0 })
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var store = AssetDatabase.LoadAssetAtPath<TransformChangesStore>(path);
                return store;
            }

            return null;
        }

        public static TransformChangesStore LoadOrCreate()
        {
            var store = LoadExisting();
            if (store == null)
            {
                string assetPath = GetDefaultAssetPath();
                store = CreateInstance<TransformChangesStore>();
                AssetDatabase.CreateAsset(store, assetPath);
                AssetDatabase.SaveAssets();
            }
            return store;
        }

        private static string GetRuntimeChangesSaverRootFolder()
        {
            // • Locate script asset
            // • Search for RuntimeChangesSaver folder

            string[] scriptGuids = AssetDatabase.FindAssets("TransformChangesStore t:Script");

            if (scriptGuids is { Length: > 0 })
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuids[0]);
                if (!string.IsNullOrEmpty(scriptPath))
                {
                    string dir = Path.GetDirectoryName(scriptPath)?.Replace("\\", "/");

                    // • Walk up until RuntimeChangesSaver or Assets root

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

                    // • Fallback: script folder

                    return Path.GetDirectoryName(scriptPath)?.Replace("\\", "/");
                }
            }

            // • Fallback: Assets root
            return "Assets";
        }

        private static string GetDefaultAssetPath()
        {
            // • Asset inside RuntimeChangesSaver/Scriptable_Objects
            // • Avoid hardcoded Assets hierarchy

            string runtimeFolder = GetRuntimeChangesSaverRootFolder();
            string soFolder = runtimeFolder + "/Scriptable_Objects";

            if (!AssetDatabase.IsValidFolder(soFolder))
            {
                AssetDatabase.CreateFolder(runtimeFolder, "Scriptable_Objects");
            }

            string assetPath = Path.Combine(soFolder, "TransformChangesStore.asset");

            return assetPath.Replace("\\", "/");
        }

        public void Clear()
        {
            changes.Clear();
            EditorUtility.SetDirty(this);
        }
    }
}