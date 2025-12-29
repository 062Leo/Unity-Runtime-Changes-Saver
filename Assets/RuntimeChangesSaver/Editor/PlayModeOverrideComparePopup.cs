
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
        snapshotGO = new GameObject("SnapshotTransform");
        snapshotGO.hideFlags = HideFlags.HideAndDontSave;

        if (liveComponent is Transform)
        {
            // 1. Vorbereitung der richtigen Komponente (RectTransform vs Transform)
            var originalSnapshot = PlayModeChangesTracker.GetSnapshot(go);

            if (originalSnapshot != null)
            {
                if (originalSnapshot.isRectTransform && liveComponent is RectTransform)
                {
                    // AddComponent<RectTransform> ersetzt das normale Transform automatisch
                    snapshotComponent = snapshotGO.AddComponent<RectTransform>();
                }
                else
                {
                    snapshotComponent = snapshotGO.transform;
                }

                // 2. Werte vom Snapshot auf das Objekt übertragen
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

                // Immer auch die Basis-Transform-Werte setzen
                snapshotComponent.transform.localPosition = originalSnapshot.position;
                snapshotComponent.transform.localRotation = originalSnapshot.rotation;
                snapshotComponent.transform.localScale = originalSnapshot.scale;

                // 3. SerializedObject synchronisieren (DAS FEHLTE)
                // Wenn du danach Editor.CreateEditor(snapshotComponent) aufrufst, 
                // sollte es funktionieren. Falls der Editor schon existiert:
                SerializedObject so = new SerializedObject(snapshotComponent);
                so.Update(); // Lädt die soeben gesetzten Werte in das SerializedObject
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
            Debug.Log($"[PlayModeOverrideComparePopup] Creating editors. snapshotComponent type={snapshotComponent.GetType().Name}, liveComponent type={liveComponent.GetType().Name}");
            leftEditor = Editor.CreateEditor(snapshotComponent);
            rightEditor = Editor.CreateEditor(liveComponent);
        }
        else
        {
            Debug.LogWarning($"[PlayModeOverrideComparePopup] snapshotComponent is NULL for GO='{go.name}', liveComponent='{liveComponent.GetType().Name}'. Editors will not be created.");
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
        editor.OnInspectorGUI();
        GUILayout.EndArea();

        GUI.enabled = true;

        GUI.EndScrollView();
        GUI.EndGroup();
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
            // Changes are already applied (we're in play mode editing live)
            Debug.Log($"Applied changes to {liveComponent.GetType().Name}");
            editorWindow.Close();
        }

        GUILayout.Space(8);
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.EndArea();
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
        Debug.Log($"Reverted {liveComponent.GetType().Name} to original values");
    }

    public override void OnClose()
    {
        if (leftEditor) UnityEngine.Object.DestroyImmediate(leftEditor);
        if (rightEditor) UnityEngine.Object.DestroyImmediate(rightEditor);
        if (snapshotGO) UnityEngine.Object.DestroyImmediate(snapshotGO);
    }
}