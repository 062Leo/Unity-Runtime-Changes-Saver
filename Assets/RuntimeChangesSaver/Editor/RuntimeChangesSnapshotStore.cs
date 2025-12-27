using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RuntimeChangesSaver.Editor
{
    public class RuntimeChangesSnapshotStore : ScriptableObject
    {
        private const string StorePath = "Assets/RuntimeChangesSaver/Editor/DoNotDelete_RuntimeChangesStore.asset";

        [SerializeField]
        private List<ComponentSnapshot> _snapshots = new List<ComponentSnapshot>();

        public IReadOnlyList<ComponentSnapshot> Snapshots => _snapshots;

        private static RuntimeChangesSnapshotStore _instance;
        public static RuntimeChangesSnapshotStore Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = LoadOrCreateInstance();
                }
                return _instance;
            }
        }

        private static RuntimeChangesSnapshotStore LoadOrCreateInstance()
        {
            var instance = AssetDatabase.LoadAssetAtPath<RuntimeChangesSnapshotStore>(StorePath);
            if (instance == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StorePath));
                instance = CreateInstance<RuntimeChangesSnapshotStore>();
                AssetDatabase.CreateAsset(instance, StorePath);
                AssetDatabase.SaveAssets();
            }
            return instance;
        }

        public void AddSnapshot(ComponentSnapshot snapshot)
        {
            // Avoid duplicates if a snapshot for the same component is already taken
            _snapshots.RemoveAll(s => s.TargetComponentId.Equals(snapshot.TargetComponentId));
            _snapshots.Add(snapshot);
            EditorUtility.SetDirty(this);
        }

        public void RemoveSnapshot(string guid)
        {
            _snapshots.RemoveAll(s => s.Guid == guid);
            EditorUtility.SetDirty(this);
        }

        public void ClearAllSnapshots()
        {
            _snapshots.Clear();
            EditorUtility.SetDirty(this);
        }

        public ComponentSnapshot FindSnapshotForComponent(Component component)
        {
            if (component == null) return null;
            var componentId = GlobalObjectId.GetGlobalObjectIdSlow(component);
            return _snapshots.Find(s => s.TargetComponentId.Equals(componentId));
        }
    }
}
