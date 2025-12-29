using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;


public class PlayModeOverridesBrowserWindow : EditorWindow
{
    private class GameObjectEntry
    {
        public GameObject GameObject;
        public List<Component> ChangedComponents = new List<Component>();
        public bool Expanded = true;
    }

    private Dictionary<Scene, List<GameObjectEntry>> _sceneEntries = new Dictionary<Scene, List<GameObjectEntry>>();
    private Dictionary<Scene, bool> _sceneFoldouts = new Dictionary<Scene, bool>();
    private Vector2 _scroll;

    [MenuItem("Tools/Play Mode Overrides Browser")]
    public static void Open()
    {
        var window = GetWindow<PlayModeOverridesBrowserWindow>("Play Mode Overrides");
        window.RefreshData();
        window.Show();
    }

    private void OnEnable()
    {
        RefreshData();
    }

    private void OnHierarchyChange()
    {
        RefreshData();
    }

    private void OnInspectorUpdate()
    {
        Repaint();
    }

    private void RefreshData()
    {
        _sceneEntries.Clear();
        _sceneFoldouts.Clear();

        int sceneCount = SceneManager.sceneCount;

        if (Application.isPlaying)
        {
            // Im Play Mode: Änderungen dynamisch über Snapshots ermitteln
            for (int i = 0; i < sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                var list = new List<GameObjectEntry>();

                GameObject[] roots = scene.GetRootGameObjects();
                foreach (GameObject root in roots)
                {
                    CollectChangedGameObjectsRecursive(root, list);
                }

                if (list.Count > 0)
                {
                    _sceneEntries[scene] = list;
                    _sceneFoldouts[scene] = true;
                }
            }

            // Zusätzlich: bereits akzeptierte Overrides aus den ScriptableObject-Stores anzeigen,
            // damit sie auch während des Play Modes im Browser sichtbar sind.

            var transformStore = PlayModeTransformChangesStore.LoadExisting();
            if (transformStore != null)
            {
                foreach (var change in transformStore.changes)
                {
                    var scene = GetSceneByPathOrName(change.scenePath);
                    if (!scene.IsValid() || !scene.isLoaded)
                        continue;

                    var go = FindInSceneByPath(scene, change.objectPath);
                    if (go == null)
                        continue;

                    if (!_sceneEntries.TryGetValue(scene, out var list))
                    {
                        list = new List<GameObjectEntry>();
                        _sceneEntries[scene] = list;
                        _sceneFoldouts[scene] = true;
                    }

                    var entry = list.Find(e => e.GameObject == go);
                    if (entry == null)
                    {
                        entry = new GameObjectEntry { GameObject = go, Expanded = false };
                        list.Add(entry);
                    }

                    if (!entry.ChangedComponents.Contains(go.transform))
                    {
                        entry.ChangedComponents.Add(go.transform);
                    }
                }
            }

            var compStore = PlayModeComponentChangesStore.LoadExisting();
            if (compStore != null)
            {
                foreach (var change in compStore.changes)
                {
                    var scene = GetSceneByPathOrName(change.scenePath);
                    if (!scene.IsValid() || !scene.isLoaded)
                        continue;

                    var go = FindInSceneByPath(scene, change.objectPath);
                    if (go == null)
                        continue;

                    var type = System.Type.GetType(change.componentType);
                    if (type == null)
                        continue;

                    var allComps = go.GetComponents(type);
                    if (change.componentIndex < 0 || change.componentIndex >= allComps.Length)
                        continue;

                    var comp = allComps[change.componentIndex];
                    if (comp == null)
                        continue;

                    if (!_sceneEntries.TryGetValue(scene, out var list))
                    {
                        list = new List<GameObjectEntry>();
                        _sceneEntries[scene] = list;
                        _sceneFoldouts[scene] = true;
                    }

                    var entry = list.Find(e => e.GameObject == go);
                    if (entry == null)
                    {
                        entry = new GameObjectEntry { GameObject = go, Expanded = false };
                        list.Add(entry);
                    }

                    if (!entry.ChangedComponents.Contains(comp))
                    {
                        entry.ChangedComponents.Add(comp);
                    }
                }
            }
        }
        else
        {
            // Im Edit Mode: auf die persistenten ScriptableObject-Stores schauen,
            // um die zuletzt akzeptierten Änderungen anzuzeigen.
            var sceneMap = new Dictionary<Scene, Dictionary<GameObject, GameObjectEntry>>();

            var transformStore = PlayModeTransformChangesStore.LoadExisting();
            if (transformStore != null)
            {
                foreach (var change in transformStore.changes)
                {
                    AddTransformChangeToSceneMap(sceneMap, change);
                }
            }

            var compStore = PlayModeComponentChangesStore.LoadExisting();
            if (compStore != null)
            {
                foreach (var change in compStore.changes)
                {
                    AddComponentChangeToSceneMap(sceneMap, change);
                }
            }

            foreach (var kvp in sceneMap)
            {
                var scene = kvp.Key;
                var goDict = kvp.Value;
                var list = new List<GameObjectEntry>(goDict.Values);
                if (list.Count > 0)
                {
                    _sceneEntries[scene] = list;
                    _sceneFoldouts[scene] = true;
                }
            }
        }
    }

    private void CollectChangedGameObjectsRecursive(GameObject go, List<GameObjectEntry> list)
    {
        var changed = PlayModeChangesTracker.GetChangedComponents(go);
        if (changed != null && changed.Count > 0)
        {
            var entry = new GameObjectEntry
            {
                GameObject = go,
                ChangedComponents = changed,
                Expanded = false
            };
            list.Add(entry);
        }

        foreach (Transform child in go.transform)
        {
            CollectChangedGameObjectsRecursive(child.gameObject, list);
        }
    }

    private void AddTransformChangeToSceneMap(Dictionary<Scene, Dictionary<GameObject, GameObjectEntry>> sceneMap, PlayModeTransformChangesStore.TransformChange change)
    {
        var scene = GetSceneByPathOrName(change.scenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        var go = FindInSceneByPath(scene, change.objectPath);
        if (go == null)
            return;

        if (!sceneMap.TryGetValue(scene, out var goDict))
        {
            goDict = new Dictionary<GameObject, GameObjectEntry>();
            sceneMap[scene] = goDict;
        }

        if (!goDict.TryGetValue(go, out var entry))
        {
            entry = new GameObjectEntry { GameObject = go, Expanded = false };
            goDict[go] = entry;
        }

        if (!entry.ChangedComponents.Contains(go.transform))
        {
            entry.ChangedComponents.Add(go.transform);
        }
    }

    private void AddComponentChangeToSceneMap(Dictionary<Scene, Dictionary<GameObject, GameObjectEntry>> sceneMap, PlayModeComponentChangesStore.ComponentChange change)
    {
        var scene = GetSceneByPathOrName(change.scenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        var go = FindInSceneByPath(scene, change.objectPath);
        if (go == null)
            return;

        var type = System.Type.GetType(change.componentType);
        if (type == null)
            return;

        var allComps = go.GetComponents(type);
        if (change.componentIndex < 0 || change.componentIndex >= allComps.Length)
            return;

        var comp = allComps[change.componentIndex];
        if (comp == null)
            return;

        if (!sceneMap.TryGetValue(scene, out var goDict))
        {
            goDict = new Dictionary<GameObject, GameObjectEntry>();
            sceneMap[scene] = goDict;
        }

        if (!goDict.TryGetValue(go, out var entry))
        {
            entry = new GameObjectEntry { GameObject = go, Expanded = false };
            goDict[go] = entry;
        }

        if (!entry.ChangedComponents.Contains(comp))
        {
            entry.ChangedComponents.Add(comp);
        }
    }

    private Scene GetSceneByPathOrName(string scenePath)
    {
        var scene = SceneManager.GetSceneByPath(scenePath);
        if (!scene.IsValid())
        {
            scene = SceneManager.GetSceneByName(scenePath);
        }
        return scene;
    }

    private GameObject FindInSceneByPath(Scene scene, string path)
    {
        if (!scene.IsValid())
            return null;

        var parts = path.Split('/');
        if (parts.Length == 0)
            return null;

        GameObject current = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == parts[0])
            {
                current = root;
                break;
            }
        }

        if (current == null)
            return null;

        for (int i = 1; i < parts.Length; i++)
        {
            var childName = parts[i];
            Transform child = null;
            foreach (Transform t in current.transform)
            {
                if (t.name == childName)
                {
                    child = t;
                    break;
                }
            }

            if (child == null)
                return null;

            current = child.gameObject;
        }

        return current;
    }

    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
            {
                RefreshData();
            }
        }

        if (_sceneEntries.Count == 0)
        {
            EditorGUILayout.HelpBox("Keine geänderten Komponenten gefunden.", MessageType.Info);
            return;
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        foreach (var kvp in _sceneEntries)
        {
            Scene scene = kvp.Key;
            List<GameObjectEntry> entries = kvp.Value;

            bool expanded = _sceneFoldouts.TryGetValue(scene, out bool value) ? value : true;
            expanded = EditorGUILayout.Foldout(expanded, scene.name, true);
            _sceneFoldouts[scene] = expanded;

            if (!expanded)
                continue;

            EditorGUI.indentLevel++;

            foreach (var entry in entries)
            {
                EditorGUILayout.BeginHorizontal();
                entry.Expanded = EditorGUILayout.Foldout(entry.Expanded, entry.GameObject.name, true);
                EditorGUILayout.ObjectField(entry.GameObject, typeof(GameObject), true);
                EditorGUILayout.EndHorizontal();

                if (!entry.Expanded)
                    continue;

                EditorGUI.indentLevel++;

                foreach (var comp in entry.ChangedComponents)
                {
                    if (comp == null)
                        continue;

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(10);
                    if (GUILayout.Button(comp.GetType().Name, EditorStyles.linkLabel))
                    {
                        // Popup immer direkt unterhalb der Button-Zeile öffnen
                        Rect buttonRect = GUILayoutUtility.GetLastRect();
                        Rect popupRect = new Rect(buttonRect.x, buttonRect.yMax, buttonRect.width, 0f);
                        PopupWindow.Show(popupRect, new PlayModeOverrideComparePopup(comp));
                    }
                    EditorGUILayout.ObjectField(comp, typeof(Component), true);
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndScrollView();
    }
}
