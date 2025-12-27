using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-Fenster, das alle im PlayModeTransformChangesStore gespeicherten
/// (per Inspector-"Apply") Play-Mode-Änderungen anzeigt.
/// </summary>
public class PlayModeChangesLogWindow : EditorWindow
{
    private Vector2 _scrollPos;

    [MenuItem("Tools/Play Mode Changes Log")] 
    public static void ShowWindow()
    {
        var window = GetWindow<PlayModeChangesLogWindow>(false, "Play Mode Changes Log", true);
        window.Show();
    }

    private void OnGUI()
    {
        var store = PlayModeTransformChangesStore.LoadExisting();

        if (store == null)
        {
            EditorGUILayout.HelpBox("Kein PlayModeTransformChangesStore gefunden. Klicke im Play Mode im Inspector auf 'Apply', um Änderungen zu speichern.", MessageType.Info);
            return;
        }

        if (store.changes == null || store.changes.Count == 0)
        {
            EditorGUILayout.HelpBox("Der Store enthält aktuell keine gespeicherten Änderungen.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Gespeicherte Play-Mode-Änderungen", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        for (int i = 0; i < store.changes.Count; i++)
        {
            var change = store.changes[i];
            DrawChangeEntry(change, i);
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawChangeEntry(PlayModeTransformChangesStore.TransformChange change, int index)
    {
        EditorGUILayout.BeginVertical("HelpBox");

        string sceneName = string.IsNullOrEmpty(change.scenePath)
            ? "(Keine Szene)"
            : Path.GetFileNameWithoutExtension(change.scenePath);

        EditorGUILayout.LabelField($"#{index + 1}  {sceneName}", EditorStyles.boldLabel);

        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.LabelField("GameObject-Pfad:", change.objectPath);

            if (change.modifiedProperties == null || change.modifiedProperties.Count == 0)
            {
                EditorGUILayout.LabelField("(Keine Properties vermerkt)", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("Geänderte Properties:", EditorStyles.miniBoldLabel);
                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (var prop in change.modifiedProperties)
                    {
                        EditorGUILayout.LabelField(GetPropertyDisplayName(prop));
                    }
                }
            }
        }

        EditorGUILayout.EndVertical();
    }

    private string GetPropertyDisplayName(string property)
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
}
