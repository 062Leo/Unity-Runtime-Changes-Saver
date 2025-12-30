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

            // flag and data for original baseline values of component
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
            // locate script file on disk
            // walk up directory hierarchy to folder "RuntimeChangesSaver" regardless of position under Assets
            string[] scriptGuids = AssetDatabase.FindAssets($"{nameof(ComponentChangesStore)} t:Script");
            if (scriptGuids is { Length: > 0 })
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuids[0]);
                if (!string.IsNullOrEmpty(scriptPath))
                {
                    string dir = Path.GetDirectoryName(scriptPath)?.Replace("\\", "/");

                    // walk upward from script until folder named "RuntimeChangesSaver" found or Assets root reached
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

                    // fallback when no explicit RuntimeChangesSaver folder found
                    // use folder where this script resides as root
                    return Path.GetDirectoryName(scriptPath)?.Replace("\\", "/");
                }
            }

            // Ultimativer Fallback: Assets als Root verwenden.
            return "Assets";
        }

        private static string GetDefaultAssetPath()
        {
            // always store asset inside actual RuntimeChangesSaver folder
            // avoid hardcoding folder position under Assets
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
