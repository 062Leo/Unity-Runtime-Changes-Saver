﻿using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RuntimeChangesSaver.Editor.ChangesTracker;
using RuntimeChangesSaver.Editor.OverrideComparePopup;

namespace RuntimeChangesSaver.Editor
{
    internal class OverridesWindow : PopupWindowContent
    {
        private readonly GameObject targetGO;
        private readonly List<Component> changedComponents;
        private Vector2 scroll;
        private const float RowHeight = 22f;
        private const float HeaderHeight = 28f;
        private const float FooterHeight = 50f;

        public OverridesWindow(GameObject go)
        {
            targetGO = go;
            changedComponents = ChangesTrackerCore.GetChangedComponents(go);
        }

        public override Vector2 GetWindowSize()
        {
            int count = Mathf.Max(1, changedComponents.Count);
            float listHeight = count * RowHeight;
            float totalHeight = HeaderHeight + listHeight + FooterHeight + 10;
            return new Vector2(320, Mathf.Min(500, totalHeight));
        }

        public override void OnGUI(Rect rect)
        {
            // Layout
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, HeaderHeight);
            DrawHeader(headerRect);

            if (changedComponents.Count == 0)
            {
                Rect helpRect = new Rect(rect.x + 10, rect.y + HeaderHeight, rect.width - 20, 40);
                GUI.Label(helpRect, "No changed components", EditorStyles.helpBox);
                return;
            }

            // List
            float listHeight = rect.height - HeaderHeight - FooterHeight;
            Rect listRect = new Rect(rect.x, rect.y + HeaderHeight, rect.width, listHeight);
            DrawComponentList(listRect);

            // Footer
            Rect footerRect = new Rect(rect.x, rect.y + HeaderHeight + listHeight, rect.width, FooterHeight);
            DrawFooter(footerRect);
        }

        void DrawHeader(Rect rect)
        {
            EditorGUI.LabelField(
                new Rect(rect.x + 6, rect.y + 6, rect.width - 12, 20),
                "Play Mode Overrides",
                EditorStyles.boldLabel
            );
        }

        void DrawComponentList(Rect rect)
        {
            Rect viewRect = new Rect(0, 0, rect.width - 16, changedComponents.Count * RowHeight);
            scroll = GUI.BeginScrollView(rect, scroll, viewRect);

            for (int i = 0; i < changedComponents.Count; i++)
            {
                Rect row = new Rect(0, i * RowHeight, viewRect.width, RowHeight);
                DrawRow(row, changedComponents[i]);
            }

            GUI.EndScrollView();
        }

        void DrawRow(Rect rowRect, Component component)
        {
            if (Event.current.type == EventType.Repaint)
                EditorStyles.helpBox.Draw(rowRect, false, false, false, false);

            var content = EditorGUIUtility.ObjectContent(component, component.GetType());
            Rect labelRect = new Rect(rowRect.x + 6, rowRect.y + 3, rowRect.width - 12, 16);

            if (GUI.Button(labelRect, content, EditorStyles.label))
            {
                PopupWindow.Show(rowRect, new OverrideComparePopupContent(component, openedFromBrowser: false, onRefreshRequest: () => RefreshContent(targetGO)));
            }
        }

        void DrawFooter(Rect rect)
        {
            // Background
            if (Event.current.type == EventType.Repaint)
            {
                Color bgColor = EditorGUIUtility.isProSkin
                    ? new Color(0.22f, 0.22f, 0.22f, 0.8f)
                    : new Color(0.8f, 0.8f, 0.8f, 0.8f);
                EditorGUI.DrawRect(rect, bgColor);
            }

            // Buttons
            float buttonWidth = 100f;
            float buttonHeight = 28f;
            float spacing = 6f;
            float totalWidth = buttonWidth * 3 + spacing * 2;
            float startX = rect.x + (rect.width - totalWidth) / 2;
            float startY = rect.y + (rect.height - buttonHeight) / 2;

            Rect revertOriginalRect = new Rect(startX, startY, buttonWidth, buttonHeight);
            Rect revertSavedRect = new Rect(startX + buttonWidth + spacing, startY, buttonWidth, buttonHeight);
            Rect applyRect = new Rect(startX + (buttonWidth + spacing) * 2, startY, buttonWidth, buttonHeight);

            bool hasAnySaved = HasAnySavedEntries();

            if (GUI.Button(revertOriginalRect, "Revert to Original"))
            {
                RevertToOriginal();
                RefreshBrowserIfOpen();
                editorWindow.Close();
            }

            EditorGUI.BeginDisabledGroup(!hasAnySaved);
            if (GUI.Button(revertSavedRect, "Revert to Saved"))
            {
                RevertToSaved();
                RefreshBrowserIfOpen();
                editorWindow.Close();
            }
            EditorGUI.EndDisabledGroup();

            if (GUI.Button(applyRect, "Apply All"))
            {
                ApplyAllChanges();
                RefreshBrowserIfOpen();
                editorWindow.Close();
            }
        }

        void RevertToOriginal()
        {
            string scenePath = targetGO.scene.path;
            if (string.IsNullOrEmpty(scenePath))
                scenePath = targetGO.scene.name;

            string objectPath = OverrideComparePopupUtilities.GetGameObjectPath(targetGO.transform);

            var tStore = TransformChangesStore.LoadExisting();
            var cStore = ComponentChangesStore.LoadExisting();
            var originalTransformStore = TransformOriginalStore.LoadExisting();
            var originalComponentStore = ComponentOriginalStore.LoadExisting();

            bool transformListed = changedComponents.Contains(targetGO.transform);
            if (transformListed)
            {
                bool reverted = false;
                var originalTransform = originalTransformStore?.entries.Find(e => e.scenePath == scenePath && e.objectPath == objectPath);

                if (originalTransform != null)
                {
                    var transform = targetGO.transform;
                    transform.localPosition = originalTransform.position;
                    transform.localRotation = originalTransform.rotation;
                    transform.localScale = originalTransform.scale;

                    RectTransform rt = transform as RectTransform;
                    if (originalTransform.isRectTransform && rt != null)
                    {
                        rt.anchoredPosition = originalTransform.anchoredPosition;
                        rt.anchoredPosition3D = originalTransform.anchoredPosition3D;
                        rt.anchorMin = originalTransform.anchorMin;
                        rt.anchorMax = originalTransform.anchorMax;
                        rt.pivot = originalTransform.pivot;
                        rt.sizeDelta = originalTransform.sizeDelta;
                        rt.offsetMin = originalTransform.offsetMin;
                        rt.offsetMax = originalTransform.offsetMax;
                    }

                    reverted = true;
                }

                if (!reverted)
                {
                    var originalSnapshot = ChangesTrackerCore.GetSnapshot(targetGO);
                    if (originalSnapshot != null)
                    {
                        var transform = targetGO.transform;
                        var rt = transform as RectTransform;

                        transform.localPosition = originalSnapshot.position;
                        transform.localRotation = originalSnapshot.rotation;
                        transform.localScale = originalSnapshot.scale;

                        if (rt != null && originalSnapshot.isRectTransform)
                        {
                            rt.anchoredPosition = originalSnapshot.anchoredPosition;
                            rt.anchoredPosition3D = originalSnapshot.anchoredPosition3D;
                            rt.anchorMin = originalSnapshot.anchorMin;
                            rt.anchorMax = originalSnapshot.anchorMax;
                            rt.pivot = originalSnapshot.pivot;
                            rt.sizeDelta = originalSnapshot.sizeDelta;
                            rt.offsetMin = originalSnapshot.offsetMin;
                            rt.offsetMax = originalSnapshot.offsetMax;
                        }
                    }
                }
            }

            foreach (var comp in changedComponents)
            {
                if (comp is Transform) continue;

                bool reverted = false;
                var type = comp.GetType();
                string componentType = type.AssemblyQualifiedName;
                var allOfType = targetGO.GetComponents(type);
                int compIndex = System.Array.IndexOf(allOfType, comp);

                var originalEntry = originalComponentStore?.entries.Find(c =>
                    c.scenePath == scenePath &&
                    c.objectPath == objectPath &&
                    c.componentType == componentType &&
                    c.componentIndex == compIndex);

                if (originalEntry != null)
                {
                    var targetSO = new SerializedObject(comp);
                    for (int i = 0; i < originalEntry.propertyPaths.Count; i++)
                    {
                        string propPath = originalEntry.propertyPaths[i];
                        var prop = targetSO.FindProperty(propPath);
                        if (prop != null)
                        {
                            string typeName = (i < originalEntry.valueTypes.Count) ? originalEntry.valueTypes[i] : string.Empty;
                            string value = (i < originalEntry.serializedValues.Count) ? originalEntry.serializedValues[i] : string.Empty;
                            OverrideComparePopupSerialization.ApplySerializedComponentValue(prop, typeName, value);
                        }
                    }
                    targetSO.ApplyModifiedProperties();
                    reverted = true;
                }

                if (!reverted)
                {
                    string compKey = ChangesTrackerCore.GetComponentKey(comp);
                    var snapshot = ChangesTrackerCore.GetComponentSnapshot(targetGO, compKey);
                    if (snapshot != null)
                    {
                        RevertComponent(comp, snapshot);
                    }
                }
            }

            if (transformListed && tStore != null)
            {
                int removeIndex = tStore.changes.FindIndex(c => c.scenePath == scenePath && c.objectPath == objectPath);
                if (removeIndex >= 0)
                {
                    tStore.changes.RemoveAt(removeIndex);
                    EditorUtility.SetDirty(tStore);
                }
            }

            if (cStore != null)
            {
                foreach (var comp in changedComponents)
                {
                    if (comp is Transform or RectTransform) continue;

                    var type = comp.GetType();
                    string componentType = type.AssemblyQualifiedName;
                    var allOfType = targetGO.GetComponents(type);
                    int compIndex = System.Array.IndexOf(allOfType, comp);

                    int removeIndex = cStore.changes.FindIndex(c =>
                        c.scenePath == scenePath &&
                        c.objectPath == objectPath &&
                        c.componentType == componentType &&
                        c.componentIndex == compIndex);

                    if (removeIndex >= 0)
                    {
                        cStore.changes.RemoveAt(removeIndex);
                    }
                }

                EditorUtility.SetDirty(cStore);
            }

            AssetDatabase.SaveAssets();
        }

        void RevertToSaved()
        {
            // Revert to stored values in the Store (ComponentChangesStore, TransformChangesStore)
            string scenePath = targetGO.scene.path;
            if (string.IsNullOrEmpty(scenePath))
                scenePath = targetGO.scene.name;

            string objectPath = OverrideComparePopupUtilities.GetGameObjectPath(targetGO.transform);
            string targetGuid = GlobalObjectId.GetGlobalObjectIdSlow(targetGO).ToString();

            bool transformListed = changedComponents.Contains(targetGO.transform);

            // Revert Transform
            var tStore = TransformChangesStore.LoadExisting();
            if (transformListed && tStore != null)
            {
                int index = tStore.changes.FindIndex(c =>
                    (!string.IsNullOrEmpty(c.globalObjectId) && c.globalObjectId == targetGuid) ||
                    (string.IsNullOrEmpty(c.globalObjectId) && c.scenePath == scenePath && c.objectPath == objectPath));
                if (index >= 0)
                {
                    var storedChange = tStore.changes[index];
                    Transform t = targetGO.transform;
                    
                    t.localPosition = storedChange.position;
                    t.localRotation = storedChange.rotation;
                    t.localScale = storedChange.scale;

                    RectTransform rt = t as RectTransform;
                    if (storedChange.isRectTransform && rt != null)
                    {
                        rt.anchoredPosition = storedChange.anchoredPosition;
                        rt.anchoredPosition3D = storedChange.anchoredPosition3D;
                        rt.anchorMin = storedChange.anchorMin;
                        rt.anchorMax = storedChange.anchorMax;
                        rt.pivot = storedChange.pivot;
                        rt.sizeDelta = storedChange.sizeDelta;
                        rt.offsetMin = storedChange.offsetMin;
                        rt.offsetMax = storedChange.offsetMax;
                    }

                    // remove store entry after revert
                    tStore.changes.RemoveAt(index);
                    EditorUtility.SetDirty(tStore);
                }
            }

            // Revert other components
            var cStore = ComponentChangesStore.LoadExisting();
            if (cStore != null)
            {
                foreach (var comp in changedComponents)
                {
                    if (comp is Transform or RectTransform) continue;

                    var type = comp.GetType();
                    string componentType = type.AssemblyQualifiedName;
                    var allOfType = targetGO.GetComponents(type);
                    int compIndex = System.Array.IndexOf(allOfType, comp);

                    int index = cStore.changes.FindIndex(c =>
                        c.componentType == componentType &&
                        c.componentIndex == compIndex &&
                        (
                            (!string.IsNullOrEmpty(c.globalObjectId) && c.globalObjectId == targetGuid) ||
                            (string.IsNullOrEmpty(c.globalObjectId) && c.scenePath == scenePath && c.objectPath == objectPath)
                        ));

                    if (index >= 0)
                    {
                        var storedChange = cStore.changes[index];
                        var targetSO = new SerializedObject(comp);

                        for (int i = 0; i < storedChange.propertyPaths.Count; i++)
                        {
                            string propPath = storedChange.propertyPaths[i];
                            var prop = targetSO.FindProperty(propPath);
                            if (prop != null)
                            {
                                OverrideComparePopupSerialization.ApplySerializedComponentValue(prop, storedChange.valueTypes[i], storedChange.serializedValues[i]);
                            }
                        }

                        targetSO.ApplyModifiedProperties();

                        // remove store entry after revert
                        cStore.changes.RemoveAt(index);
                        EditorUtility.SetDirty(cStore);
                    }
                }
            }

            AssetDatabase.SaveAssets();
        }

        bool HasAnySavedEntries()
        {
            string scenePath = targetGO.scene.path;
            if (string.IsNullOrEmpty(scenePath))
                scenePath = targetGO.scene.name;

            string objectPath = OverrideComparePopupUtilities.GetGameObjectPath(targetGO.transform);

            bool transformListed = changedComponents.Contains(targetGO.transform);

            // Transform saved?
            var tStore = TransformChangesStore.LoadExisting();
            if (transformListed && tStore != null)
            {
                int index = tStore.changes.FindIndex(c => c.scenePath == scenePath && c.objectPath == objectPath);
                if (index >= 0)
                    return true;
            }

            // Any component saved?
            var cStore = ComponentChangesStore.LoadExisting();
            if (cStore != null)
            {
                foreach (var comp in changedComponents)
                {
                    if (comp is Transform or RectTransform) continue;

                    var type = comp.GetType();
                    string componentType = type.AssemblyQualifiedName;
                    var allOfType = targetGO.GetComponents(type);
                    int compIndex = System.Array.IndexOf(allOfType, comp);

                    int idx = cStore.changes.FindIndex(c =>
                        c.scenePath == scenePath &&
                        c.objectPath == objectPath &&
                        c.componentType == componentType &&
                        c.componentIndex == compIndex);

                    if (idx >= 0)
                        return true;
                }
            }

            return false;
        }

        void ApplyAllChanges()
        {
            // Accept changes
            bool hasTransformChange = false;
            foreach (var comp in changedComponents)
            {
                if (comp is Transform or RectTransform)
                {
                    hasTransformChange = true;
                    break;
                }
            }

            if (hasTransformChange)
            {
                ChangesTrackerCore.AcceptTransformChanges(targetGO);
            }

            // Non-transform acceptance

            foreach (var comp in changedComponents)
            {
                if (comp is null or Transform)
                    continue;

                ChangesTrackerCore.AcceptComponentChanges(comp);
            }

            //Debug.Log($"[TransformDebug][OverridesWindow.ApplyAll] Accepted all changes on GO='{targetGO.name}' (will be applied when exiting play mode)");
        }

        void RevertComponent(Component comp, ComponentSnapshot snapshot)
        {
            SerializedObject so = new SerializedObject(comp);

            foreach (var kvp in snapshot.properties)
            {
                SerializedProperty prop = so.FindProperty(kvp.Key);
                if (prop == null) continue;

                try
                {
                    SetPropertyValue(prop, kvp.Value);
                }
                catch
                {
                    // ignored
                }
            }

            so.ApplyModifiedProperties();
        }

        void SetPropertyValue(SerializedProperty prop, object value)
        {
            if (value == null) return;

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: prop.intValue = (int)value; break;
                case SerializedPropertyType.Boolean: prop.boolValue = (bool)value; break;
                case SerializedPropertyType.Float: prop.floatValue = (float)value; break;
                case SerializedPropertyType.String: prop.stringValue = (string)value; break;
                case SerializedPropertyType.Color: prop.colorValue = (Color)value; break;
                case SerializedPropertyType.Vector2: prop.vector2Value = (Vector2)value; break;
                case SerializedPropertyType.Vector3: prop.vector3Value = (Vector3)value; break;
                case SerializedPropertyType.Vector4: prop.vector4Value = (Vector4)value; break;
                case SerializedPropertyType.Quaternion: prop.quaternionValue = (Quaternion)value; break;
                case SerializedPropertyType.Enum: prop.enumValueIndex = (int)value; break;
            }
        }

        private static void RefreshBrowserIfOpen()
        {
            if (EditorWindow.HasOpenInstances<OverridesBrowserWindow>())
            {
                OverridesBrowserWindow.Open();
            }
        }

        private void RefreshContent(GameObject go)
        {
            changedComponents.Clear();
            changedComponents.AddRange(ChangesTrackerCore.GetChangedComponents(go));
        }
    }
}