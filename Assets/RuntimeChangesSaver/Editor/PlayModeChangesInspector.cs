using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Inspector GUI genau wie Prefab Overrides - ganz oben im Inspector
/// </summary>
[InitializeOnLoad]
public class PlayModeChangesInspector
{
    static PlayModeChangesInspector()
    {
        // Nach dem Standard-Header von Unity zeichnen, gleich wie Prefab Overrides
        Editor.finishedDefaultHeaderGUI += OnPostHeaderGUI;
    }

    private static void OnPostHeaderGUI(Editor editor)
    {
        // Nur im Play Mode anzeigen
        if (!Application.isPlaying)
            return;

        if (editor == null || editor.target == null)
            return;

        // GameObject bestimmen (funktioniert für GameObject- und Component-Inspektoren)
        GameObject go = editor.target as GameObject;
        if (go == null)
        {
            var comp = editor.target as Component;
            if (comp != null)
            {
                go = comp.gameObject;
            }
        }

        // Nur GameObject-Inspektoren interessieren uns
        if (go == null)
            return;

        int id = go.GetInstanceID();

        TransformSnapshot original = PlayModeChangesTracker.GetSnapshot(id);
        TransformSnapshot current = null;
        List<string> changes = null;
 
        // Falls aus irgendeinem Grund beim Play-Start kein Snapshot existiert,
        // legen wir beim ersten Aufruf einen Basis-Snapshot an.
        if (original == null)
        {
            original = new TransformSnapshot(go);
            PlayModeChangesTracker.SetSnapshot(id, original);
        }

        current = new TransformSnapshot(go);
        changes = PlayModeChangesTracker.GetChangedProperties(original, current);

        // Panel direkt im Header zeichnen
        DrawPlayModeOverridesHeader(go, id, original, current, changes);
    }

    private static void DrawPlayModeOverridesHeader(GameObject go, int id, TransformSnapshot original, TransformSnapshot current, List<string> changes)
    {
        // Exakt wie Prefab Override Header
        EditorGUILayout.BeginVertical("HelpBox");

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Play Mode Overrides", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        bool hasChanges = changes != null && changes.Count > 0;
        bool canMarkOrUnmark = Application.isPlaying && hasChanges && go != null;

        // Buttons deaktivieren, wenn nichts zum Markieren ist
        using (new EditorGUI.DisabledScope(!canMarkOrUnmark))
        {
            if (GUILayout.Button("Unmark All", EditorStyles.miniButtonLeft, GUILayout.Width(80)))
            {
                PlayModeChangesTracker.RevertAll(id);
            }

            if (GUILayout.Button("Mark All", EditorStyles.miniButtonRight, GUILayout.Width(80)))
            {
                PlayModeChangesTracker.ApplyAll(id, go);
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        // Info-Text / Property Liste
        if (!hasChanges)
        {
            EditorGUILayout.LabelField("Keine Änderungen.", EditorStyles.miniLabel);
        }
        else
        {
            // Property Liste genau wie Prefab Overrides
            foreach (string property in changes)
            {
                DrawPropertyOverride(id, original, current, property);
            }
        }

        // Apply-Button am Ende: schreibt alle markierten Änderungen ins ScriptableObject
        bool anySelected = false;
        if (hasChanges)
        {
            foreach (var prop in changes)
            {
                if (PlayModeChangesTracker.IsPropertySelected(id, prop))
                {
                    anySelected = true;
                    break;
                }
            }
        }

        using (new EditorGUI.DisabledScope(!anySelected || go == null))
        {
            if (GUILayout.Button("Apply", GUILayout.Width(80)))
            {
                PlayModeChangesTracker.PersistSelectedChangesForAll();
            }
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);
    }

    private static void DrawPropertyOverride(int id, TransformSnapshot original, TransformSnapshot current, string property)
    {
        EditorGUILayout.BeginHorizontal();

        // Checkbox für Selektion
        bool isSelected = PlayModeChangesTracker.IsPropertySelected(id, property);
        bool newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(14));

        if (newSelected != isSelected)
        {
            PlayModeChangesTracker.ToggleProperty(id, property);
        }

        // Property Label mit Icon (genau wie Prefab Override)
        GUIContent label = new GUIContent(GetPropertyDisplayName(property), GetPropertyTooltip(property, original, current));

        // Blau highlighting für Modified Properties
        if (isSelected)
        {
            GUI.contentColor = new Color(0.3f, 0.6f, 1f);
        }

        EditorGUILayout.LabelField(label, EditorStyles.label);

        GUI.contentColor = Color.white;

        GUILayout.FlexibleSpace();

        // Werte Anzeige
        string valueDisplay = GetValueDisplay(property, current);

        EditorGUILayout.LabelField(valueDisplay, EditorStyles.miniLabel, GUILayout.Width(150));

        EditorGUILayout.EndHorizontal();
    }

    private static string GetPropertyDisplayName(string property)
    {
        switch (property)
        {
            case "position": return "Position";
            case "rotation": return "Rotation";
            case "scale": return "Scale";
            case "anchoredPosition": return "Anchored Position";
            case "anchoredPosition3D": return "Anchored Position 3D";
            case "anchorMin": return "Anchor Min";
            case "anchorMax": return "Anchor Max";
            case "pivot": return "Pivot";
            case "sizeDelta": return "Size Delta";
            case "offsetMin": return "Offset Min";
            case "offsetMax": return "Offset Max";
            default: return property;
        }
    }

    private static string GetPropertyTooltip(string property, TransformSnapshot original, TransformSnapshot current)
    {
        string originalValue = GetValueDisplay(property, original);
        string currentValue = GetValueDisplay(property, current);
        return $"Original: {originalValue}\nCurrent: {currentValue}";
    }

    private static string GetValueDisplay(string property, TransformSnapshot snapshot)
    {
        switch (property)
        {
            case "position": return FormatVector3(snapshot.position);
            case "rotation": return FormatQuaternion(snapshot.rotation);
            case "scale": return FormatVector3(snapshot.scale);
            case "anchoredPosition": return FormatVector2(snapshot.anchoredPosition);
            case "anchoredPosition3D": return FormatVector3(snapshot.anchoredPosition3D);
            case "anchorMin": return FormatVector2(snapshot.anchorMin);
            case "anchorMax": return FormatVector2(snapshot.anchorMax);
            case "pivot": return FormatVector2(snapshot.pivot);
            case "sizeDelta": return FormatVector2(snapshot.sizeDelta);
            case "offsetMin": return FormatVector2(snapshot.offsetMin);
            case "offsetMax": return FormatVector2(snapshot.offsetMax);
            default: return "";
        }
    }

    private static string FormatVector3(Vector3 v)
    {
        return $"({v.x:F2}, {v.y:F2}, {v.z:F2})";
    }

    private static string FormatVector2(Vector2 v)
    {
        return $"({v.x:F2}, {v.y:F2})";
    }

    private static string FormatQuaternion(Quaternion q)
    {
        Vector3 euler = q.eulerAngles;
        return $"({euler.x:F1}°, {euler.y:F1}°, {euler.z:F1}°)";
    }
}