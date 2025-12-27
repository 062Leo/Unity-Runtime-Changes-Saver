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

        // Demo-TreeView mit genau einem Eintrag: "Transform".
        GUILayout.Space(4);
        DrawDemoTransformRow();

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

    private void DrawDemoTransformRow()
    {
        const float rowHeight = 18f;

        Rect rowRect = GUILayoutUtility.GetRect(100, 10000, rowHeight, rowHeight);

        // Hintergrund wie eine einfache TreeView-Zeile
        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(rowRect, new Color(0.22f, 0.22f, 0.22f, EditorGUIUtility.isProSkin ? 0.6f : 0.2f));
        }

        Rect labelRect = rowRect;
        labelRect.xMin += 16f; // Einrückung wie bei einem TreeView-Item
        GUI.Label(labelRect, "Transform", EditorStyles.label);

        // Klick auf die Zeile öffnet ein Vergleichs-Popup links daneben.
        if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
        {
            if (Event.current.button == 0)
            {
                Event.current.Use();
                var popupRect = new Rect(rowRect.xMax, rowRect.y, 350f, 260f);
                PopupWindow.Show(popupRect, new TransformComparisonDemoPopup());
            }
        }
    }

    // Frontend-only-Vergleichspopup, angelehnt an ComparisonViewPopup
    private class TransformComparisonDemoPopup : PopupWindowContent
    {
        private Vector2 _scrollPos;
        private Vector3 _sourcePosition = new Vector3(0, 0, 0);
        private Vector3 _sourceRotation = new Vector3(0, 0, 0);
        private Vector3 _sourceScale = new Vector3(1, 1, 1);

        private Vector3 _instancePosition = new Vector3(1, 2, 3);
        private Vector3 _instanceRotation = new Vector3(10, 20, 30);
        private Vector3 _instanceScale = new Vector3(1.1f, 0.9f, 1.0f);

        public override Vector2 GetWindowSize()
        {
            return new Vector2(420f, 260f);
        }

        public override void OnGUI(Rect rect)
        {
            // Kopfzeile mit "Prefab Source" und "Override" wie im Original
            GUILayout.BeginHorizontal();
            GUILayout.Label("Prefab Source", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Override", EditorStyles.boldLabel);
            GUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            GUILayout.BeginHorizontal();

            // Linke Spalte: Source-Transform (read-only Darstellung)
            GUILayout.BeginVertical();
            EditorGUILayout.LabelField("Transform", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                _sourcePosition = EditorGUILayout.Vector3Field("Position", _sourcePosition);
                _sourceRotation = EditorGUILayout.Vector3Field("Rotation", _sourceRotation);
                _sourceScale = EditorGUILayout.Vector3Field("Scale", _sourceScale);
            }
            GUILayout.EndVertical();

            GUILayout.Space(16);

            // Rechte Spalte: Instance-Transform (editierbare Darstellung)
            GUILayout.BeginVertical();
            EditorGUILayout.LabelField("Transform", EditorStyles.boldLabel);
            _instancePosition = EditorGUILayout.Vector3Field("Position", _instancePosition);
            _instanceRotation = EditorGUILayout.Vector3Field("Rotation", _instanceRotation);
            _instanceScale = EditorGUILayout.Vector3Field("Scale", _instanceScale);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();

            GUILayout.FlexibleSpace();

            // Untere Buttonreihe: Apply / Revert ohne Funktion
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Revert", GUILayout.Width(80f)))
            {
                // Frontend-only
            }
            if (GUILayout.Button("Apply", GUILayout.Width(80f)))
            {
                // Frontend-only
            }
            GUILayout.EndHorizontal();
        }
    }
}
