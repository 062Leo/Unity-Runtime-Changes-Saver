﻿using System.Collections.Generic;
using System.Linq;
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
        private float headerHeight = 28f;
        private const float FooterHeight = 50f;

        private readonly bool showMaterialToggle;
        private readonly bool hasNameDelta;

        public OverridesWindow(GameObject go)
        {
            targetGO = go;
            changedComponents = ChangesTrackerCore.GetChangedComponents(go);

            hasNameDelta = ChangesTrackerCore.HasNameDelta(go);
            showMaterialToggle = changedComponents.Any(c => c is Renderer r && ChangesTrackerCore.HasMaterialDelta(r));

            if (showMaterialToggle) headerHeight += 18f;
        }

        public override Vector2 GetWindowSize()
        {
            int rowCount = changedComponents.Count + (hasNameDelta ? 1 : 0);
            int count = Mathf.Max(1, rowCount);
            float listHeight = count * RowHeight;
            float totalHeight = headerHeight + listHeight + FooterHeight + 10;
            return new Vector2(320, Mathf.Min(500, totalHeight));
        }

        public override void OnGUI(Rect rect)
        {
            // Layout
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, headerHeight);
            DrawHeader(headerRect);

            if (changedComponents.Count == 0 && !hasNameDelta)
            {
                Rect helpRect = new Rect(rect.x + 10, rect.y + headerHeight, rect.width - 20, 40);
                GUI.Label(helpRect, "No changed components", EditorStyles.helpBox);
                return;
            }

            // List
            float listHeight = rect.height - headerHeight - FooterHeight;
            Rect listRect = new Rect(rect.x, rect.y + headerHeight, rect.width, listHeight);
            DrawComponentList(listRect);

            // Footer
            Rect footerRect = new Rect(rect.x, rect.y + headerHeight + listHeight, rect.width, FooterHeight);
            DrawFooter(footerRect);
        }

        void DrawHeader(Rect rect)
        {
            EditorGUI.LabelField(
                new Rect(rect.x + 6, rect.y + 6, rect.width - 12, 20),
                "Play Mode Overrides",
                EditorStyles.boldLabel
            );

            float y = rect.y + 26f;

            if (showMaterialToggle)
            {
                bool persistMaterials = ChangesTrackerCore.ShouldPersistMaterialChanges();
                bool newPersistMaterials = GUI.Toggle(new Rect(rect.x + 6, y, rect.width - 12, 18), persistMaterials, "Persist Renderer material assignments");
                if (newPersistMaterials != persistMaterials)
                {
                    ChangesTrackerCore.SetPersistMaterialChanges(newPersistMaterials);
                }
            }
        }

        void DrawComponentList(Rect rect)
        {
            int rowCount = changedComponents.Count + (hasNameDelta ? 1 : 0);
            Rect viewRect = new Rect(0, 0, rect.width - 16, rowCount * RowHeight);
            scroll = GUI.BeginScrollView(rect, scroll, viewRect);

            float y = 0f;

            if (hasNameDelta)
            {
                Rect nameRow = new Rect(0, y, viewRect.width, RowHeight);
                DrawNameRow(nameRow);
                y += RowHeight;
            }

            for (int i = 0; i < changedComponents.Count; i++)
            {
                Rect row = new Rect(0, y, viewRect.width, RowHeight);
                DrawRow(row, changedComponents[i]);
                y += RowHeight;
            }

            GUI.EndScrollView();
        }

        void DrawNameRow(Rect rowRect)
        {
            if (Event.current.type == EventType.Repaint)
                EditorStyles.helpBox.Draw(rowRect, false, false, false, false);

            var labelRect = new Rect(rowRect.x + 6, rowRect.y + 3, rowRect.width - 12, 16);
            GUI.Label(labelRect, "GameObject Name", EditorStyles.label);
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
            string targetGuid = GlobalObjectId.GetGlobalObjectIdSlow(targetGO).ToString();

            var tStore = TransformChangesStore.LoadExisting();
            var cStore = ComponentChangesStore.LoadExisting();
            var originalTransformStore = TransformOriginalStore.LoadExisting();
            var originalComponentStore = ComponentOriginalStore.LoadExisting();

            bool transformListed = changedComponents.Contains(targetGO.transform);
            bool nameOnly = !transformListed && hasNameDelta;
            
            if (transformListed)
            {
                bool reverted = false;
                var originalTransform = originalTransformStore?.entries.Find(e =>
                    (!string.IsNullOrEmpty(e.globalObjectId) && e.globalObjectId == targetGuid) ||
                    (string.IsNullOrEmpty(e.globalObjectId) && e.scenePath == scenePath && e.objectPath == objectPath));

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

            // Revert name changes only if there is a name delta
            if (hasNameDelta)
            {
                var nameOriginalStore = GameObjectNameOriginalStore.LoadExisting();
                var originalName = nameOriginalStore?.entries.Find(e =>
                    (!string.IsNullOrEmpty(e.globalObjectId) && e.globalObjectId == targetGuid) ||
                    (string.IsNullOrEmpty(e.globalObjectId) && e.scenePath == scenePath && e.objectPath == objectPath));

                if (originalName != null && !string.IsNullOrEmpty(originalName.originalName))
                {
                    targetGO.name = originalName.originalName;
                }
                else
                {
                    // Fallback for unsaved name changes: use play-enter snapshot
                    var originalNameSnapshot = SnapshotManager.GetNameSnapshot(targetGO);
                    if (originalNameSnapshot != null && !string.IsNullOrEmpty(originalNameSnapshot.objectName))
                    {
                        targetGO.name = originalNameSnapshot.objectName;
                    }
                }

                // Reset name baseline so HasNameDelta clears and buttons disable correctly
                SnapshotManager.SetNameSnapshot(targetGO, new GameObjectNameSnapshot(targetGO));
            }

            // Reset transform baseline after revert so the object is no longer flagged as changed
            SnapshotManager.ResetTransformBaseline(targetGO);

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

                    if (comp is Renderer renderer && originalEntry.materialGuids is { Count: > 0 })
                    {
                        ApplyMaterials(renderer, originalEntry.materialGuids);
                    }
                    reverted = true;
                }

                if (!reverted)
                {
                    string compKey = ChangesTrackerCore.GetComponentKey(comp);
                    var snapshot = ChangesTrackerCore.GetComponentSnapshot(targetGO, compKey);
                    if (snapshot != null)
                    {
                        RevertComponent(comp, snapshot);
                        if (comp is Renderer renderer && snapshot.materialGuids is { Count: > 0 })
                        {
                            ApplyMaterials(renderer, snapshot.materialGuids);
                        }
                    }
                }
            }

            if ((transformListed || nameOnly) && tStore != null)
            {
                int removeIndex = tStore.changes.FindIndex(c =>
                    (!string.IsNullOrEmpty(c.globalObjectId) && c.globalObjectId == targetGuid) ||
                    (string.IsNullOrEmpty(c.globalObjectId) && c.scenePath == scenePath && c.objectPath == objectPath));
                if (removeIndex >= 0)
                {
                    tStore.changes.RemoveAt(removeIndex);
                    EditorUtility.SetDirty(tStore);
                }
            }

            // Remove name changes from store only when name delta was present
            if (hasNameDelta)
            {
                var nameStore = GameObjectNameChangesStore.LoadExisting();
                if (nameStore != null)
                {
                    int removeIndex = nameStore.changes.FindIndex(c =>
                        (!string.IsNullOrEmpty(c.globalObjectId) && c.globalObjectId == targetGuid) ||
                        (string.IsNullOrEmpty(c.globalObjectId) && c.scenePath == scenePath && c.objectPath == objectPath));
                    if (removeIndex >= 0)
                    {
                        nameStore.changes.RemoveAt(removeIndex);
                        EditorUtility.SetDirty(nameStore);
                    }
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
            bool nameOnly = !transformListed && hasNameDelta;

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
                }
            }

            // Revert name only when name delta exists
            if (hasNameDelta)
            {
                var nameStore = GameObjectNameChangesStore.LoadExisting();
                if (nameStore != null)
                {
                    int index = nameStore.changes.FindIndex(c =>
                        (!string.IsNullOrEmpty(c.globalObjectId) && c.globalObjectId == targetGuid) ||
                        (string.IsNullOrEmpty(c.globalObjectId) && c.scenePath == scenePath && c.objectPath == objectPath));
                    if (index >= 0)
                    {
                        var storedChange = nameStore.changes[index];
                        targetGO.name = storedChange.newName;
                    }
                }
            }

            // Reset baselines so the object is no longer flagged as changed
            SnapshotManager.ResetTransformBaseline(targetGO);
            SnapshotManager.SetNameSnapshot(targetGO, new GameObjectNameSnapshot(targetGO));

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

                        if (storedChange.includeMaterialChanges && comp is Renderer renderer)
                        {
                            ApplyMaterials(renderer, storedChange.materialGuids);
                        }

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
            string targetGuid = GlobalObjectId.GetGlobalObjectIdSlow(targetGO).ToString();

            bool transformListed = changedComponents.Contains(targetGO.transform);
            bool nameOnly = !transformListed && hasNameDelta;

            // Transform saved?
            var tStore = TransformChangesStore.LoadExisting();
            if (transformListed && tStore != null)
            {
                int index = tStore.changes.FindIndex(c => c.scenePath == scenePath && c.objectPath == objectPath);
                if (index >= 0)
                    return true;
            }

            // Name saved? (always check, even if no current name delta)
            var nameStore = GameObjectNameChangesStore.LoadExisting();
            if (nameStore != null)
            {
                int index = nameStore.changes.FindIndex(c =>
                    (!string.IsNullOrEmpty(c.globalObjectId) && c.globalObjectId == targetGuid) ||
                    (string.IsNullOrEmpty(c.globalObjectId) && c.scenePath == scenePath && c.objectPath == objectPath));
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
            bool hasTransformChange = changedComponents.Any(comp => comp is Transform or RectTransform);
            
            if (hasTransformChange)
            {
                ChangesTrackerCore.AcceptTransformChanges(targetGO);
            }

            if (hasNameDelta)
            {
                ChangesTrackerCore.AcceptNameChanges(targetGO);
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

        private static void ApplyMaterials(Renderer renderer, List<string> materialGuids)
        {
            if (renderer == null || materialGuids == null || materialGuids.Count == 0)
                return;

            var current = renderer.sharedMaterials;
            var applied = new Material[materialGuids.Count];

            for (int i = 0; i < materialGuids.Count; i++)
            {
                string guid = materialGuids[i];
                if (string.IsNullOrEmpty(guid))
                {
                    applied[i] = null;
                    continue;
                }

                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null && i < current.Length)
                {
                    applied[i] = current[i];
                    Debug.LogWarning($"[RCS][Revert] Material GUID '{guid}' could not be resolved for renderer '{renderer.name}'");
                }
                else
                {
                    applied[i] = mat;
                }
            }

            renderer.sharedMaterials = applied;
        }
    }
}