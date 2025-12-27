using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal class PlayModeOverridesPopup : PopupWindowContent
{
    private readonly GameObject _gameObject;
    private readonly int _instanceId;
    private readonly TransformSnapshot _original;
    private TransformSnapshot _current;
    private readonly List<string> _changedProperties;

    private Vector2 _scrollPosition;

    public PlayModeOverridesPopup(GameObject gameObject, int instanceId, TransformSnapshot original, TransformSnapshot current, List<string> changedProperties)
    {
        _gameObject = gameObject;
        _instanceId = instanceId;
        _original = original;
        _current = current;
        _changedProperties = changedProperties ?? new List<string>();
    }

    public override Vector2 GetWindowSize()
    {
        // Feste, aber ausreichend große Grundgröße; Höhe passt sich über Layout an
        return new Vector2(360f, 420f);
    }

    public override void OnGUI(Rect rect)
    {
        // ESC schließt das Popup wie beim Unity-Overrides-Fenster
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            editorWindow.Close();
            GUIUtility.ExitGUI();
        }

        DrawHeader(rect);

        bool hasChanges = _changedProperties != null && _changedProperties.Count > 0;

        GUILayout.Space(4);

        if (!hasChanges)
        {
            EditorGUILayout.LabelField("No Play Mode overrides", EditorStyles.miniLabel);
            return;
        }

        // Scrollbarer Bereich für Property-Liste und Before/After-Ansicht
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        DrawPropertyToggleList();

        GUILayout.Space(6);

        // Zweispaltige Before/After-Darstellung wie im bestehenden Inspector
        PlayModeChangesInspector.DrawTransformBeforeAfter(_gameObject, _instanceId, _original, _current, _changedProperties);

        EditorGUILayout.EndScrollView();

        GUILayout.Space(6);

        DrawActionButtons();
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

        GUIContent titleContent = new GUIContent("Review, Revert or Apply Play Mode Overrides");
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

    private void DrawPropertyToggleList()
    {
        if (_changedProperties == null || _changedProperties.Count == 0)
            return;

        EditorGUILayout.LabelField("Changed Properties", EditorStyles.boldLabel);

        foreach (var prop in _changedProperties)
        {
            bool selected = PlayModeChangesTracker.IsPropertySelected(_instanceId, prop);
            string displayName = PlayModeChangesInspector.GetPropertyDisplayName(prop);
            string tooltip = PlayModeChangesInspector.GetPropertyTooltip(prop, _original, _current);

            bool newSelected = EditorGUILayout.ToggleLeft(new GUIContent(displayName, tooltip), selected);
            if (newSelected != selected)
            {
                PlayModeChangesTracker.ToggleProperty(_instanceId, prop);
            }
        }
    }

    private void DrawActionButtons()
    {
        bool hasChanges = _changedProperties != null && _changedProperties.Count > 0;

        bool anySelected = false;
        if (hasChanges)
        {
            foreach (var prop in _changedProperties)
            {
                if (PlayModeChangesTracker.IsPropertySelected(_instanceId, prop))
                {
                    anySelected = true;
                    break;
                }
            }
        }

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        using (new EditorGUI.DisabledScope(!hasChanges))
        {
            if (GUILayout.Button("Revert All", EditorStyles.miniButtonLeft, GUILayout.Width(90)))
            {
                PlayModeChangesInspector.RevertTransform(_gameObject.transform, _original, _changedProperties);
                PlayModeChangesTracker.RevertAll(_instanceId);
            }

            using (new EditorGUI.DisabledScope(!anySelected))
            {
                if (GUILayout.Button("Revert Selected", EditorStyles.miniButtonMid, GUILayout.Width(110)))
                {
                    var selectedProps = new List<string>();
                    foreach (var prop in _changedProperties)
                    {
                        if (PlayModeChangesTracker.IsPropertySelected(_instanceId, prop))
                            selectedProps.Add(prop);
                    }

                    if (selectedProps.Count > 0)
                    {
                        PlayModeChangesInspector.RevertTransform(_gameObject.transform, _original, selectedProps);
                    }
                }

                if (GUILayout.Button("Apply Selected", EditorStyles.miniButtonMid, GUILayout.Width(110)))
                {
                    // Nutzt die aktuell markierten Properties im PlayModeChangesTracker
                    PlayModeChangesTracker.PersistSelectedChangesForAll();
                }
            }

            if (GUILayout.Button("Apply All", EditorStyles.miniButtonRight, GUILayout.Width(90)))
            {
                PlayModeChangesTracker.ApplyAll(_instanceId, _gameObject);
                PlayModeChangesTracker.PersistSelectedChangesForAll();
            }
        }

        GUILayout.EndHorizontal();
    }
}
