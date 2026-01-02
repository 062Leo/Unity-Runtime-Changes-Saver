using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

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
