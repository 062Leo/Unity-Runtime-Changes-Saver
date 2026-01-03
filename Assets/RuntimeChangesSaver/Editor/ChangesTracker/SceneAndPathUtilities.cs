using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace RuntimeChangesSaver.Editor.ChangesTracker
{
    public static class SceneAndPathUtilities
    {
        public static string NormalizeScenePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            return path.Replace('\\', '/').Trim();
        }

        public static Scene GetSceneByPathOrName(string scenePath)
        {
            var scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid())
            {
                scene = SceneManager.GetSceneByName(scenePath);
            }
            return scene;
        }

        /// <summary>
        /// Hybrid lookup: attempts GUID-based lookup first, falls back to path-based lookup.
        /// Logs warning if fallback is used.
        /// </summary>
        public static GameObject FindGameObjectByGuidOrPath(Scene scene, string globalObjectIdStr, string objectPath)
        {
            // Attempt GUID lookup first (primary method)
            if (!string.IsNullOrEmpty(globalObjectIdStr))
            {
                GameObject guidResult = FindGameObjectByGuid(globalObjectIdStr);
                if (guidResult != null)
                {
                    Debug.Log($"[RCS][Lookup] GUID hit for '{globalObjectIdStr}' -> {guidResult.scene.path}/{GetGameObjectPath(guidResult.transform)}");
                    return guidResult;
                }
                Debug.LogWarning($"[RCS][Lookup] GUID miss for '{globalObjectIdStr}', falling back to path '{objectPath}'");
            }

            // Fallback to path-based lookup
            GameObject pathResult = FindInSceneByPath(scene, objectPath);
            if (pathResult != null && !string.IsNullOrEmpty(globalObjectIdStr))
            {
                Debug.LogWarning($"[RCS][Lookup] Fallback succeeded via path '{objectPath}'");
            }

            return pathResult;
        }

        /// <summary>
        /// Attempts to find a GameObject by its GlobalObjectId string.
        /// Returns null if GUID is invalid or object not found.
        /// </summary>
        public static GameObject FindGameObjectByGuid(string globalObjectIdStr)
        {
            if (string.IsNullOrEmpty(globalObjectIdStr))
                return null;

            if (!GlobalObjectId.TryParse(globalObjectIdStr, out GlobalObjectId globalObjectId))
            {
                Debug.LogWarning($"[RCS][Lookup] Failed to parse GlobalObjectId '{globalObjectIdStr}'");
                return null;
            }

            Object obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId);
            if (obj is GameObject go)
            {
                Debug.Log($"[RCS][Lookup] GUID resolved to GameObject '{go.name}' in scene '{go.scene.path}'");
                return go;
            }

            if (obj is Component comp)
            {
                Debug.Log($"[RCS][Lookup] GUID resolved to Component '{comp.GetType().Name}' on GameObject '{comp.gameObject.name}'");
                return comp.gameObject;
            }

            return null;
        }

        public static GameObject FindInSceneByPath(Scene scene, string path)
        {
            if (!scene.IsValid())
                return null;

            var parts = path.Split('/');
            if (parts.Length == 0)
                return null;

            GameObject current = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == parts[0])
                {
                    current = root;
                    break;
                }
            }

            if (current == null)
                return null;

            for (int i = 1; i < parts.Length; i++)
            {
                var childName = parts[i];
                Transform child = null;
                foreach (Transform t in current.transform)
                {
                    if (t.name == childName)
                    {
                        child = t;
                        break;
                    }
                }

                if (child == null)
                    return null;

                current = child.gameObject;
            }

            return current;
        }

        public static string GetGameObjectPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }

        public static string GetGameObjectKey(GameObject go)
        {
            if (go == null) return "";

            string scenePath = go.scene.path;
            if (string.IsNullOrEmpty(scenePath))
                scenePath = go.scene.name;

            string goPath = GetGameObjectPath(go.transform);
            return $"{scenePath}|{goPath}";
        }

        public static string GetComponentKey(Component comp)
        {
            var allComps = comp.gameObject.GetComponents(comp.GetType());
            int index = System.Array.IndexOf(allComps, comp);
            return $"{comp.GetType().Name}_{index}";
        }
    }
}
