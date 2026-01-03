using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RuntimeChangesSaver.Editor.ChangesTracker
{
    public static class SnapshotManager
    {
        private static readonly Dictionary<string, TransformSnapshot> snapshots = new();
        private static readonly Dictionary<string, Dictionary<string, ComponentSnapshot>> componentSnapshots = new();

        public static void CaptureSnapshotsInPlayMode()
        {
            snapshots.Clear();
            componentSnapshots.Clear();

            int sceneCount = SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                GameObject[] roots = scene.GetRootGameObjects();
                foreach (GameObject rootGO in roots)
                {
                    CaptureGameObjectRecursive(rootGO);
                }
            }
        }

        public static void CaptureSnapshotsInEditMode()
        {
            snapshots.Clear();
            componentSnapshots.Clear();

            int sceneCount = SceneManager.sceneCount;
            if (sceneCount == 0)
                return;

            for (int i = 0; i < sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                GameObject[] roots = scene.GetRootGameObjects();
                foreach (GameObject rootGO in roots)
                {
                    CaptureGameObjectRecursive(rootGO);
                }
            }
        }

        public static void ResetTransformBaseline(GameObject go)
        {
            if (go == null) return;
            string key = GetGoKey(go);
            snapshots[key] = new TransformSnapshot(go);
        }

        public static void ResetComponentBaseline(Component comp)
        {
            if (comp == null) return;

            GameObject go = comp.gameObject;
            string goKey = GetGoKey(go);

            if (!componentSnapshots.TryGetValue(goKey, out var dict))
            {
                dict = new Dictionary<string, ComponentSnapshot>();
                componentSnapshots[goKey] = dict;
            }

            string compKey = SceneAndPathUtilities.GetComponentKey(comp);
            dict[compKey] = CaptureComponentSnapshot(comp);
        }

        public static TransformSnapshot GetSnapshot(GameObject go)
        {
            if (go == null)
                return null;

            string key = GetGoKey(go);
            bool found = snapshots.TryGetValue(key, out var snap);
            return found ? snap : null;
        }

        public static void SetSnapshot(GameObject go, TransformSnapshot snapshot)
        {
            if (snapshot == null) return;
            string key = GetGoKey(go);
            snapshots[key] = snapshot;
        }

        public static List<Component> GetChangedComponents(GameObject go)
        {
            if (go == null) return new List<Component>();

            string key = GetGoKey(go);

            if (!componentSnapshots.ContainsKey(key))
            {
                Debug.LogWarning($"[RCS][Snapshot] No component baselines for GUID key '{key}' (name='{go.name}')");
                return new List<Component>();
            }

            var changed = new List<Component>();
            var compSnapshots = componentSnapshots[key];

            if (snapshots.TryGetValue(key, out var originalTransform))
            {
                TransformSnapshot currentTransform = new TransformSnapshot(go);
                var transformChanges = GetChangedProperties(originalTransform, currentTransform);

                if (transformChanges.Count > 0)
                    changed.Add(go.transform);
            }
            else
            {
                Debug.LogWarning($"[RCS][Snapshot] Missing transform baseline for GUID key '{key}' (name='{go.name}')");
            }

            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp is null or Transform) continue;

                string compKey = SceneAndPathUtilities.GetComponentKey(comp);

                if (!compSnapshots.TryGetValue(compKey, out var snapshot))
                {
                    Debug.LogWarning($"[RCS][Snapshot] Missing component baseline for key '{compKey}' on '{go.name}'");
                    continue;
                }

                bool hasChanged = HasComponentChanged(comp, snapshot);

                if (hasChanged)
                    changed.Add(comp);
            }

            return changed;
        }

        private static int CaptureGameObjectRecursive(GameObject go)
        {
            int count = 1;

            string key = GetGoKey(go);
            snapshots[key] = new TransformSnapshot(go);

            var compDict = new Dictionary<string, ComponentSnapshot>();
            Component[] components = go.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp is null or Transform)
                    continue;

                string compKey = SceneAndPathUtilities.GetComponentKey(comp);
                ComponentSnapshot compSnapshot = CaptureComponentSnapshot(comp);
                compDict[compKey] = compSnapshot;
            }

            componentSnapshots[key] = compDict;

            foreach (Transform child in go.transform)
            {
                count += CaptureGameObjectRecursive(child.gameObject);
            }

            return count;
        }

        private static ComponentSnapshot CaptureComponentSnapshot(Component comp)
        {
            var snapshot = new ComponentSnapshot
            {
                componentType = comp.GetType().AssemblyQualifiedName,
                globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(comp.gameObject).ToString()
            };

            SerializedObject so = new SerializedObject(comp);
            SerializedProperty prop = so.GetIterator();

            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (prop.name == "m_Script") continue;

                try
                {
                    object value = Serialization.ComponentPropertySerializer.GetPropertyValue(prop);
                    if (value != null)
                        snapshot.properties[prop.propertyPath] = value;
                }
                catch
                {
                }
            }

            return snapshot;
        }

        private static bool HasComponentChanged(Component comp, ComponentSnapshot snapshot)
        {
            SerializedObject so = new SerializedObject(comp);
            SerializedProperty prop = so.GetIterator();

            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (prop.name == "m_Script") continue;

                if (!snapshot.properties.TryGetValue(prop.propertyPath, out var originalValue))
                    continue;

                try
                {
                    object currentValue = Serialization.ComponentPropertySerializer.GetPropertyValue(prop);

                    if (currentValue == null && originalValue == null) continue;
                    if (currentValue == null || originalValue == null) return true;

                    if (currentValue is Vector2 v2Current && originalValue is Vector2 v2Original)
                    {
                        if (Vector2.Distance(v2Current, v2Original) > 0.0001f) return true;
                    }
                    else if (currentValue is Vector3 v3Current && originalValue is Vector3 v3Original)
                    {
                        if (Vector3.Distance(v3Current, v3Original) > 0.0001f) return true;
                    }
                    else if (currentValue is Quaternion qCurrent && originalValue is Quaternion qOriginal)
                    {
                        if (Quaternion.Angle(qCurrent, qOriginal) > 0.0001f) return true;
                    }
                    else if (currentValue is float fCurrent && originalValue is float fOriginal)
                    {
                        if (Mathf.Abs(fCurrent - fOriginal) > 0.0001f) return true;
                    }
                    else
                    {
                        if (!currentValue.Equals(originalValue)) return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        public static List<string> GetChangedProperties(TransformSnapshot original, TransformSnapshot current)
        {
            List<string> changed = new List<string>();

            if (original.position != current.position) changed.Add("position");
            if (original.rotation != current.rotation) changed.Add("rotation");
            if (original.scale != current.scale) changed.Add("scale");

            if (original.isRectTransform)
            {
                if (original.anchoredPosition != current.anchoredPosition) changed.Add("anchoredPosition");
                if (original.anchoredPosition3D != current.anchoredPosition3D) changed.Add("anchoredPosition3D");
                if (original.anchorMin != current.anchorMin) changed.Add("anchorMin");
                if (original.anchorMax != current.anchorMax) changed.Add("anchorMax");
                if (original.pivot != current.pivot) changed.Add("pivot");
                if (original.sizeDelta != current.sizeDelta) changed.Add("sizeDelta");
                if (original.offsetMin != current.offsetMin) changed.Add("offsetMin");
                if (original.offsetMax != current.offsetMax) changed.Add("offsetMax");
            }

            return changed;
        }

        public static ComponentSnapshot GetComponentSnapshot(GameObject go, string componentKey)
        {
            string goKey = GetGoKey(go);
            if (!componentSnapshots.TryGetValue(goKey, out var componentSnapshot))
                return null;

            return componentSnapshot.GetValueOrDefault(componentKey);
        }

        private static string GetGoKey(GameObject go)
        {
            if (go == null) return string.Empty;
            string guid = GlobalObjectId.GetGlobalObjectIdSlow(go).ToString();
            if (!string.IsNullOrEmpty(guid))
                return guid;
            return SceneAndPathUtilities.GetGameObjectKey(go);
        }
    }
}
