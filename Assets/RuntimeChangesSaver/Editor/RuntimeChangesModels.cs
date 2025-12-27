using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RuntimeChangesSaver.Editor
{
    [Serializable]
    public class PropertyChange
    {
        public string PropertyPath;
        public string ValueJson;
        public string TypeName; 
    }

    [Serializable]
    public class ComponentSnapshot
    {
        public string Guid;
        public GlobalObjectId TargetComponentId;
        public string ComponentTypeName;
        public string GameObjectName;

        // For Component-Level
        public string FullComponentJson;

        // For Property-Level
        public List<PropertyChange> PropertyChanges = new List<PropertyChange>();

        public ComponentSnapshot(Component component)
        {
            Guid = System.Guid.NewGuid().ToString();
            TargetComponentId = GlobalObjectId.GetGlobalObjectIdSlow(component);
            ComponentTypeName = component.GetType().FullName;
            GameObjectName = component.gameObject.name;
            FullComponentJson = EditorJsonUtility.ToJson(component, true);
        }
    }
}
