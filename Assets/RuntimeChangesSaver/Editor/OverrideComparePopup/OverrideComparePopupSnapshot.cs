﻿using UnityEditor;
using UnityEngine;
using RuntimeChangesSaver.Editor.ChangesTracker;

namespace RuntimeChangesSaver.Editor.OverrideComparePopup
{
    /// <summary>
    /// Handles snapshot creation and editor initialization for comparison.
    /// </summary>
    internal class OverrideComparePopupSnapshot
    {
        public GameObject SnapshotGO { get; private set; }
        public Component SnapshotComponent { get; private set; }

        private Component liveComponent;

        public OverrideComparePopupSnapshot(Component component)
        {
            liveComponent = component;
            CreateSnapshot();
        }

        private void CreateSnapshot()
        {
            var go = liveComponent.gameObject;
            SnapshotGO = new GameObject("SnapshotTransform")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            if (liveComponent is Transform)
            {
                CreateTransformSnapshot(go);
            }
            else
            {
                CreateComponentSnapshot(go);
            }
        }

        private void CreateTransformSnapshot(GameObject go)
        {
            TransformOriginalStore.TransformOriginal originalMatch = null;
            TransformChangesStore.TransformChange changeMatch = null;

            string scenePath = go.scene.path;
            if (string.IsNullOrEmpty(scenePath))
                scenePath = go.scene.name;

            string objectPath = OverrideComparePopupUtilities.GetGameObjectPath(go.transform);

            var originalStore = TransformOriginalStore.LoadExisting();
            if (originalStore != null)
            {
                originalMatch = originalStore.entries.Find(e => e.scenePath == scenePath && e.objectPath == objectPath);
            }

            var changeStore = TransformChangesStore.LoadExisting();
            if (changeStore != null)
            {
                changeMatch = changeStore.changes.Find(c => c.scenePath == scenePath && c.objectPath == objectPath);
            }

            if (originalMatch != null)
            {
                CreateTransformSnapshotFromOriginal(originalMatch);
            }
            else if (changeMatch != null)
            {
                CreateTransformSnapshotFromChange(changeMatch);
            }
            else if (Application.isPlaying)
            {
                CreateTransformSnapshotFromLiveSnapshot(go);
            }
        }

        private void CreateTransformSnapshotFromOriginal(TransformOriginalStore.TransformOriginal original)
        {
            if (original.isRectTransform && liveComponent is RectTransform)
            {
                SnapshotComponent = SnapshotGO.AddComponent<RectTransform>();
            }
            else
            {
                SnapshotComponent = SnapshotGO.transform;
            }

            if (SnapshotComponent is RectTransform snapshotRT)
            {
                snapshotRT.anchoredPosition = original.anchoredPosition;
                snapshotRT.anchoredPosition3D = original.anchoredPosition3D;
                snapshotRT.anchorMin = original.anchorMin;
                snapshotRT.anchorMax = original.anchorMax;
                snapshotRT.pivot = original.pivot;
                snapshotRT.sizeDelta = original.sizeDelta;
                snapshotRT.offsetMin = original.offsetMin;
                snapshotRT.offsetMax = original.offsetMax;
            }

            SnapshotComponent.transform.localPosition = original.position;
            SnapshotComponent.transform.localRotation = original.rotation;
            SnapshotComponent.transform.localScale = original.scale;

            SerializedObject so = new SerializedObject(SnapshotComponent);
            so.Update();
        }

        private void CreateTransformSnapshotFromChange(TransformChangesStore.TransformChange change)
        {
            if (change.isRectTransform && liveComponent is RectTransform)
            {
                SnapshotComponent = SnapshotGO.AddComponent<RectTransform>();
            }
            else
            {
                SnapshotComponent = SnapshotGO.transform;
            }

            if (SnapshotComponent is RectTransform snapshotRT)
            {
                snapshotRT.anchoredPosition = change.anchoredPosition;
                snapshotRT.anchoredPosition3D = change.anchoredPosition3D;
                snapshotRT.anchorMin = change.anchorMin;
                snapshotRT.anchorMax = change.anchorMax;
                snapshotRT.pivot = change.pivot;
                snapshotRT.sizeDelta = change.sizeDelta;
                snapshotRT.offsetMin = change.offsetMin;
                snapshotRT.offsetMax = change.offsetMax;
            }

            SnapshotComponent.transform.localPosition = change.position;
            SnapshotComponent.transform.localRotation = change.rotation;
            SnapshotComponent.transform.localScale = change.scale;

            SerializedObject so = new SerializedObject(SnapshotComponent);
            so.Update();
        }

        private void CreateTransformSnapshotFromLiveSnapshot(GameObject go)
        {
            var originalSnapshot = ChangesTrackerCore.GetSnapshot(go);

            if (originalSnapshot != null)
            {
                if (originalSnapshot.isRectTransform && liveComponent is RectTransform)
                {
                    SnapshotComponent = SnapshotGO.AddComponent<RectTransform>();
                }
                else
                {
                    SnapshotComponent = SnapshotGO.transform;
                }

                if (SnapshotComponent is RectTransform snapshotRT)
                {
                    snapshotRT.anchoredPosition = originalSnapshot.anchoredPosition;
                    snapshotRT.anchoredPosition3D = originalSnapshot.anchoredPosition3D;
                    snapshotRT.anchorMin = originalSnapshot.anchorMin;
                    snapshotRT.anchorMax = originalSnapshot.anchorMax;
                    snapshotRT.pivot = originalSnapshot.pivot;
                    snapshotRT.sizeDelta = originalSnapshot.sizeDelta;
                    snapshotRT.offsetMin = originalSnapshot.offsetMin;
                    snapshotRT.offsetMax = originalSnapshot.offsetMax;
                }

                SnapshotComponent.transform.localPosition = originalSnapshot.position;
                SnapshotComponent.transform.localRotation = originalSnapshot.rotation;
                SnapshotComponent.transform.localScale = originalSnapshot.scale;

                SerializedObject so = new SerializedObject(SnapshotComponent);
                so.Update();
            }
        }

        private void CreateComponentSnapshot(GameObject go)
        {
            var type = liveComponent.GetType();
            SnapshotComponent = SnapshotGO.AddComponent(type);

            bool appliedFromStore = TryApplyComponentSnapshotFromStores(go, type);
            if (!appliedFromStore)
            {
                CreateComponentSnapshotFromLiveSnapshot(go, type);
            }
        }

        private void CreateComponentSnapshotFromLiveSnapshot(GameObject go, System.Type type)
        {
            string compKey = ChangesTrackerCore.GetComponentKey(liveComponent);
            var snapshot = ChangesTrackerCore.GetComponentSnapshot(go, compKey);

            if (snapshot != null)
            {
                SerializedObject so = new SerializedObject(SnapshotComponent);

                foreach (var kvp in snapshot.properties)
                {
                    SerializedProperty prop = so.FindProperty(kvp.Key);
                    if (prop != null)
                    {
                        try
                        {
                            OverrideComparePopupSerialization.SetPropertyValue(prop, kvp.Value);
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private bool TryApplyComponentSnapshotFromStores(GameObject go, System.Type type)
        {
            string scenePath = go.scene.path;
            if (string.IsNullOrEmpty(scenePath))
                scenePath = go.scene.name;

            string objectPath = OverrideComparePopupUtilities.GetGameObjectPath(go.transform);
            string componentType = type.AssemblyQualifiedName;
            var allOfType = go.GetComponents(type);
            int index = System.Array.IndexOf(allOfType, liveComponent);

            var originalStore = ComponentOriginalStore.LoadExisting();
            var originalMatch = originalStore?.entries.Find(e =>
                e.scenePath == scenePath &&
                e.objectPath == objectPath &&
                e.componentType == componentType &&
                e.componentIndex == index);

            if (originalMatch != null)
            {
                ApplyComponentOriginalToSnapshot(originalMatch);
                return true;
            }

            var changeStore = ComponentChangesStore.LoadExisting();
            var changeMatch = changeStore?.changes.Find(c =>
                c.scenePath == scenePath &&
                c.objectPath == objectPath &&
                c.componentType == componentType &&
                c.componentIndex == index);

            if (changeMatch != null)
            {
                ApplyComponentChangeToSnapshot(changeMatch);
                return true;
            }

            return false;
        }

        private void ApplyComponentChangeToSnapshot(ComponentChangesStore.ComponentChange match)
        {
            SerializedObject so = new SerializedObject(SnapshotComponent);

            for (int i = 0; i < match.propertyPaths.Count; i++)
            {
                string path = match.propertyPaths[i];
                SerializedProperty prop = so.FindProperty(path);
                if (prop == null)
                    continue;

                string typeName = (i < match.valueTypes.Count) ? match.valueTypes[i] : string.Empty;
                string value = (i < match.serializedValues.Count) ? match.serializedValues[i] : string.Empty;

                try
                {
                    OverrideComparePopupSerialization.ApplySerializedComponentValue(prop, typeName, value);
                }
                catch
                {
                    // ignored
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private void ApplyComponentOriginalToSnapshot(ComponentOriginalStore.ComponentOriginal match)
        {
            SerializedObject so = new SerializedObject(SnapshotComponent);

            for (int i = 0; i < match.propertyPaths.Count; i++)
            {
                string path = match.propertyPaths[i];
                SerializedProperty prop = so.FindProperty(path);
                if (prop == null)
                    continue;

                string typeName = (i < match.valueTypes.Count) ? match.valueTypes[i] : string.Empty;
                string value = (i < match.serializedValues.Count) ? match.serializedValues[i] : string.Empty;

                try
                {
                    OverrideComparePopupSerialization.ApplySerializedComponentValue(prop, typeName, value);
                }
                catch
                {
                    // ignored
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public void Cleanup()
        {
            if (SnapshotGO)
                Object.DestroyImmediate(SnapshotGO);
        }
    }
}

