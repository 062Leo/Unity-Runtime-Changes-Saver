using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace RuntimeChangesSaver.Editor
{
    [InitializeOnLoad]
    public static class ComponentHeaderGUI
    {
        // Stores which properties are selected for application, keyed by snapshot GUID
        private static readonly Dictionary<string, HashSet<string>> SelectedProperties = new Dictionary<string, HashSet<string>>();

        static ComponentHeaderGUI()
        {
            EditorApplication.delayCall += Initialize;
        }

        private static void Initialize()
        {
            // In Unity 6000+, we need to use a different approach
            // Subscribe to the editor update event to check for active editors
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            // Check if we have an active editor and draw our custom header
            var activeEditor = EditorWindow.focusedWindow as EditorWindow;
            if (activeEditor != null && Selection.activeGameObject != null)
            {
                var components = Selection.activeGameObject.GetComponents<Component>();
                foreach (var component in components)
                {
                    if (component != null && !(component is Transform))
                    {
                        DrawComponentHeader(component);
                    }
                }
            }
        }

        private static void DrawComponentHeader(Component component)
        {
            if (Application.isPlaying) return;

            var snapshot = RuntimeChangesSnapshotStore.Instance.FindSnapshotForComponent(component);
            bool isTracked = RuntimeChangesTracker.IsComponentTracked(component);

            if (snapshot == null && !isTracked) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (snapshot != null)
            {
                DrawSnapshotUI(snapshot, component);
            }
            else if (isTracked)
            {
                EditorGUILayout.LabelField("Tracking enabled. Changes will be captured on exiting play mode.", EditorStyles.miniLabel);
            }

            DrawTrackingToggle(component, isTracked);

            EditorGUILayout.EndVertical();
        }

        private static void DrawSnapshotUI(ComponentSnapshot snapshot, Component component)
        {
            // Initialize selection set if it doesn't exist
            if (!SelectedProperties.ContainsKey(snapshot.Guid))
            {
                SelectedProperties[snapshot.Guid] = new HashSet<string>();
            }
            var selection = SelectedProperties[snapshot.Guid];

            // Header with title and Mark/Unmark buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Runtime Changes", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Mark All", EditorStyles.miniButtonLeft, GUILayout.Width(70)))
            {
                foreach (var change in snapshot.PropertyChanges)
                {
                    selection.Add(change.PropertyPath);
                }
            }
            if (GUILayout.Button("Unmark All", EditorStyles.miniButtonRight, GUILayout.Width(70)))
            {
                selection.Clear();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            // Property list
            if (snapshot.PropertyChanges.Count == 0)
            {
                EditorGUILayout.LabelField("No property changes detected (only component was modified).", EditorStyles.miniLabel);
            }
            else
            {
                foreach (var change in snapshot.PropertyChanges)
                {
                    DrawPropertyChange(change, selection);
                }
            }

            // Footer with Apply/Discard buttons
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(selection.Count == 0))
            {
                if (GUILayout.Button("Apply Marked", GUILayout.Width(100)))
                {
                    ApplyMarkedChanges(snapshot, component, selection);
                    RuntimeChangesSnapshotStore.Instance.RemoveSnapshot(snapshot.Guid);
                }
            }

            if (GUILayout.Button("Apply All (Component)", GUILayout.Width(150)))
            {
                ApplyAllChanges(snapshot, component);
                RuntimeChangesSnapshotStore.Instance.RemoveSnapshot(snapshot.Guid);
            }

            if (GUILayout.Button("Discard", GUILayout.Width(70)))
            {
                if (EditorUtility.DisplayDialog("Discard Changes", "Are you sure?", "Yes", "No"))
                {
                    RuntimeChangesSnapshotStore.Instance.RemoveSnapshot(snapshot.Guid);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawPropertyChange(PropertyChange change, HashSet<string> selection)
        {
            EditorGUILayout.BeginHorizontal();
            bool isSelected = selection.Contains(change.PropertyPath);
            bool newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(14));

            if (newSelected != isSelected)
            {
                if (newSelected) selection.Add(change.PropertyPath); else selection.Remove(change.PropertyPath);
            }

            if (isSelected) GUI.contentColor = new Color(0.3f, 0.6f, 1f);
            EditorGUILayout.LabelField(new GUIContent(change.PropertyPath), EditorStyles.label);
            GUI.contentColor = Color.white;

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(change.ValueJson.Trim('"'), EditorStyles.miniLabel, GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawTrackingToggle(Component component, bool isTracked)
        {
            bool shouldTrack = EditorGUILayout.Toggle("Track Changes", isTracked);
            if (shouldTrack != isTracked)
            {
                if (shouldTrack) RuntimeChangesTracker.TrackComponent(component); else RuntimeChangesTracker.StopTrackingComponent(component);
            }
        }

        private static void ApplyMarkedChanges(ComponentSnapshot snapshot, Component targetComponent, HashSet<string> selection)
        {
            Undo.RecordObject(targetComponent, "Apply Marked Runtime Changes");
            var serializedObject = new SerializedObject(targetComponent);
            foreach (var propertyPath in selection)
            {
                var change = snapshot.PropertyChanges.FirstOrDefault(c => c.PropertyPath == propertyPath);
                if (change == null) continue;

                var property = serializedObject.FindProperty(propertyPath);
                if (property != null)
                {
                    ApplyPropertyValue(property, change);
                }
            }
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetComponent);
        }

        private static void ApplyAllChanges(ComponentSnapshot snapshot, Component targetComponent)
        {
            Undo.RecordObject(targetComponent, "Apply All Runtime Changes (Component)");
            EditorJsonUtility.FromJsonOverwrite(snapshot.FullComponentJson, targetComponent);
            EditorUtility.SetDirty(targetComponent);
        }

        private static void ApplyPropertyValue(SerializedProperty property, PropertyChange change)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer: property.intValue = int.Parse(change.ValueJson); break;
                case SerializedPropertyType.Boolean: property.boolValue = bool.Parse(change.ValueJson); break;
                case SerializedPropertyType.Float: property.floatValue = float.Parse(change.ValueJson); break;
                case SerializedPropertyType.String: property.stringValue = change.ValueJson.Trim('"'); break;
                case SerializedPropertyType.Vector3: property.vector3Value = JsonUtility.FromJson<Vector3>(change.ValueJson); break;
                case SerializedPropertyType.Quaternion: property.quaternionValue = JsonUtility.FromJson<Quaternion>(change.ValueJson); break;
                default: Debug.LogWarning($"[RuntimeChangesSaver] Applying property of type {property.propertyType} is not supported yet."); break;
            }
        }
    }
}
