
using UnityEditor;
using UnityEngine;



internal class PlayModeOverrideComparePopup : PopupWindowContent
{
    private readonly Component liveComponent;
    private GameObject snapshotGO;
    private Component snapshotComponent;
    private Editor leftEditor;
    private Editor rightEditor;
    private Vector2 leftScroll;
    private Vector2 rightScroll;
    private const float MinWidth = 350f;
    private const float HeaderHeight = 24f;
    private const float FooterHeight = 40f;

    public PlayModeOverrideComparePopup(Component component)
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
            // Transform-Vergleich: Im Play Mode basieren wir auf den Snapshots im
            // PlayModeChangesTracker. Im Edit Mode (z.B. wenn der Browser geöffnet ist)
            // verwenden wir die im ScriptableObject gespeicherten Originalwerte.

            if (Application.isPlaying)
            {
                // Play Mode: wie bisher über die gespeicherten Snapshots arbeiten.
                var originalSnapshot = PlayModeChangesTracker.GetSnapshot(go);

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

                    var posProp = so.FindProperty("m_LocalPosition");
                    var rotProp = so.FindProperty("m_LocalRotation");
                    var scaleProp = so.FindProperty("m_LocalScale");
                    if (posProp != null && scaleProp != null)
                    {
                        Debug.Log($"[TransformDebug][ComparePopup.Serialized] Serialized snapshot for GO='{snapshotGO.name}': pos={posProp.vector3Value}, scale={scaleProp.vector3Value}");
                    }
                }
                else
                {
                    Debug.Log($"[TransformDebug][ComparePopup.Create] Original snapshot MISSING for GO='{go.name}' (Transform, Play Mode)");
                }
            }
            else
            {
                // Edit Mode (z.B. Browser): Originalwerte aus dem persistenten Store holen.
                var store = PlayModeTransformChangesStore.LoadExisting();
                PlayModeTransformChangesStore.TransformChange match = null;

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
                            match = c;
                            break;
                        }
                    }
                }

                if (match != null)
                {
                    var change = match;

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

                    Debug.Log($"[TransformDebug][ComparePopup.Create] EditMode baseline from store for GO='{go.name}', useOriginal={useOriginal}, pos={basePos}, rot={baseRot.eulerAngles}, scale={baseScale}");
                }
                else
                {
                    Debug.Log($"[TransformDebug][ComparePopup.Create] No TransformChange in store for GO='{go.name}' (Edit Mode)");
                }
            }
        }
        else
        {
            // For other components, create snapshot from stored data

            var type = liveComponent.GetType();
            snapshotComponent = snapshotGO.AddComponent(type);

            // Restore values from snapshot
            string compKey = PlayModeChangesTracker.GetComponentKey(liveComponent);
            var snapshot = PlayModeChangesTracker.GetComponentSnapshot(go, compKey);

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
                        catch { }
                    }
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        if (snapshotComponent != null)
        {
            var leftTransform = snapshotComponent.transform;
            var rightTransform = (liveComponent as Component)?.transform;

            Debug.Log($"[TransformDebug][ComparePopup.EditorsCreated] snapshotComponentType={snapshotComponent.GetType().Name}, liveComponentType={liveComponent.GetType().Name}, leftPos={leftTransform.localPosition}, leftRot={leftTransform.localRotation.eulerAngles}, leftScale={leftTransform.localScale}, rightPos={(rightTransform != null ? rightTransform.localPosition.ToString() : "n/a")}, rightRot={(rightTransform != null ? rightTransform.localRotation.eulerAngles.ToString() : "n/a")}, rightScale={(rightTransform != null ? rightTransform.localScale.ToString() : "n/a")}");

            leftEditor = Editor.CreateEditor(snapshotComponent);
            rightEditor = Editor.CreateEditor(liveComponent);
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

    public override Vector2 GetWindowSize()
    {
        return new Vector2(MinWidth * 2 + 6, 500 + FooterHeight);
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

        DrawColumn(leftColumn, leftEditor, ref leftScroll, "Original", false);
        DrawSeparator(new Rect(rect.x + columnWidth, rect.y, 6, contentHeight));
        DrawColumn(rightColumn, rightEditor, ref rightScroll, "Play Mode", true);

        DrawFooter(footerRect);
    }

    void DrawColumn(Rect columnRect, Editor editor, ref Vector2 scroll, string title, bool editable)
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

        Rect viewRect = new Rect(0, 0, contentRect.width - 16, 2000);
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

    void DrawColumnHeader(Rect rect, UnityEngine.Object target, string title)
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
                PlayModeChangesTracker.AcceptTransformChanges(liveComponent.gameObject);
            }
            else
            {
                PlayModeChangesTracker.AcceptComponentChanges(liveComponent);
            }
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
        Debug.Log($"[TransformDebug][ComparePopup.Revert] Reverted {liveComponent.GetType().Name} to original values");
    }

    public override void OnClose()
    {
        if (leftEditor) UnityEngine.Object.DestroyImmediate(leftEditor);
        if (rightEditor) UnityEngine.Object.DestroyImmediate(rightEditor);
        if (snapshotGO) UnityEngine.Object.DestroyImmediate(snapshotGO);
    }
}