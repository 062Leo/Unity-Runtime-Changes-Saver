using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RuntimeChangesSaver.Editor
{
    [InitializeOnLoad]
    public static class RuntimeChangesTracker
    {
        private static readonly List<Component> TrackedComponents = new List<Component>();
        private static readonly Dictionary<GlobalObjectId, string> PrePlayModeStates = new Dictionary<GlobalObjectId, string>();

        static RuntimeChangesTracker()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void TrackComponent(Component component)
        {
            if (component != null && !TrackedComponents.Contains(component))
            {
                TrackedComponents.Add(component);
                Debug.Log($"[RuntimeChangesSaver] Started tracking {component.GetType().Name} on {component.gameObject.name}");
            }
        }

        public static void StopTrackingComponent(Component component)
        {
            if (component != null && TrackedComponents.Contains(component))
            {
                TrackedComponents.Remove(component);
                Debug.Log($"[RuntimeChangesSaver] Stopped tracking {component.GetType().Name} on {component.gameObject.name}");
            }
        }

        public static bool IsComponentTracked(Component component)
        {
            return component != null && TrackedComponents.Contains(component);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    CapturePrePlayModeStates();
                    break;

                case PlayModeStateChange.EnteredEditMode:
                    CapturePostPlayModeStates();
                    break;
            }
        }

        private static void CapturePrePlayModeStates()
        {
            PrePlayModeStates.Clear();
            foreach (var component in TrackedComponents.Where(c => c != null))
            {
                var id = GlobalObjectId.GetGlobalObjectIdSlow(component);
                PrePlayModeStates[id] = EditorJsonUtility.ToJson(component, true);
            }
        }

        private static void CapturePostPlayModeStates()
        {
            var store = RuntimeChangesSnapshotStore.Instance;

            foreach (var preState in PrePlayModeStates)
            {
                var component = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(preState.Key) as Component;
                if (component == null) continue;

                var postStateJson = EditorJsonUtility.ToJson(component, true);
                if (preState.Value == postStateJson) continue; // No changes detected

                var snapshot = new ComponentSnapshot(component);
                snapshot.FullComponentJson = postStateJson; // This is the play-mode state
                CalculatePropertyChanges(snapshot, preState.Value, postStateJson);
                
                store.AddSnapshot(snapshot);
            }

            PrePlayModeStates.Clear();
        }

        private static void CalculatePropertyChanges(ComponentSnapshot snapshot, string preJson, string postJson)
        {
            var preObject = ScriptableObject.CreateInstance<TempObjectContainer>();
            var postObject = ScriptableObject.CreateInstance<TempObjectContainer>();

            EditorJsonUtility.FromJsonOverwrite(preJson, preObject);
            EditorJsonUtility.FromJsonOverwrite(postJson, postObject);

            var preSerialized = new SerializedObject(preObject);
            var postSerialized = new SerializedObject(postObject);

            var preIterator = preSerialized.GetIterator();
            var postIterator = postSerialized.GetIterator();

            while (preIterator.NextVisible(true) && postIterator.NextVisible(true))
            {
                if (!SerializedProperty.DataEquals(preIterator, postIterator))
                {
                    snapshot.PropertyChanges.Add(new PropertyChange
                    {
                        PropertyPath = preIterator.propertyPath,
                        // A more robust solution would be needed for complex types
                        ValueJson = GetPropertyValueAsJson(postIterator),
                        TypeName = postIterator.propertyType.ToString()
                    });
                }
            }
            
            Object.DestroyImmediate(preObject);
            Object.DestroyImmediate(postObject);
        }
        
        private static string GetPropertyValueAsJson(SerializedProperty prop)
        {
            // This is a simplified version. A real implementation would need to handle all serializable types.
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue.ToString();
                case SerializedPropertyType.Boolean: return prop.boolValue.ToString().ToLower();
                case SerializedPropertyType.Float: return prop.floatValue.ToString();
                case SerializedPropertyType.String: return $"\"{prop.stringValue}\"";
                case SerializedPropertyType.Vector3: return JsonUtility.ToJson(prop.vector3Value);
                case SerializedPropertyType.Quaternion: return JsonUtility.ToJson(prop.quaternionValue);
                // Add other types as needed
                default: return "null"; // Unsupported type
            }
        }

        // Helper class to hold temporary component data for diffing
        private class TempObjectContainer : ScriptableObject
        {
        }
    }
}
