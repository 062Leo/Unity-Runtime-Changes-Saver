using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class DualInspectorWindow : EditorWindow
{
    private GameObject targetGO;
    private GameObject snapshotGO;

    private readonly List<Editor> leftEditors = new();
    private readonly List<Editor> rightEditors = new();

    private Vector2 sharedScroll;

    private const float ToolbarHeight = 20f;
    private const float Splitter = 6f;
    private const float ScrollbarWidth = 16f;

    [MenuItem("Tools/Dual Inspector")]
    static void Open()
    {
        GetWindow<DualInspectorWindow>("Dual Inspector");
    }

    void OnEnable()
    {
        Selection.selectionChanged += Rebuild;
        Rebuild();
    }

    void OnDisable()
    {
        Selection.selectionChanged -= Rebuild;
        Cleanup();
    }

    void Cleanup()
    {
        foreach (var e in leftEditors) DestroyImmediate(e);
        foreach (var e in rightEditors) DestroyImmediate(e);
        leftEditors.Clear();
        rightEditors.Clear();

        if (snapshotGO != null)
            DestroyImmediate(snapshotGO);
    }

    void Rebuild()
    {
        Cleanup();

        targetGO = Selection.activeGameObject;
        if (targetGO == null)
            return;

        snapshotGO = Instantiate(targetGO);
        snapshotGO.hideFlags = HideFlags.HideAndDontSave;

        BuildEditors(snapshotGO, leftEditors);
        BuildEditors(targetGO, rightEditors);

        sharedScroll = Vector2.zero;
        Repaint();
    }

    void BuildEditors(GameObject go, List<Editor> list)
    {
        foreach (var c in go.GetComponents<Component>())
        {
            if (c == null) continue;
            list.Add(Editor.CreateEditor(c));
        }
    }

    void OnGUI()
    {
        if (targetGO == null)
        {
            EditorGUILayout.LabelField("No GameObject selected.");
            return;
        }

        DrawToolbar();

        Rect contentRect = new Rect(
            0,
            ToolbarHeight,
            position.width,
            position.height - ToolbarHeight
        );

        float columnWidth = (contentRect.width - Splitter) * 0.5f;

        Rect leftRect = new Rect(
            contentRect.x,
            contentRect.y,
            columnWidth,
            contentRect.height
        );

        Rect rightRect = new Rect(
            contentRect.x + columnWidth + Splitter,
            contentRect.y,
            columnWidth,
            contentRect.height
        );

        DrawInspectorColumn(leftRect, leftEditors, editable: false);
        DrawInspectorColumn(rightRect, rightEditors, editable: true);
    }

    void DrawToolbar()
    {
        Rect rect = new Rect(0, 0, position.width, ToolbarHeight);
        GUILayout.BeginArea(rect, EditorStyles.toolbar);

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Revert", EditorStyles.toolbarButton))
        {
            Undo.RecordObject(targetGO, "Revert Changes");
            EditorUtility.CopySerialized(snapshotGO, targetGO);
            Rebuild();
        }

        if (GUILayout.Button("Apply", EditorStyles.toolbarButton))
        {
            Rebuild(); // Snapshot neu setzen
        }

        GUILayout.EndArea();
    }

    void DrawInspectorColumn(
        Rect rect,
        List<Editor> editors,
        bool editable
    )
    {
        GUI.Box(rect, GUIContent.none);

        Rect viewRect = new Rect(
            0,
            0,
            rect.width - ScrollbarWidth,
            CalculateViewHeight(editors)
        );

        sharedScroll = GUI.BeginScrollView(rect, sharedScroll, viewRect);

        GUI.enabled = editable;
        GUILayout.BeginArea(viewRect);

        foreach (var ed in editors)
        {
            if (ed == null) continue;

            DrawHeader(ed.target);
            ed.OnInspectorGUI();
            GUILayout.Space(8);
        }

        GUILayout.EndArea();
        GUI.enabled = true;

        GUI.EndScrollView();
    }

    float CalculateViewHeight(List<Editor> editors)
    {
        // ausreichend hoch, damit ScrollView korrekt funktioniert
        return Mathf.Max(1000, editors.Count * 120);
    }

    void DrawHeader(Object target)
    {
        var content = EditorGUIUtility.ObjectContent(target, target.GetType());
        Rect rect = GUILayoutUtility.GetRect(16, 22, GUILayout.ExpandWidth(true));

        GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

        if (content.image)
            GUI.DrawTexture(
                new Rect(rect.x + 6, rect.y + 3, 16, 16),
                content.image
            );

        EditorGUI.LabelField(
            new Rect(rect.x + 26, rect.y + 3, rect.width, 16),
            content.text,
            EditorStyles.boldLabel
        );
    }
}
