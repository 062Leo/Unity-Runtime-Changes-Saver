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

            public bool hasOriginalValues;

            public Vector3 originalPosition;
            public Quaternion originalRotation;
            public Vector3 originalScale;

            public Vector2 originalAnchoredPosition;
            public Vector3 originalAnchoredPosition3D;
            public Vector2 originalAnchorMin;
            public Vector2 originalAnchorMax;
            public Vector2 originalPivot;
            public Vector2 originalSizeDelta;
            public Vector2 originalOffsetMin;
            public Vector2 originalOffsetMax;
        }

        public List<TransformChange> changes = new List<TransformChange>();

        public static TransformChangesStore LoadExisting()
        {
            string[] guids = AssetDatabase.FindAssets("t:TransformChangesStore");
            if (guids is { Length: > 0 })
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var store = AssetDatabase.LoadAssetAtPath<TransformChangesStore>(path);
                Debug.Log($"[TransformDebug][Store.LoadExisting] Found existing store at '{path}', changeCount={store.changes.Count}");
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
            else
            {
                string existingPath = AssetDatabase.GetAssetPath(store);
                Debug.Log($"[TransformDebug][Store.LoadOrCreate] Using existing store at '{existingPath}', changeCount={store.changes.Count}");
            }
            return store;
        }

        private static string GetRuntimeChangesSaverRootFolder()
        {
            // Versuche, den Speicherort dieses Skripts zu finden und von dort
            // zum Ordner "RuntimeChangesSaver" hochzulaufen, egal wo er unterhalb
            // von Assets einsortiert ist.
            string[] scriptGuids = AssetDatabase.FindAssets("TransformChangesStore t:Script");

            if (scriptGuids is { Length: > 0 })
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuids[0]);
                if (!string.IsNullOrEmpty(scriptPath))
                {
                    string dir = Path.GetDirectoryName(scriptPath)?.Replace("\\", "/");

                    // Vom Skript nach oben laufen, bis wir einen Ordner namens
                    // "RuntimeChangesSaver" finden oder Assets erreichen.
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

                    // Fallback: wenn kein expliziter RuntimeChangesSaver-Ordner
                    // gefunden wurde, verwenden wir den Ordner, in dem das Skript liegt.
                    return Path.GetDirectoryName(scriptPath)?.Replace("\\", "/");
                }
            }

            // Ultimativer Fallback: Assets als Root verwenden.
            return "Assets";
        }

        private static string GetDefaultAssetPath()
        {
            // Immer innerhalb des tatsächlichen RuntimeChangesSaver-Ordners speichern,
            // aber dessen Position unterhalb von Assets NICHT hardcoden.
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
            Debug.Log("[TransformDebug][Store.Clear] Cleared all stored transform changes");
        }
    }
}