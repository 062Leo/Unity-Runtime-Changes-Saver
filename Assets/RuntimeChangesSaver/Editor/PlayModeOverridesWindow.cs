using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class PlayModeOverridesWindow : EditorWindow
{
    private GameObject _gameObject;
    private int _instanceId;
    private TransformSnapshot _original;
    private TransformSnapshot _current;
    private List<string> _changedProperties;

    private bool _showTransformComparison;

    public static void Open(GameObject gameObject, int instanceId, TransformSnapshot original, TransformSnapshot current, List<string> changedProperties)
    {
        var window = GetWindow<PlayModeOverridesWindow>("Play Mode Overrides");
        window._gameObject = gameObject;
        window._instanceId = instanceId;
        window._original = original;
        window._current = current;
        window._changedProperties = changedProperties;
        window.position = new Rect(200, 200, 360f, 420f);
        window.Show();
    }

    void OnGUI()
    {
        // Obere Hälfte: Overrides-Header + Liste
        DrawHeader();

        GUILayout.Space(4);
        GUILayout.Space(4);
        DrawDemoTransformRow();

        // Trennlinie zwischen oberem und unterem Bereich
        GUILayout.Space(4);
        Rect splitterRect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(splitterRect, new Color(0.2f, 0.2f, 0.2f, 0.8f));

        // Untere Hälfte: Transform-Vergleich (nur wenn angefordert und Daten vorhanden)
        if (_showTransformComparison && _gameObject != null && _original != null && _current != null && _changedProperties != null && _changedProperties.Count > 0)
        {
            GUILayout.Space(4);

            // Gleiche Darstellung wie an anderer Stelle: Before/After Transform
            PlayModeChangesInspector.DrawTransformBeforeAfter(_gameObject, _instanceId, _original, _current, _changedProperties);
        }

        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Revert All", GUILayout.Width(120f)))
        {
            // Frontend-only
        }

        if (GUILayout.Button("Apply All", GUILayout.Width(120f)))
        {
            // Frontend-only
        }

        GUILayout.EndHorizontal();
        GUILayout.Space(6);
    }

    void DrawHeader()
    {
        const float headerHeight = 60f;
        const float leftMargin = 6f;

        Rect headerRect = GUILayoutUtility.GetRect(20, 10000, headerHeight, headerHeight);

        Color bgColor = EditorGUIUtility.isProSkin
            ? new Color(0.5f, 0.5f, 0.5f, 0.2f)
            : new Color(0.9f, 0.9f, 0.9f, 0.6f);
        EditorGUI.DrawRect(headerRect, bgColor);

        GUIStyle boldRight = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleRight
        };

        GUIContent titleContent = new GUIContent("Review, Revert or Apply Overrides");
        float titleWidth = boldRight.CalcSize(titleContent).x;
        Rect titleRect = new Rect(headerRect.x + leftMargin, headerRect.y, titleWidth, headerRect.height);
        titleRect.height = EditorGUIUtility.singleLineHeight;
        GUI.Label(titleRect, titleContent, boldRight);

        float labelWidth = EditorStyles.label.CalcSize(new GUIContent("on")).x;

        Rect lineRect = headerRect;
        lineRect.height = EditorGUIUtility.singleLineHeight;
        lineRect.y += 20f;

        Rect labelRect = new Rect(headerRect.x + leftMargin, lineRect.y, labelWidth, lineRect.height);
        Rect contentRect = lineRect;
        contentRect.xMin = labelRect.xMax;

        GUI.Label(labelRect, "on", EditorStyles.label);
        GUI.Label(contentRect, _gameObject != null ? _gameObject.name : "<none>", EditorStyles.label);

        labelRect.y += EditorGUIUtility.singleLineHeight;
        contentRect.y += EditorGUIUtility.singleLineHeight;

        string stageName = "Play Mode";
        if (_gameObject != null && _gameObject.scene.IsValid())
            stageName = string.IsNullOrEmpty(_gameObject.scene.name) ? _gameObject.scene.path : _gameObject.scene.name;

        GUI.Label(labelRect, "in", EditorStyles.label);
        GUI.Label(contentRect, stageName, EditorStyles.label);
    }

    void DrawDemoTransformRow()
    {
        const float rowHeight = 18f;

        Rect rowRect = GUILayoutUtility.GetRect(100, 10000, rowHeight, rowHeight);

        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(rowRect, new Color(0.22f, 0.22f, 0.22f, EditorGUIUtility.isProSkin ? 0.6f : 0.2f));
        }

        Rect labelRect = rowRect;
        labelRect.xMin += 16f;
        GUI.Label(labelRect, "Transform", EditorStyles.label);

        if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
        {
            if (Event.current.button == 0)
            {
                Event.current.Use();
                _showTransformComparison = true;
            }
        }
    }
}
