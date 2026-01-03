using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RuntimeChangesSaver.Editor
{
    public class GameObjectNameOriginalStore : ScriptableObject
    {
        [Serializable]
        public class NameOriginal
        {
            public string scenePath;
            public string objectPath;
            public string globalObjectId;
            public string originalName;
        }

        public List<NameOriginal> entries = new List<NameOriginal>();

        public static GameObjectNameOriginalStore LoadExisting()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameObjectNameOriginalStore");
            if (guids is { Length: > 0 })
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<GameObjectNameOriginalStore>(path);
            }

            return null;
        }

        public static GameObjectNameOriginalStore LoadOrCreate()
        {
            var store = LoadExisting();
            if (store == null)
            {
                string assetPath = GetDefaultAssetPath();
                store = CreateInstance<GameObjectNameOriginalStore>();
                AssetDatabase.CreateAsset(store, assetPath);
                AssetDatabase.SaveAssets();
            }

            return store;
        }

        private static string GetRuntimeChangesSaverRootFolder()
        {
            string[] scriptGuids = AssetDatabase.FindAssets("GameObjectNameOriginalStore t:Script");

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

                        dir = Path.GetDirectoryName(dir)?.Replace("\\", "/");
                    }
                }
            }

            return "Assets/RuntimeChangesSaver";
        }

        private static string GetDefaultAssetPath()
        {
            string rootFolder = GetRuntimeChangesSaverRootFolder();
            string soDir = rootFolder + "/Scriptable_Objects";

            if (!AssetDatabase.IsValidFolder(soDir))
            {
                string parentPath = Path.GetDirectoryName(soDir)?.Replace("\\", "/");
                string folderName = Path.GetFileName(soDir);
                if (!string.IsNullOrEmpty(parentPath))
                {
                    AssetDatabase.CreateFolder(parentPath, folderName);
                }
            }

            return soDir + "/GameObjectNameOriginalStore.asset";
        }

        public void Clear()
        {
            entries.Clear();
            EditorUtility.SetDirty(this);
        }
    }
}
