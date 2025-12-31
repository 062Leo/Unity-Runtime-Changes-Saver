using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RuntimeChangesSaver.Editor
{
    public class ComponentChangesStore : ScriptableObject
    {
        [Serializable]
        public class ComponentChange
        {
            public string scenePath;
            public string objectPath;
            public string componentType;
            public int componentIndex;

            public List<string> propertyPaths = new List<string>();
            public List<string> serializedValues = new List<string>();
            public List<string> valueTypes = new List<string>();

            // original baseline values for component
            public bool hasOriginalValues;
            public List<string> originalSerializedValues = new List<string>();
            public List<string> originalValueTypes = new List<string>();
        }

        public List<ComponentChange> changes = new List<ComponentChange>();

        public static ComponentChangesStore LoadExisting()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(ComponentChangesStore)}");
            if (guids is { Length: > 0 })
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<ComponentChangesStore>(path);
            }

            return null;
        }

        public static ComponentChangesStore LoadOrCreate()
        {
            var store = LoadExisting();
            if (store == null)
            {
                string assetPath = GetDefaultAssetPath();
                store = CreateInstance<ComponentChangesStore>();
                AssetDatabase.CreateAsset(store, assetPath);
                AssetDatabase.SaveAssets();
            }

            return store;
        }

        private static string GetRuntimeChangesSaverRootFolder()
        {
            // locate script asset, walk up to "RuntimeChangesSaver" folder under Assets
            string[] scriptGuids = AssetDatabase.FindAssets($"{nameof(ComponentChangesStore)} t:Script");
            if (scriptGuids is { Length: > 0 })
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuids[0]);
                if (!string.IsNullOrEmpty(scriptPath))
                {
                    string dir = Path.GetDirectoryName(scriptPath)?.Replace("\\", "/");

                    // walk upward from script until "RuntimeChangesSaver" or Assets root
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

                    // Fallback: no "RuntimeChangesSaver" folder
                    // Use script folder as root
                    return Path.GetDirectoryName(scriptPath)?.Replace("\\", "/");
                }
            }

            // Final fallback: Assets
            return "Assets";
        }

        private static string GetDefaultAssetPath()
        {
            // Store asset inside RuntimeChangesSaver folder
            // store asset inside RuntimeChangesSaver folder, avoid hardcoded Assets path
            string runtimeFolder = GetRuntimeChangesSaverRootFolder();
            string soFolder = runtimeFolder + "/Scriptable_Objects";

            if (!AssetDatabase.IsValidFolder(soFolder))
            {
                AssetDatabase.CreateFolder(runtimeFolder, "Scriptable_Objects");
            }

            string assetPath = Path.Combine(soFolder, "ComponentChangesStore.asset");
            return assetPath.Replace("\\", "/");
        }

        public void Clear()
        {
            changes.Clear();
            EditorUtility.SetDirty(this);
        }
    }
}
