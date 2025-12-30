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
        private Vector2 leftScroll;
        private Vector2 rightScroll;

        private const float MinWidth = 350f;
        private const float HeaderHeight = 24f;
        private const float FooterHeight = 40f;
        private float cachedHeight = -1f;
        private bool isDragging;
        private Vector2 dragStartMouse;
        private Rect dragStartWindow;

        public OverrideComparePopup(Component component)
        {
            liveComponent = component;
            CreateSnapshotAndEditors();
        }

        void CreateSnapshotAndEditors()
        {
            var go = liveComponent.gameObject;
            Debug.Log($"[TransformDebug][ComparePopup.Create] LiveComponent='{liveComponent.GetType().Name}', GO='{go.name}'");
            snapshotGO = new GameObject("SnapshotTransform");
            snapshotGO.hideFlags = HideFlags.HideAndDontSave;

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

                    Debug.Log($"[TransformDebug][ComparePopup.Create] Baseline from TransformStore for GO='{go.name}', useOriginal={useOriginal}, pos={basePos}, rot={baseRot.eulerAngles}, scale={baseScale}");
                }
                else if (Application.isPlaying)
                {
                    // Kein Store-Eintrag vorhanden: Im Play Mode wie bisher über die
                    // gespeicherten Snapshots arbeiten (Originalzustand vor den Änderungen).
                    var originalSnapshot = ChangesTracker.GetSnapshot(go);

                    if (originalSnapshot != null)
                    {
                        Debug.Log($"[TransformDebug][ComparePopup.Create] Original snapshot FOUND for GO='{go.name}'. isRect={originalSnapshot.isRectTransform}, pos={originalSnapshot.position}, rot={originalSnapshot.rotation.eulerAngles}, scale={originalSnapshot.scale}");

                        if (originalSnapshot.isRectTransform && liveComponent is RectTransform)
                        {
                            // AddComponent<RectTransform> ersetzt das normale Transform automatisch
                            snapshotComponent = snapshotGO.AddComponent<RectTransform>();
                        }
                        else
                        {
                            snapshotComponent = snapshotGO.transform;
                        }

                        // Werte vom Snapshot auf das Objekt übertragen
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

                            Debug.Log($"[TransformDebug][ComparePopup.Create] Applied RectTransform data to snapshot GO='{snapshotGO.name}': anchoredPos={snapshotRT.anchoredPosition}, sizeDelta={snapshotRT.sizeDelta}, anchorMin={snapshotRT.anchorMin}, anchorMax={snapshotRT.anchorMax}");
                        }

                        snapshotComponent.transform.localPosition = originalSnapshot.position;
                        snapshotComponent.transform.localRotation = originalSnapshot.rotation;
                        snapshotComponent.transform.localScale = originalSnapshot.scale;

                        var t = snapshotComponent.transform;
                        Debug.Log($"[TransformDebug][ComparePopup.Create] Applied basic Transform data to snapshot GO='{snapshotGO.name}': pos={t.localPosition}, rot={t.localRotation.eulerAngles}, scale={t.localScale}");

                        SerializedObject so = new SerializedObject(snapshotComponent);
                        so.Update();

                        
                    }
                    else
                    {
                        Debug.Log($"[TransformDebug][ComparePopup.Create] Original snapshot MISSING for GO='{go.name}' (Transform, Play Mode)");
                    }
                }
                else
                {
                    Debug.Log($"[TransformDebug][ComparePopup.Create] No TransformChange in store for GO='{go.name}' (Edit Mode, no baseline available)");
                }
            }
            else
            {
                // For other components, create snapshot from stored data

                var type = liveComponent.GetType();
                snapshotComponent = snapshotGO.AddComponent(type);

                if (Application.isPlaying)
                {
                    // Play Mode: zunächst versuchen wir – analog zu Transforms – einen passenden
                    // Eintrag im Component-Store zu finden (für bereits akzeptierte Overrides).
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

                        var baseValues = (match.hasOriginalValues &&
                                          match.originalSerializedValues != null &&
                                          match.originalSerializedValues.Count == match.propertyPaths.Count)
                            ? match.originalSerializedValues
                            : match.serializedValues;

                        var baseTypes = (match.hasOriginalValues &&
                                         match.originalValueTypes != null &&
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
                        // Fallback: wie bisher über den ComponentSnapshot arbeiten.
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
                    // Edit Mode (z.B. Browser): Originalwerte aus dem Component-Store holen.
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

                        // Wenn Originalwerte vorhanden und passend dimensioniert sind, diese verwenden,
                        // andernfalls auf die aktuell persistierten Werte zurückfallen.
                        var baseValues = (match.hasOriginalValues &&
                                          match.originalSerializedValues != null &&
                                          match.originalSerializedValues.Count == match.propertyPaths.Count)
                            ? match.originalSerializedValues
                            : match.serializedValues;

                        var baseTypes = (match.hasOriginalValues &&
                                         match.originalValueTypes != null &&
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

            if (snapshotComponent != null)
            {
                var leftTransform = snapshotComponent.transform;
                var rightTransform = liveComponent?.transform;

                Debug.Log($"[TransformDebug][ComparePopup.EditorsCreated] snapshotComponentType={snapshotComponent.GetType().Name}, liveComponentType={liveComponent.GetType().Name}, leftPos={leftTransform.localPosition}, leftRot={leftTransform.localRotation.eulerAngles}, leftScale={leftTransform.localScale}, rightPos={(rightTransform != null ? rightTransform.localPosition.ToString() : "n/a")}, rightRot={(rightTransform != null ? rightTransform.localRotation.eulerAngles.ToString() : "n/a")}, rightScale={(rightTransform != null ? rightTransform.localScale.ToString() : "n/a")}");

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
                    Color col; if (ColorUtility.TryParseHtmlString(value, out col)) prop.colorValue = col;
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
            if (cachedHeight < 0f)
            {
                cachedHeight = EstimateInspectorHeight();
            }

            return new Vector2(MinWidth * 2 + 6, cachedHeight + FooterHeight);
        }

        public override void OnGUI(Rect rect)
        {
            if (leftEditor == null || rightEditor == null)
            {
                EditorGUILayout.HelpBox("Failed to create editors", MessageType.Error);
                return;
            }

            float columnWidth = (rect.width - 6) * 0.5f;
            float contentHeight = rect.height - FooterHeight;

            Rect leftColumn = new Rect(rect.x, rect.y, columnWidth, contentHeight);
            Rect rightColumn = new Rect(rect.x + columnWidth + 6, rect.y, columnWidth, contentHeight);
            Rect footerRect = new Rect(rect.x, rect.y + contentHeight, rect.width, FooterHeight);

            HandleWindowDragging(rect);

            DrawColumn(leftColumn, leftEditor, ref leftScroll, "Original", false);
            DrawSeparator(new Rect(rect.x + columnWidth, rect.y, 6, contentHeight));
            DrawColumn(rightColumn, rightEditor, ref rightScroll, "Play Mode", true);

            DrawFooter(footerRect);
        }

        void HandleWindowDragging(Rect windowRect)
        {
            if (editorWindow == null)
                return;

            Rect dragRect = new Rect(windowRect.x, windowRect.y, windowRect.width, HeaderHeight);
            Event e = Event.current;

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0 && dragRect.Contains(e.mousePosition))
                    {
                        isDragging = true;
                        dragStartMouse = GUIUtility.GUIToScreenPoint(e.mousePosition);
                        dragStartWindow = editorWindow.position;
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (isDragging)
                    {
                        Vector2 current = GUIUtility.GUIToScreenPoint(e.mousePosition);
                        Vector2 delta = current - dragStartMouse;
                        Rect pos = dragStartWindow;
                        pos.position += delta;
                        editorWindow.position = pos;
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (isDragging && e.button == 0)
                    {
                        isDragging = false;
                        e.Use();
                    }
                    break;
            }
        }

        void DrawColumn(Rect columnRect, UnityEditor.Editor editor, ref Vector2 scroll, string title, bool editable)
        {
            // Header
            Rect headerRect = new Rect(columnRect.x, columnRect.y, columnRect.width, HeaderHeight);
            DrawColumnHeader(headerRect, editor.target, title);

            // Content area with scroll
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
            // Spezialbehandlung für Transform/RectTransform, da der eingebaute TransformInspector
            // im Popup-Layout keine Werte anzeigt, obwohl sie korrekt im Objekt vorhanden sind.
            if (editor != null && editor.target is Transform transformTarget)
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
            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Revert", GUILayout.Width(120f), GUILayout.Height(28f)))
            {
                RevertChanges();
                editorWindow.Close();
            }

            GUILayout.Space(8);

            if (GUILayout.Button("Apply", GUILayout.Width(120f), GUILayout.Height(28f)))
            {
                // Transform-Änderungen für dieses GameObject annehmen und für
                // den späteren Übergang in den Edit Mode persistieren.
                if (liveComponent is Transform || liveComponent is RectTransform)
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

            GUILayout.Space(8);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.EndArea();
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

            // Copy all values from snapshot to live component
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

            // Im Play Mode zusätzlich die Baseline im PlayModeChangesTracker
            // für dieses Objekt/diese Komponente aktualisieren, damit
            // GetChangedComponents sie nicht weiter als "geändert" meldet.
            if (Application.isPlaying && liveComponent != null)
            {
                if (liveComponent is Transform || liveComponent is RectTransform)
                {
                    ChangesTracker.ResetTransformBaseline(liveComponent.gameObject);
                }
                else
                {
                    ChangesTracker.ResetComponentBaseline(liveComponent);
                }
            }

            Debug.Log($"[TransformDebug][ComparePopup.Revert] Reverted {liveComponent.GetType().Name} to original values");

            if (liveComponent != null)
            {
                var go = liveComponent.gameObject;
                string scenePath = go.scene.path;
                if (string.IsNullOrEmpty(scenePath))
                    scenePath = go.scene.name;

                string objectPath = GetGameObjectPathForPopup(go.transform);

                if (liveComponent is Transform || liveComponent is RectTransform)
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
                            Debug.Log($"[TransformDebug][ComparePopup.Revert] Removed Transform entry from store for GO='{go.name}'");
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
                            Debug.Log($"[TransformDebug][ComparePopup.Revert] Removed Component entry from store for GO='{go.name}', comp='{liveComponent.GetType().Name}'");
                        }
                    }
                }
            }

            // Nach einem erfolgreichen Revert den Browser aktualisieren, falls er geöffnet ist.
            RefreshBrowserIfOpen();
        }

        private static void RefreshBrowserIfOpen()
        {
            if (EditorWindow.HasOpenInstances<OverridesBrowserWindow>())
            {
                OverridesBrowserWindow.Open();
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