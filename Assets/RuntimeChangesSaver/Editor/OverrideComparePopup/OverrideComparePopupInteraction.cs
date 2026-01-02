﻿using UnityEditor;
using UnityEngine;
using RuntimeChangesSaver.Editor.ChangesTracker;

namespace RuntimeChangesSaver.Editor.OverrideComparePopup
{
    /// <summary>
    /// Handles user interactions: drag-drop, apply, revert buttons.
    /// </summary>
    internal class OverrideComparePopupInteraction
    {
        private bool isDragging = false;
        private Vector2 dragLastMousePos = Vector2.zero;
        private const float DragHeaderHeight = 20f;

        private Component liveComponent;
        private GameObject snapshotGO;
        private Component snapshotComponent;

        public OverrideComparePopupInteraction(Component liveComponent, GameObject snapshotGO, Component snapshotComponent)
        {
            this.liveComponent = liveComponent;
            this.snapshotGO = snapshotGO;
            this.snapshotComponent = snapshotComponent;
        }

        /// <summary>
        /// Handles drag-and-drop functionality for moving the popup window.
        /// </summary>
        public void HandleDragAndDrop(Rect rect, EditorWindow editorWindow)
        {
            Rect dragHeaderRect = new Rect(rect.x, rect.y, rect.width, DragHeaderHeight);

            if (Event.current.type == EventType.MouseDown && dragHeaderRect.Contains(Event.current.mousePosition))
            {
                isDragging = true;
                dragLastMousePos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                Event.current.Use();
            }
            else if (Event.current.type == EventType.MouseDrag && isDragging)
            {
                Vector2 currentScreenPos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                Vector2 delta = currentScreenPos - dragLastMousePos;

                Rect newRect = editorWindow.position;
                newRect.position += delta;
                editorWindow.position = newRect;

                dragLastMousePos = currentScreenPos;
                Event.current.Use();
            }
            else if (Event.current.type == EventType.MouseUp)
            {
                isDragging = false;
            }

            if (Event.current.type == EventType.Repaint)
            {
                GUI.Box(dragHeaderRect, GUIContent.none, EditorStyles.toolbar);
            }
        }

        /// <summary>
        /// Reverts all changes made in Play Mode back to the snapshot state.
        /// </summary>
        public void RevertChanges()
        {
            if (snapshotComponent == null) return;

            SerializedObject sourceSO = new SerializedObject(snapshotComponent);
            SerializedObject targetSO = new SerializedObject(liveComponent);

            SerializedProperty sourceProp = sourceSO.GetIterator();
            bool enterChildren = true;

            while (sourceProp.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (sourceProp.name == "m_Script") continue;

                SerializedProperty targetProp = targetSO.FindProperty(sourceProp.propertyPath);
                if (targetProp != null && targetProp.propertyType == sourceProp.propertyType)
                {
                    targetSO.CopyFromSerializedProperty(sourceProp);
                }
            }

            targetSO.ApplyModifiedProperties();

            if (Application.isPlaying && liveComponent != null)
            {
                if (liveComponent is Transform or RectTransform)
                {
                    ChangesTrackerCore.ResetTransformBaseline(liveComponent.gameObject);
                }
                else
                {
                    ChangesTrackerCore.ResetComponentBaseline(liveComponent);
                }
            }

            RemoveFromStore();
            RefreshBrowserIfOpen();
        }

        /// <summary>
        /// Applies the current Play Mode changes to the acceptance system.
        /// </summary>
        public void ApplyChanges()
        {
            if (liveComponent is Transform or RectTransform)
            {
                ChangesTrackerCore.AcceptTransformChanges(liveComponent.gameObject);
            }
            else
            {
                ChangesTrackerCore.AcceptComponentChanges(liveComponent);
            }

            RefreshBrowserIfOpen();
        }

        /// <summary>
        /// Checks if there are unsaved changes compared to the snapshot.
        /// </summary>
        public bool HasUnsavedChanges()
        {
            if (snapshotComponent == null || liveComponent == null)
                return false;

            var snapshotSO = new SerializedObject(snapshotComponent);
            var liveSO = new SerializedObject(liveComponent);

            SerializedProperty prop = snapshotSO.GetIterator();
            bool enterChildren = true;

            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (prop.name == "m_Script")
                    continue;

                var liveProp = liveSO.FindProperty(prop.propertyPath);
                if (liveProp == null || liveProp.propertyType != prop.propertyType)
                    continue;

                if (OverrideComparePopupSerialization.PropertiesDiffer(prop, liveProp))
                    return true;
            }

            return false;
        }

        private void RemoveFromStore()
        {
            if (liveComponent == null) return;

            var go = liveComponent.gameObject;
            string scenePath = go.scene.path;
            if (string.IsNullOrEmpty(scenePath))
                scenePath = go.scene.name;

            string objectPath = OverrideComparePopupUtilities.GetGameObjectPath(go.transform);

            if (liveComponent is Transform or RectTransform)
            {
                var tStore = TransformChangesStore.LoadExisting();
                if (tStore != null)
                {
                    int index = tStore.changes.FindIndex(c => c.scenePath == scenePath && c.objectPath == objectPath);
                    if (index >= 0)
                    {
                        tStore.changes.RemoveAt(index);
                        EditorUtility.SetDirty(tStore);
                        AssetDatabase.SaveAssets();
                    }
                }
            }
            else
            {
                var cStore = ComponentChangesStore.LoadExisting();
                if (cStore != null)
                {
                    var type = liveComponent.GetType();
                    string componentType = type.AssemblyQualifiedName;
                    var allOfType = go.GetComponents(type);
                    int compIndex = System.Array.IndexOf(allOfType, liveComponent);

                    int index = cStore.changes.FindIndex(c =>
                        c.scenePath == scenePath &&
                        c.objectPath == objectPath &&
                        c.componentType == componentType &&
                        c.componentIndex == compIndex);

                    if (index >= 0)
                    {
                        cStore.changes.RemoveAt(index);
                        EditorUtility.SetDirty(cStore);
                        AssetDatabase.SaveAssets();
                    }
                }
            }
        }

        private static void RefreshBrowserIfOpen()
        {
            if (EditorWindow.HasOpenInstances<OverridesBrowserWindow>())
            {
                OverridesBrowserWindow.Open();
            }
        }
    }
}


