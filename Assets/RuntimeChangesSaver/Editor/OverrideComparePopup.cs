using UnityEditor;
using UnityEngine;

namespace RuntimeChangesSaver.Editor
{
    internal class OverrideComparePopup : PopupWindowContent
    {
        private readonly Component liveComponent;

        private GameObject snapshotGO;
        private Component snapshotComponent;
        private UnityEditor.Editor leftEditor;
        private UnityEditor.Editor rightEditor;
        
        // Gemeinsamer Scroll-Wert (0 bis 1)
        private float scrollNormalized = 0f;
        private float leftMaxScroll = 0f;
        private float rightMaxScroll = 0f;

        private const float MinWidth = 350f;
        private const float HeaderHeight = 24f;
        private const float FooterHeight = 40f;
        private const float FixedWindowHeight = 400f; // Feste Fensterhöhe
        private float scrollVelocity = 5f;
        private const float MaxWindowHeight = 400f;
        private const float MinWindowHeight = 250f;
        private float currentWindowHeight = -1f;
        private float targetWindowHeight = -1f;
        private bool initialSizeSet = false;

        // Drag-and-Drop Variablen
        private bool isDragging = false;
        private Vector2 dragLastMousePos = Vector2.zero;
        private const float DragHeaderHeight = 20f;
        
        public OverrideComparePopup(Component component)
        {
            liveComponent = component;
            CreateSnapshotAndEditors();
        }

        void CreateSnapshotAndEditors()
        {
            var go = liveComponent.gameObject;
            snapshotGO = new GameObject("SnapshotTransform")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            if (liveComponent is Transform)
            {
            
                TransformChangesStore.TransformChange storeMatch = null;
                var store = TransformChangesStore.LoadExisting();
                if (store != null)
                {
                    string scenePath = go.scene.path;
                    if (string.IsNullOrEmpty(scenePath))
                        scenePath = go.scene.name;

                    string objectPath = GetGameObjectPathForPopup(go.transform);

                    foreach (var c in store.changes)
                    {
                        if (c.scenePath == scenePath && c.objectPath == objectPath)
                        {
                            storeMatch = c;
                            break;
                        }
                    }
                }

                if (storeMatch != null)
                {
                    var change = storeMatch;

                    bool useOriginal = change.hasOriginalValues;

                    Vector3 basePos = useOriginal ? change.originalPosition : change.position;
                    Quaternion baseRot = useOriginal ? change.originalRotation : change.rotation;
                    Vector3 baseScale = useOriginal ? change.originalScale : change.scale;

                    Vector2 baseAnchoredPos = useOriginal ? change.originalAnchoredPosition : change.anchoredPosition;
                    Vector3 baseAnchoredPos3D = useOriginal ? change.originalAnchoredPosition3D : change.anchoredPosition3D;
                    Vector2 baseAnchorMin = useOriginal ? change.originalAnchorMin : change.anchorMin;
                    Vector2 baseAnchorMax = useOriginal ? change.originalAnchorMax : change.anchorMax;
                    Vector2 basePivot = useOriginal ? change.originalPivot : change.pivot;
                    Vector2 baseSizeDelta = useOriginal ? change.originalSizeDelta : change.sizeDelta;
                    Vector2 baseOffsetMin = useOriginal ? change.originalOffsetMin : change.offsetMin;
                    Vector2 baseOffsetMax = useOriginal ? change.originalOffsetMax : change.offsetMax;

                    if (change.isRectTransform && liveComponent is RectTransform)
                    {
                        snapshotComponent = snapshotGO.AddComponent<RectTransform>();
                    }
                    else
                    {
                        snapshotComponent = snapshotGO.transform;
                    }

                    if (snapshotComponent is RectTransform snapshotRT)
                    {
                        snapshotRT.anchoredPosition = baseAnchoredPos;
                        snapshotRT.anchoredPosition3D = baseAnchoredPos3D;
                        snapshotRT.anchorMin = baseAnchorMin;
                        snapshotRT.anchorMax = baseAnchorMax;
                        snapshotRT.pivot = basePivot;
                        snapshotRT.sizeDelta = baseSizeDelta;
                        snapshotRT.offsetMin = baseOffsetMin;
                        snapshotRT.offsetMax = baseOffsetMax;
                    }

                    snapshotComponent.transform.localPosition = basePos;
                    snapshotComponent.transform.localRotation = baseRot;
                    snapshotComponent.transform.localScale = baseScale;

                    SerializedObject so = new SerializedObject(snapshotComponent);
                    so.Update();

                }
                else if (Application.isPlaying)
                {
                    // No matching TransformChange entry in store; use stored snapshot as original state

                    var originalSnapshot = ChangesTracker.GetSnapshot(go);

                    if (originalSnapshot != null)
                    {

                        if (originalSnapshot.isRectTransform && liveComponent is RectTransform)
                        {
                            // AddComponent<RectTransform> replaces default Transform automatically

                            snapshotComponent = snapshotGO.AddComponent<RectTransform>();
                        }
                        else
                        {
                            snapshotComponent = snapshotGO.transform;
                        }

                        // Apply snapshot values to component

                        if (snapshotComponent is RectTransform snapshotRT)
                        {
                            snapshotRT.anchoredPosition = originalSnapshot.anchoredPosition;
                            snapshotRT.anchoredPosition3D = originalSnapshot.anchoredPosition3D;
                            snapshotRT.anchorMin = originalSnapshot.anchorMin;
                            snapshotRT.anchorMax = originalSnapshot.anchorMax;
                            snapshotRT.pivot = originalSnapshot.pivot;
                            snapshotRT.sizeDelta = originalSnapshot.sizeDelta;
                            snapshotRT.offsetMin = originalSnapshot.offsetMin;
                            snapshotRT.offsetMax = originalSnapshot.offsetMax;
                        }

                        snapshotComponent.transform.localPosition = originalSnapshot.position;
                        snapshotComponent.transform.localRotation = originalSnapshot.rotation;
                        snapshotComponent.transform.localScale = originalSnapshot.scale;

                        SerializedObject so = new SerializedObject(snapshotComponent);
                        so.Update();
                    }
                }
            }
            else
            {
                // Snapshot creation for non-transform components based on stored data

                var type = liveComponent.GetType();
                snapshotComponent = snapshotGO.AddComponent(type);

                if (Application.isPlaying)
                {
                    // Play mode lookup, search ComponentChangesStore for accepted overrides

                    ComponentChangesStore.ComponentChange match = null;
                    var compStore = ComponentChangesStore.LoadExisting();

                    if (compStore != null)
                    {
                        string scenePath = go.scene.path;
                        if (string.IsNullOrEmpty(scenePath))
                            scenePath = go.scene.name;

                        string objectPath = GetGameObjectPathForPopup(go.transform);
                        string componentType = type.AssemblyQualifiedName;
                        var allOfType = go.GetComponents(type);
                        int index = System.Array.IndexOf(allOfType, liveComponent);

                        foreach (var c in compStore.changes)
                        {
                            if (c.scenePath == scenePath &&
                                c.objectPath == objectPath &&
                                c.componentType == componentType &&
                                c.componentIndex == index)
                            {
                                match = c;
                                break;
                            }
                        }
                    }

                    if (match != null)
                    {
                        SerializedObject so = new SerializedObject(snapshotComponent);

                        var baseValues = (match is { hasOriginalValues: true, originalSerializedValues: not null } &&
                                          match.originalSerializedValues.Count == match.propertyPaths.Count)
                            ? match.originalSerializedValues
                            : match.serializedValues;

                        var baseTypes = (match is { hasOriginalValues: true, originalValueTypes: not null } &&
                                         match.originalValueTypes.Count == match.propertyPaths.Count)
                            ? match.originalValueTypes
                            : match.valueTypes;

                        for (int i = 0; i < match.propertyPaths.Count; i++)
                        {
                            string path = match.propertyPaths[i];
                            SerializedProperty prop = so.FindProperty(path);
                            if (prop == null)
                                continue;

                            string typeName = (i < baseTypes.Count) ? baseTypes[i] : string.Empty;
                            string value = (i < baseValues.Count) ? baseValues[i] : string.Empty;

                            try
                            {
                                ApplySerializedComponentValueForPopup(prop, typeName, value);
                            }
                            catch
                            {
                                // ignored
                            }
                        }

                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    else
                    {
                        // Fallback: use ComponentSnapshot when no store entry

                        string compKey = ChangesTracker.GetComponentKey(liveComponent);
                        var snapshot = ChangesTracker.GetComponentSnapshot(go, compKey);

                        if (snapshot != null)
                        {
                            SerializedObject so = new SerializedObject(snapshotComponent);

                            foreach (var kvp in snapshot.properties)
                            {
                                SerializedProperty prop = so.FindProperty(kvp.Key);
                                if (prop != null)
                                {
                                    try
                                    {
                                        SetPropertyValue(prop, kvp.Value);
                                    }
                                    catch
                                    {
                                        // ignored
                                    }
                                }
                            }

                            so.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                }
                else
                {
                    // Edit mode (e.g. browser): read original values from ComponentChangesStore

                    var compStore = ComponentChangesStore.LoadExisting();
                    ComponentChangesStore.ComponentChange match = null;

                    if (compStore != null)
                    {
                        string scenePath = go.scene.path;
                        if (string.IsNullOrEmpty(scenePath))
                            scenePath = go.scene.name;

                        string objectPath = GetGameObjectPathForPopup(go.transform);
                        string componentType = type.AssemblyQualifiedName;
                        var allOfType = go.GetComponents(type);
                        int index = System.Array.IndexOf(allOfType, liveComponent);

                        foreach (var c in compStore.changes)
                        {
                            if (c.scenePath == scenePath &&
                                c.objectPath == objectPath &&
                                c.componentType == componentType &&
                                c.componentIndex == index)
                            {
                                match = c;
                                break;
                            }
                        }
                    }

                    if (match != null)
                    {
                        SerializedObject so = new SerializedObject(snapshotComponent);

                        var baseValues = (match is { hasOriginalValues: true, originalSerializedValues: not null } &&
                                          match.originalSerializedValues.Count == match.propertyPaths.Count)
                            ? match.originalSerializedValues
                            : match.serializedValues;

                        var baseTypes = (match is { hasOriginalValues: true, originalValueTypes: not null } &&
                                         match.originalValueTypes.Count == match.propertyPaths.Count)
                            ? match.originalValueTypes
                            : match.valueTypes;

                        for (int i = 0; i < match.propertyPaths.Count; i++)
                        {
                            string path = match.propertyPaths[i];
                            SerializedProperty prop = so.FindProperty(path);
                            if (prop == null)
                                continue;

                            string typeName = (i < baseTypes.Count) ? baseTypes[i] : string.Empty;
                            string value = (i < baseValues.Count) ? baseValues[i] : string.Empty;

                            try
                            {
                                ApplySerializedComponentValueForPopup(prop, typeName, value);
                            }
                            catch
                            {
                                // ignored
                            }
                        }

                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }

            if (snapshotComponent)
            {
                leftEditor = UnityEditor.Editor.CreateEditor(snapshotComponent);
                rightEditor = UnityEditor.Editor.CreateEditor(liveComponent);
            }
            else
            {
                Debug.LogWarning($"[TransformDebug][ComparePopup.EditorsCreated] snapshotComponent is NULL for GO='{go.name}', liveComponent='{liveComponent.GetType().Name}'. Editors will not be created.");
            }
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

        private void ApplySerializedComponentValueForPopup(SerializedProperty prop, string typeName, string value)
        {
            switch (typeName)
            {
                case "Integer":
                    if (int.TryParse(value, out var iVal)) prop.intValue = iVal;
                    break;
                case "Boolean":
                    if (bool.TryParse(value, out var bVal)) prop.boolValue = bVal;
                    break;
                case "Float":
                    if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fVal)) prop.floatValue = fVal;
                    break;
                case "String":
                    prop.stringValue = value;
                    break;
                case "Color":
                    if (ColorUtility.TryParseHtmlString(value, out var col)) prop.colorValue = col;
                    break;
                case "Vector2":
                    prop.vector2Value = DeserializeVector2ForPopup(value);
                    break;
                case "Vector3":
                    prop.vector3Value = DeserializeVector3ForPopup(value);
                    break;
                case "Vector4":
                    prop.vector4Value = DeserializeVector4ForPopup(value);
                    break;
                case "Quaternion":
                    prop.quaternionValue = DeserializeQuaternionForPopup(value);
                    break;
                case "Enum":
                    if (int.TryParse(value, out var eVal)) prop.enumValueIndex = eVal;
                    break;
            }
        }

        private Vector2 DeserializeVector2ForPopup(string s)
        {
            var parts = s.Split(',');
            if (parts.Length != 2) return Vector2.zero;
            float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
            float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y);
            return new Vector2(x, y);
        }

        private Vector3 DeserializeVector3ForPopup(string s)
        {
            var parts = s.Split(',');
            if (parts.Length != 3) return Vector3.zero;
            float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
            float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y);
            float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z);
            return new Vector3(x, y, z);
        }

        private Vector4 DeserializeVector4ForPopup(string s)
        {
            var parts = s.Split(',');
            if (parts.Length != 4) return Vector4.zero;
            float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
            float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y);
            float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z);
            float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w);
            return new Vector4(x, y, z, w);
        }

        private Quaternion DeserializeQuaternionForPopup(string s)
        {
            var parts = s.Split(',');
            if (parts.Length != 4) return Quaternion.identity;
            float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
            float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y);
            float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z);
            float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w);
            return new Quaternion(x, y, z, w);
        }

        private float EstimateInspectorHeight()
        {
            if (rightEditor == null || rightEditor.target == null)
                return 300f;

            SerializedObject so = new SerializedObject(rightEditor.target);
            SerializedProperty prop = so.GetIterator();

            int lineCount = 0;
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.name == "m_Script")
                    continue;
                lineCount++;
            }

            float lineHeight = EditorGUIUtility.singleLineHeight + 4f;
            float estimated = lineCount * lineHeight + HeaderHeight + 20f;
            return Mathf.Clamp(estimated, 200f, 800f);
        }

        public override Vector2 GetWindowSize()
{
    // Startet bei Min, wächst dann dynamisch bis Max
    float h = targetWindowHeight < 0 ? MinWindowHeight : targetWindowHeight;
    return new Vector2(MinWidth * 2 + 6, h);
}

public override void OnGUI(Rect rect)
{
    if (leftEditor == null || rightEditor == null) return;

    // 0. DRAG-HANDLING
    HandleDragAndDrop(rect);

    // 1. DYNAMISCHE GRÖSSEN-ANPASSUNG
    // Wir messen ständig, wie viel Platz der Inhalt UNTER dem aktuellen Fenster bräuchte
    float extraSpaceNeeded = Mathf.Max(leftMaxScroll, rightMaxScroll);
    
    if (Event.current.type == EventType.Layout)
    {
        // Zielhöhe = Aktuelle Höhe + das was unten abgeschnitten ist
        float desiredHeight = Mathf.Clamp(rect.height + extraSpaceNeeded, MinWindowHeight, MaxWindowHeight);
        
        // Nur anpassen, wenn die Abweichung relevant ist (> 1 Pixel)
        if (Mathf.Abs(targetWindowHeight - desiredHeight) > 1f)
        {
            targetWindowHeight = desiredHeight;
            // Das erzwingt, dass Unity GetWindowSize() neu aufruft und das Popup resizet
            editorWindow.ShowAsDropDown(new Rect(editorWindow.position.position, Vector2.zero), GetWindowSize());
        }
    }

    // 2. SCROLL-LOGIK
    // Scrollen ist nur aktiv, wenn wir am Max-Limit (400) angekommen sind und immer noch Platz brauchen
    bool needsScrolling = (rect.height >= MaxWindowHeight - 1f) && extraSpaceNeeded > 0.5f;
    HandleMouseWheel(rect, needsScrolling);

    // 3. LAYOUT & ZEICHNEN
    float scrollbarWidth = needsScrolling ? 15f : 0f;
    float columnWidth = (rect.width - scrollbarWidth - 6) * 0.5f;
    float contentHeight = rect.height - FooterHeight - HeaderHeight;

    DrawColumnHeader(new Rect(rect.x, rect.y, columnWidth, HeaderHeight), leftEditor.target, "Original");
    DrawColumnHeader(new Rect(rect.x + columnWidth + 6, rect.y, columnWidth, HeaderHeight), rightEditor.target, "Play Mode");

    Rect contentRect = new Rect(rect.x, rect.y + HeaderHeight, rect.width, contentHeight);
    GUILayout.BeginArea(contentRect);
    GUILayout.BeginHorizontal();

    // Wir geben den Editoren den vollen Platz, damit sie ihre echte Höhe berechnen können
    DrawSynchronizedColumn(columnWidth, contentHeight, leftEditor, scrollNormalized, ref leftMaxScroll, false);
    DrawSeparator(new Rect(columnWidth, 0, 2, contentHeight));
    DrawSynchronizedColumn(columnWidth, contentHeight, rightEditor, scrollNormalized, ref rightMaxScroll, true);

    if (needsScrolling)
    {
        Rect scrollbarRect = new Rect(rect.width - 15, 0, 15, contentHeight);
        scrollNormalized = GUI.VerticalScrollbar(scrollbarRect, scrollNormalized, 0.1f, 0f, 1.0f);
    }
    else
    {
        scrollNormalized = 0f; // Reset wenn alles reinpasst
    }

    GUILayout.EndHorizontal();
    GUILayout.EndArea();

    DrawFooter(new Rect(rect.x, rect.y + rect.height - FooterHeight, rect.width, FooterHeight));
}

private void HandleMouseWheel(Rect rect, bool needsScrolling)
{
    if (needsScrolling && rect.Contains(Event.current.mousePosition) && Event.current.type == EventType.ScrollWheel)
    {
        scrollNormalized = Mathf.Clamp01(scrollNormalized + Event.current.delta.y * 0.05f);
        Event.current.Use();
    }
}

        void DrawSynchronizedColumn(float width, float height, UnityEditor.Editor editor, float scrollValue, ref float maxScroll, bool editable)
        {
            // Bereich für diese Spalte
            Rect container = EditorGUILayout.BeginVertical(GUILayout.Width(width), GUILayout.Height(height));
            
            // Berechnung des tatsächlichen Offsets basierend auf der dynamischen Höhe
            float currentOffset = scrollValue * maxScroll;
            
            Vector2 scrollPos = new Vector2(0, currentOffset);
            
            // ScrollView ohne sichtbare Scrollbars (da wir die Logik steuern)
            scrollPos = GUILayout.BeginScrollView(scrollPos, GUIStyle.none, GUIStyle.none, GUILayout.Height(height));

            GUI.enabled = editable;
            
            // Inhalt zeichnen
            if (editor.target is Transform t)
                DrawTransformInspector(t, editable);
            else
                editor.OnInspectorGUI();

            // Hier erfassen wir die tatsächliche Höhe des Inhalts (wichtig für dynamische Listen)
            if (Event.current.type == EventType.Repaint)
            {
                Rect lastRect = GUILayoutUtility.GetLastRect();
                maxScroll = Mathf.Max(0, lastRect.yMax - height);
            }

            GUI.enabled = true;
            GUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        
 
        void DrawColumn(Rect columnRect, UnityEditor.Editor editor, ref Vector2 scroll, string title, bool editable)
        {
            // Column header area

            Rect headerRect = new Rect(columnRect.x, columnRect.y, columnRect.width, HeaderHeight);
            DrawColumnHeader(headerRect, editor.target, title);

            // Scrollable content area for inspector

            Rect contentRect = new Rect(
                columnRect.x,
                columnRect.y + HeaderHeight,
                columnRect.width,
                columnRect.height - HeaderHeight
            );

            GUI.BeginGroup(contentRect);

            Rect viewRect = new Rect(0, 0, contentRect.width - 16, contentRect.height);

            scroll = GUI.BeginScrollView(
                new Rect(0, 0, contentRect.width, contentRect.height),
                scroll,
                viewRect
            );

            GUI.enabled = editable;

            GUILayout.BeginArea(new Rect(4, 0, viewRect.width - 8, viewRect.height));
            // Special handling for Transform and RectTransform editors
            // Built-in TransformInspector shows no values in popup layout despite correct object data

            if (editor is { target: Transform transformTarget })
            {
                DrawTransformInspector(transformTarget, editable);
            }
            else
            {
                editor.OnInspectorGUI();
            }
            GUILayout.EndArea();

            GUI.enabled = true;

            GUI.EndScrollView();
            GUI.EndGroup();
        }

        void DrawTransformInspector(Transform t, bool editable)
        {
            if (t == null)
                return;

            EditorGUI.BeginDisabledGroup(!editable);

            EditorGUI.BeginChangeCheck();
            Vector3 pos = EditorGUILayout.Vector3Field("Position", t.localPosition);
            Vector3 rot = EditorGUILayout.Vector3Field("Rotation", t.localEulerAngles);
            Vector3 scale = EditorGUILayout.Vector3Field("Scale", t.localScale);

            if (EditorGUI.EndChangeCheck() && editable)
            {
                Undo.RecordObject(t, "Edit Transform");
                t.localPosition = pos;
                t.localEulerAngles = rot;
                t.localScale = scale;
                EditorUtility.SetDirty(t);
            }

            EditorGUI.EndDisabledGroup();
        }

        void DrawColumnHeader(Rect rect, Object target, string title)
        {
            if (Event.current.type == EventType.Repaint)
            {
                EditorStyles.toolbar.Draw(rect, false, false, false, false);
            }

            var content = EditorGUIUtility.ObjectContent(target, target.GetType());

            Rect iconRect = new Rect(rect.x + 4, rect.y + 4, 16, 16);
            if (content.image != null)
            {
                GUI.DrawTexture(iconRect, content.image);
            }

            Rect labelRect = new Rect(rect.x + 24, rect.y + 4, rect.width - 28, 16);
            EditorGUI.LabelField(labelRect, $"{content.text} ({title})", EditorStyles.boldLabel);
        }

        void DrawSeparator(Rect rect)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Color separatorColor = EditorGUIUtility.isProSkin
                    ? new Color(0.15f, 0.15f, 0.15f)
                    : new Color(0.6f, 0.6f, 0.6f);
                EditorGUI.DrawRect(rect, separatorColor);
            } 
        }

        void DrawFooter(Rect rect)
        {
            GUILayout.BeginArea(rect);

            GUILayout.Space(2);
            GUILayout.BeginHorizontal();

            // INFO-BEREICH LINKS (60 % BREITE), nur im Play Mode mit Änderungen
            if (Application.isPlaying && HasUnsavedChangesComparedToSnapshot())
            {
                float infoWidth = rect.width * 0.6f;
                GUILayout.BeginVertical(GUILayout.Width(infoWidth));
                EditorGUILayout.HelpBox("Current Play Mode changes differ from the stored overrides and have not been applied yet.", MessageType.Info);
                GUILayout.EndVertical();
            }
            else
            {
                GUILayout.Space(rect.width * 0.6f);
            }

            GUILayout.FlexibleSpace();

            // BUTTONS RECHTS
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Revert", GUILayout.Width(120f), GUILayout.Height(28f)))
            {
                RevertChanges();
                editorWindow.Close();
            }

            GUILayout.Space(8);

            if (GUILayout.Button("Apply", GUILayout.Width(120f), GUILayout.Height(28f)))
            {
                // Transform changes acceptance for this GameObject and persistence for later transition to Edit Mode.
                if (liveComponent is Transform or RectTransform)
                {
                    ChangesTracker.AcceptTransformChanges(liveComponent.gameObject);
                }
                else
                {
                    ChangesTracker.AcceptComponentChanges(liveComponent);
                }

                RefreshBrowserIfOpen();
                editorWindow.Close();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        bool HasUnsavedChangesComparedToSnapshot()
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

                if (PropertiesDiffer(prop, liveProp))
                    return true;
            }

            return false;
        }

        bool PropertiesDiffer(SerializedProperty a, SerializedProperty b)
        {
            switch (a.propertyType)
            {
                case SerializedPropertyType.Integer: return a.intValue != b.intValue;
                case SerializedPropertyType.Boolean: return a.boolValue != b.boolValue;
                case SerializedPropertyType.Float: return !Mathf.Approximately(a.floatValue, b.floatValue);
                case SerializedPropertyType.String: return a.stringValue != b.stringValue;
                case SerializedPropertyType.Color: return a.colorValue != b.colorValue;
                case SerializedPropertyType.Vector2: return a.vector2Value != b.vector2Value;
                case SerializedPropertyType.Vector3: return a.vector3Value != b.vector3Value;
                case SerializedPropertyType.Vector4: return a.vector4Value != b.vector4Value;
                case SerializedPropertyType.Quaternion: return a.quaternionValue != b.quaternionValue;
                case SerializedPropertyType.Enum: return a.enumValueIndex != b.enumValueIndex;
                default: return false;
            }
        }

        private static string GetGameObjectPathForPopup(Transform transform)
        {
            if (transform == null)
                return string.Empty;

            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        void RevertChanges()
        {
            if (snapshotComponent == null) return;

            // Copy of all values from snapshot to live component
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
                    ChangesTracker.ResetTransformBaseline(liveComponent.gameObject);
                }
                else
                {
                    ChangesTracker.ResetComponentBaseline(liveComponent);
                }
            }


            if (liveComponent != null)
            {
                var go = liveComponent.gameObject;
                string scenePath = go.scene.path;
                if (string.IsNullOrEmpty(scenePath))
                    scenePath = go.scene.name;

                string objectPath = GetGameObjectPathForPopup(go.transform);

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

            RefreshBrowserIfOpen();
        }

        private static void RefreshBrowserIfOpen()
        {
            if (EditorWindow.HasOpenInstances<OverridesBrowserWindow>())
            {
                OverridesBrowserWindow.Open();
            }
        }

        private void HandleDragAndDrop(Rect rect)
        {
            // Header-Bereich für Drag-Detection
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

            // Visual Feedback: Header-Bereich zeichnen
            if (Event.current.type == EventType.Repaint)
            {
                GUI.Box(dragHeaderRect, GUIContent.none, EditorStyles.toolbar);
            }
        }

        public override void OnClose()
        {
            if (leftEditor) Object.DestroyImmediate(leftEditor);
            if (rightEditor) Object.DestroyImmediate(rightEditor);
            if (snapshotGO) Object.DestroyImmediate(snapshotGO);
        }
    }
}