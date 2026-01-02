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
            TransformChangesStore.TransformChange storeMatch = null;
            var store = TransformChangesStore.LoadExisting();
            if (store != null)
            {
                string scenePath = go.scene.path;
                if (string.IsNullOrEmpty(scenePath))
                    scenePath = go.scene.name;

                string objectPath = OverrideComparePopupUtilities.GetGameObjectPath(go.transform);

                foreach (var c in store.changes)
                {
                    if (c.scenePath == scenePath && c.objectPath == objectPath)
                    {
                        storeMatch = c;
                        break;
                    }
                }
            }

            if (storeMatch != null)
            {
                CreateTransformSnapshotFromStore(storeMatch);
            }
            else if (Application.isPlaying)
            {
                CreateTransformSnapshotFromLiveSnapshot(go);
            }
        }

        private void CreateTransformSnapshotFromStore(TransformChangesStore.TransformChange change)
        {
            bool useOriginal = change.hasOriginalValues;

            Vector3 basePos = useOriginal ? change.originalPosition : change.position;
            Quaternion baseRot = useOriginal ? change.originalRotation : change.rotation;
            Vector3 baseScale = useOriginal ? change.originalScale : change.scale;

            Vector2 baseAnchoredPos = useOriginal ? change.originalAnchoredPosition : change.anchoredPosition;
            Vector3 baseAnchoredPos3D = useOriginal ? change.originalAnchoredPosition3D : change.anchoredPosition3D;
            Vector2 baseAnchorMin = useOriginal ? change.originalAnchorMin : change.anchorMin;
            Vector2 baseAnchorMax = useOriginal ? change.originalAnchorMax : change.anchorMax;
            Vector2 basePivot = useOriginal ? change.originalPivot : change.pivot;
            Vector2 baseSizeDelta = useOriginal ? change.originalSizeDelta : change.sizeDelta;
            Vector2 baseOffsetMin = useOriginal ? change.originalOffsetMin : change.offsetMin;
            Vector2 baseOffsetMax = useOriginal ? change.originalOffsetMax : change.offsetMax;

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
                snapshotRT.anchoredPosition = baseAnchoredPos;
                snapshotRT.anchoredPosition3D = baseAnchoredPos3D;
                snapshotRT.anchorMin = baseAnchorMin;
                snapshotRT.anchorMax = baseAnchorMax;
                snapshotRT.pivot = basePivot;
                snapshotRT.sizeDelta = baseSizeDelta;
                snapshotRT.offsetMin = baseOffsetMin;
                snapshotRT.offsetMax = baseOffsetMax;
            }

            SnapshotComponent.transform.localPosition = basePos;
            SnapshotComponent.transform.localRotation = baseRot;
            SnapshotComponent.transform.localScale = baseScale;

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

            if (Application.isPlaying)
            {
                CreateComponentSnapshotFromStore(go, type);
            }
            else
            {
                CreateComponentSnapshotFromEditMode(go, type);
            }
        }

        private void CreateComponentSnapshotFromStore(GameObject go, System.Type type)
        {
            ComponentChangesStore.ComponentChange match = null;
            var compStore = ComponentChangesStore.LoadExisting();

            if (compStore != null)
            {
                string scenePath = go.scene.path;
                if (string.IsNullOrEmpty(scenePath))
                    scenePath = go.scene.name;

                string objectPath = OverrideComparePopupUtilities.GetGameObjectPath(go.transform);
                string componentType = type.AssemblyQualifiedName;
                var allOfType = go.GetComponents(type);
                int index = System.Array.IndexOf(allOfType, liveComponent);

                foreach (var c in compStore.changes)
                {
                    if (c.scenePath == scenePath &&
                        c.objectPath == objectPath &&
                        c.componentType == componentType &&
                        c.componentIndex == index)
                    {
                        match = c;
                        break;
                    }
                }
            }

            if (match != null)
            {
                ApplyComponentChangeToSnapshot(match);
            }
            else
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

        private void CreateComponentSnapshotFromEditMode(GameObject go, System.Type type)
        {
            var compStore = ComponentChangesStore.LoadExisting();
            ComponentChangesStore.ComponentChange match = null;

            if (compStore != null)
            {
                string scenePath = go.scene.path;
                if (string.IsNullOrEmpty(scenePath))
                    scenePath = go.scene.name;

                string objectPath = OverrideComparePopupUtilities.GetGameObjectPath(go.transform);
                string componentType = type.AssemblyQualifiedName;
                var allOfType = go.GetComponents(type);
                int index = System.Array.IndexOf(allOfType, liveComponent);

                foreach (var c in compStore.changes)
                {
                    if (c.scenePath == scenePath &&
                        c.objectPath == objectPath &&
                        c.componentType == componentType &&
                        c.componentIndex == index)
                    {
                        match = c;
                        break;
                    }
                }
            }

            if (match != null)
            {
                ApplyComponentChangeToSnapshot(match);
            }
        }

        private void ApplyComponentChangeToSnapshot(ComponentChangesStore.ComponentChange match)
        {
            SerializedObject so = new SerializedObject(SnapshotComponent);

            var baseValues = (match is { hasOriginalValues: true, originalSerializedValues: not null } &&
                              match.originalSerializedValues.Count == match.propertyPaths.Count)
                ? match.originalSerializedValues
                : match.serializedValues;

            var baseTypes = (match is { hasOriginalValues: true, originalValueTypes: not null } &&
                             match.originalValueTypes.Count == match.propertyPaths.Count)
                ? match.originalValueTypes
                : match.valueTypes;

            for (int i = 0; i < match.propertyPaths.Count; i++)
            {
                string path = match.propertyPaths[i];
                SerializedProperty prop = so.FindProperty(path);
                if (prop == null)
                    continue;

                string typeName = (i < baseTypes.Count) ? baseTypes[i] : string.Empty;
                string value = (i < baseValues.Count) ? baseValues[i] : string.Empty;

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

