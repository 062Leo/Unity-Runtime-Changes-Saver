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
            public string globalObjectId;
            public string componentType;
            public int componentIndex;

            public List<string> propertyPaths = new List<string>();
            public List<string> serializedValues = new List<string>();
            public List<string> valueTypes = new List<string>();
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
            // locate script asset, walk up to RuntimeChangesSaver under Assets
            string[] scriptGuids = AssetDatabase.FindAssets($"{nameof(ComponentChangesStore)} t:Script");
            if (scriptGuids is { Length: > 0 })
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuids[0]);
                if (!string.IsNullOrEmpty(scriptPath))
                {
                    string dir = Path.GetDirectoryName(scriptPath)?.Replace("\\", "/");

                    // walk up until RuntimeChangesSaver or Assets root
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

                    // fallback: no RuntimeChangesSaver, use script folder as root
                    return Path.GetDirectoryName(scriptPath)?.Replace("\\", "/");
                }
            }

            // fallback: Assets root
            return "Assets";
        }

        private static string GetDefaultAssetPath()
        {
            // asset inside RuntimeChangesSaver/Scriptable_Objects, avoid hardcoded Assets path
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
