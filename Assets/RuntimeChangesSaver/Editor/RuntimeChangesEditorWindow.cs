using UnityEditor;
using UnityEngine;

namespace RuntimeChangesSaver.Editor
{
    public class RuntimeChangesEditorWindow : EditorWindow
    {
        private enum ApplyMode { ComponentLevel, PropertyLevel }
        private ApplyMode _applyMode = ApplyMode.ComponentLevel;
        private Vector2 _scrollPosition;

        [MenuItem("Window/Runtime Changes Saver")]
        public static void ShowWindow()
        {
            GetWindow<RuntimeChangesEditorWindow>("Runtime Changes Saver");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Captured Snapshots", EditorStyles.boldLabel);
            
            _applyMode = (ApplyMode)EditorGUILayout.EnumPopup("Apply Mode", _applyMode);

            if (GUILayout.Button("Clear All Snapshots"))
            {
                if (EditorUtility.DisplayDialog("Clear All Snapshots", "Are you sure you want to delete all captured snapshots? This action cannot be undone.", "Yes", "No"))
                {
                    RuntimeChangesSnapshotStore.Instance.ClearAllSnapshots();
                }
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            var snapshots = RuntimeChangesSnapshotStore.Instance.Snapshots;
            if (snapshots.Count == 0)
            {
                EditorGUILayout.HelpBox("No changes captured. Mark components with the 'Track Changes' checkbox in the inspector and enter play mode to capture changes.", MessageType.Info);
            }
            else
            {
                foreach (var snapshot in snapshots)
                {
                    DrawSnapshot(snapshot);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSnapshot(ComponentSnapshot snapshot)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField($"{snapshot.GameObjectName} -> {snapshot.ComponentTypeName}", EditorStyles.boldLabel);

            var targetObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(snapshot.TargetComponentId);
            if (targetObject == null)
            {
                EditorGUILayout.HelpBox("The original component could not be found. It may have been deleted.", MessageType.Warning);
            }
            else
            {
                if (GUILayout.Button("Apply Changes"))
                {
                    ApplyChanges(snapshot, targetObject as Component);
                }
            }

            if (GUILayout.Button("Discard"))
            {
                RuntimeChangesSnapshotStore.Instance.RemoveSnapshot(snapshot.Guid);
                Repaint();
            }

            EditorGUILayout.EndVertical();
        }

        private void ApplyChanges(ComponentSnapshot snapshot, Component targetComponent)
        {
            if (targetComponent == null) return;

            Undo.RecordObject(targetComponent, "Apply Runtime Changes");

            if (_applyMode == ApplyMode.ComponentLevel)
            {
                EditorJsonUtility.FromJsonOverwrite(snapshot.FullComponentJson, targetComponent);
            }
            else // Property-Level
            {
                var serializedObject = new SerializedObject(targetComponent);
                foreach (var change in snapshot.PropertyChanges)
                {
                    var property = serializedObject.FindProperty(change.PropertyPath);
                    if (property != null)
                    {
                        ApplyPropertyValue(property, change);
                    }
                }
                serializedObject.ApplyModifiedProperties();
            }
            
            EditorUtility.SetDirty(targetComponent);
            Debug.Log($"[RuntimeChangesSaver] Applied changes to {targetComponent.GetType().Name} on {targetComponent.gameObject.name}");
        }

        private void ApplyPropertyValue(SerializedProperty property, PropertyChange change)
        {
            // This needs to be expanded to support more types, matching the tracker's serialization
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer: property.intValue = int.Parse(change.ValueJson); break;
                case SerializedPropertyType.Boolean: property.boolValue = bool.Parse(change.ValueJson); break;
                case SerializedPropertyType.Float: property.floatValue = float.Parse(change.ValueJson); break;
                case SerializedPropertyType.String: property.stringValue = change.ValueJson.Trim('"'); break;
                case SerializedPropertyType.Vector3: property.vector3Value = JsonUtility.FromJson<Vector3>(change.ValueJson); break;
                case SerializedPropertyType.Quaternion: property.quaternionValue = JsonUtility.FromJson<Quaternion>(change.ValueJson); break;
                // Add other types as needed
                default: Debug.LogWarning($"[RuntimeChangesSaver] Applying property of type {property.propertyType} is not supported yet."); break;
            }
        }
    }
}
