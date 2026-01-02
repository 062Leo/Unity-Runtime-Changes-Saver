using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RuntimeChangesSaver.Editor
{
    public class ComponentOriginalStore : ScriptableObject
    {
        [Serializable]
        public class ComponentOriginal
        {
            public string scenePath;
            public string objectPath;
            public string componentType;
            public int componentIndex;

            public List<string> propertyPaths = new List<string>();
            public List<string> serializedValues = new List<string>();
            public List<string> valueTypes = new List<string>();
        }

        public List<ComponentOriginal> entries = new List<ComponentOriginal>();

        public static ComponentOriginalStore LoadExisting()
        {
            string[] guids = AssetDatabase.FindAssets("t:ComponentOriginalStore");
            if (guids is { Length: > 0 })
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<ComponentOriginalStore>(path);
            }

            return null;
        }

        public static ComponentOriginalStore LoadOrCreate()
        {
            var store = LoadExisting();
            if (store == null)
            {
                string assetPath = GetDefaultAssetPath();
                store = CreateInstance<ComponentOriginalStore>();
                AssetDatabase.CreateAsset(store, assetPath);
                AssetDatabase.SaveAssets();
            }

            return store;
        }

        private static string GetRuntimeChangesSaverRootFolder()
        {
            string[] scriptGuids = AssetDatabase.FindAssets("ComponentOriginalStore t:Script");
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

            string assetPath = Path.Combine(soFolder, "ComponentOriginalStore.asset");
            return assetPath.Replace("\\", "/");
        }

        public void Clear()
        {
            entries.Clear();
            EditorUtility.SetDirty(this);
        }
    }
}
