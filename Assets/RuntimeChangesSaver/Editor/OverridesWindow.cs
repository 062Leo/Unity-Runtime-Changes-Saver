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

            if (GUI.Button(revertOriginalRect, "Revert to Original"))
            {
                RevertToOriginal();
                RefreshBrowserIfOpen();
                editorWindow.Close();
            }

            if (GUI.Button(revertSavedRect, "Revert to Saved"))
            {
                RevertToSaved();
                RefreshBrowserIfOpen();
                editorWindow.Close();
            }

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

            // Revert Transform strictly to stored original values
            var tStore = TransformChangesStore.LoadExisting();
            if (tStore != null)
            {
                int index = tStore.changes.FindIndex(c => c.scenePath == scenePath && c.objectPath == objectPath);
                if (index >= 0)
                {
                    var stored = tStore.changes[index];
                    if (stored.hasOriginalValues)
                    {
                        var transform = targetGO.transform;
                        transform.localPosition = stored.originalPosition;
                        transform.localRotation = stored.originalRotation;
                        transform.localScale = stored.originalScale;

                        RectTransform rt = transform as RectTransform;
                        if (stored.isRectTransform && rt != null)
                        {
                            rt.anchoredPosition = stored.originalAnchoredPosition;
                            rt.anchoredPosition3D = stored.originalAnchoredPosition3D;
                            rt.anchorMin = stored.originalAnchorMin;
                            rt.anchorMax = stored.originalAnchorMax;
                            rt.pivot = stored.originalPivot;
                            rt.sizeDelta = stored.originalSizeDelta;
                            rt.offsetMin = stored.originalOffsetMin;
                            rt.offsetMax = stored.originalOffsetMax;
                        }
                    }
                }
            }

            // Revert other components strictly to stored original values
            var cStore = ComponentChangesStore.LoadExisting();
            foreach (var comp in changedComponents)
            {
                if (comp is Transform) continue;

                if (cStore == null)
                    continue;

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
                {
                    var stored = cStore.changes[idx];
                    if (stored.hasOriginalValues && stored.originalSerializedValues.Count == stored.propertyPaths.Count)
                    {
                        var targetSO = new SerializedObject(comp);
                        for (int i = 0; i < stored.propertyPaths.Count; i++)
                        {
                            string propPath = stored.propertyPaths[i];
                            var prop = targetSO.FindProperty(propPath);
                            if (prop != null)
                            {
                                OverrideComparePopupSerialization.ApplySerializedComponentValue(prop, stored.originalValueTypes[i], stored.originalSerializedValues[i]);
                            }
                        }
                        targetSO.ApplyModifiedProperties();
                    }
                }
            }

            // Remove entries from stores after successful revert to original
            if (tStore != null)
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

            // Revert Transform
            var tStore = TransformChangesStore.LoadExisting();
            if (tStore != null)
            {
                int index = tStore.changes.FindIndex(c => c.scenePath == scenePath && c.objectPath == objectPath);
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
                        c.scenePath == scenePath &&
                        c.objectPath == objectPath &&
                        c.componentType == componentType &&
                        c.componentIndex == compIndex);

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
                    }
                }
            }
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