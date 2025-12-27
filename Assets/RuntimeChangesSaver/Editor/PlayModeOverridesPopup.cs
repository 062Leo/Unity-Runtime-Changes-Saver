using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal class PlayModeOverridesPopup : PopupWindowContent
{
    private readonly GameObject _gameObject;

    // Reines Frontend: wir speichern nur das GameObject für die Header-Anzeige.
    public PlayModeOverridesPopup(GameObject gameObject, int instanceId, TransformSnapshot original, TransformSnapshot current, List<string> changedProperties)
    {
        _gameObject = gameObject;
    }

    public override Vector2 GetWindowSize()
    {
        // Basisgröße ähnlich PrefabOverridesWindow, passt sich über Layout an
        return new Vector2(360f, 420f);
    }

    public override void OnGUI(Rect rect)
    {
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            editorWindow.Close();
            GUIUtility.ExitGUI();
        }

        DrawHeader(rect);

        GUILayout.Space(4);

        // Reines Frontend: wir tun so, als ob keine TreeView-Daten vorhanden wären
        // und zeigen nur den "No overrides"-Zustand wie im PrefabOverridesWindow.
        EditorGUILayout.LabelField("No overrides", EditorStyles.miniLabel);

        GUILayout.FlexibleSpace();

        // Untere Button-Leiste optisch wie im PrefabOverridesWindow.
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Revert All", GUILayout.Width(120f)))
        {
            // Frontend-only: keine Aktion
        }

        if (GUILayout.Button("Apply All", GUILayout.Width(120f)))
        {
            // Frontend-only: keine Aktion
        }

        GUILayout.EndHorizontal();
        GUILayout.Space(6);
    }

    private void DrawHeader(Rect totalRect)
    {
        const float headerHeight = 60f;
        const float leftMargin = 6f;

        Rect headerRect = GUILayoutUtility.GetRect(20, 10000, headerHeight, headerHeight);

        Color bgColor = EditorGUIUtility.isProSkin
            ? new Color(0.5f, 0.5f, 0.5f, 0.2f)
            : new Color(0.9f, 0.9f, 0.9f, 0.6f);
        EditorGUI.DrawRect(headerRect, bgColor);

        // Titelzeile (rechtsbündig, wie im PrefabOverridesWindow)
        GUIStyle boldRight = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleRight
        };

        // Titel exakt wie im PrefabOverridesWindow
        GUIContent titleContent = new GUIContent("Review, Revert or Apply Overrides");
        float titleWidth = boldRight.CalcSize(titleContent).x;
        Rect titleRect = new Rect(headerRect.x + leftMargin, headerRect.y, titleWidth, headerRect.height);
        titleRect.height = EditorGUIUtility.singleLineHeight;
        GUI.Label(titleRect, titleContent, boldRight);

        // "on" Zeile
        float labelWidth = EditorStyles.label.CalcSize(new GUIContent("on")).x;

        Rect lineRect = headerRect;
        lineRect.height = EditorGUIUtility.singleLineHeight;
        lineRect.y += 20f;

        Rect labelRect = new Rect(headerRect.x + leftMargin, lineRect.y, labelWidth, lineRect.height);
        Rect contentRect = lineRect;
        contentRect.xMin = labelRect.xMax;

        GUI.Label(labelRect, "on", EditorStyles.label);
        GUI.Label(contentRect, _gameObject != null ? _gameObject.name : "<none>", EditorStyles.label);

        // "in" Zeile
        labelRect.y += EditorGUIUtility.singleLineHeight;
        contentRect.y += EditorGUIUtility.singleLineHeight;

        string stageName = "Play Mode";
        if (_gameObject != null && _gameObject.scene.IsValid())
            stageName = string.IsNullOrEmpty(_gameObject.scene.name) ? _gameObject.scene.path : _gameObject.scene.name;

        GUI.Label(labelRect, "in", EditorStyles.label);
        GUI.Label(contentRect, stageName, EditorStyles.label);
    }
}
