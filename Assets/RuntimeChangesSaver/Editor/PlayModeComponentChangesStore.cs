using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;


public class PlayModeComponentChangesStore : ScriptableObject
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

        // Flag und Daten für die ursprünglichen (Baseline-)Werte der Komponente
        public bool hasOriginalValues;
        public List<string> originalSerializedValues = new List<string>();
        public List<string> originalValueTypes = new List<string>();
    }

    public List<ComponentChange> changes = new List<ComponentChange>();

    public static PlayModeComponentChangesStore LoadExisting()
    {
        string[] guids = AssetDatabase.FindAssets("t:PlayModeComponentChangesStore");
        if (guids != null && guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<PlayModeComponentChangesStore>(path);
        }

        return null;
    }

    public static PlayModeComponentChangesStore LoadOrCreate()
    {
        var store = LoadExisting();
        if (store == null)
        {
            string assetPath = GetDefaultAssetPath();
            store = CreateInstance<PlayModeComponentChangesStore>();
            AssetDatabase.CreateAsset(store, assetPath);
            AssetDatabase.SaveAssets();
        }

        return store;
    }

    private static string GetRuntimeChangesSaverRootFolder()
    {
        // Versuche, den Speicherort dieses Skripts zu finden und von dort
        // zum Ordner "RuntimeChangesSaver" hochzulaufen, egal wo er unterhalb
        // von Assets einsortiert ist.
        string[] scriptGuids = AssetDatabase.FindAssets("PlayModeComponentChangesStore t:Script");
        if (scriptGuids != null && scriptGuids.Length > 0)
        {
            string scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuids[0]);
            if (!string.IsNullOrEmpty(scriptPath))
            {
                string dir = Path.GetDirectoryName(scriptPath).Replace("\\", "/");

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
                return Path.GetDirectoryName(scriptPath).Replace("\\", "/");
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

        string assetPath = Path.Combine(soFolder, "PlayModeComponentChangesStore.asset");
        return assetPath.Replace("\\", "/");
    }

    public void Clear()
    {
        changes.Clear();
        EditorUtility.SetDirty(this);
    }
}
