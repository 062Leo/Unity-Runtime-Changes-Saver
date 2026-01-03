using System;
using UnityEditor;
using UnityEngine;

namespace RuntimeChangesSaver.Editor
{
    [Serializable]
    public class GameObjectNameSnapshot
    {
        public string objectName;
        public string globalObjectId;

        public GameObjectNameSnapshot(GameObject go)
        {
            objectName = go.name;
            globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(go).ToString();
        }
    }
}
