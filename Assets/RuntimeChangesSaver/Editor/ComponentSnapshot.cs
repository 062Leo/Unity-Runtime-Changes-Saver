using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RuntimeChangesSaver.Editor
{
    [Serializable]
    public class ComponentSnapshot
    {
        public string componentType; 
        public string globalObjectId;
        public List<string> materialGuids = new List<string>();
        public Dictionary<string, object> properties = new Dictionary<string, object>();

        public ComponentSnapshot() { }

        public ComponentSnapshot(Component comp)
        {
            componentType = comp.GetType().AssemblyQualifiedName;
            // Capture GlobalObjectId of the owning GameObject for robust GUID-based lookup
            globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(comp.gameObject).ToString();
        }
    }
}
