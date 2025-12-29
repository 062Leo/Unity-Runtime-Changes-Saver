
using UnityEditor;
using UnityEngine;




[InitializeOnLoad]
public class PlayModeChangesInspector
{
    static PlayModeChangesInspector()
    {
        Editor.finishedDefaultHeaderGUI += OnPostHeaderGUI;
    }

    private static void OnPostHeaderGUI(Editor editor)
    {
        if (!Application.isPlaying)
            return;

        if (editor == null || editor.target == null)
            return;

        GameObject go = editor.target as GameObject;
        if (go == null)
        {
            var comp = editor.target as Component;
            if (comp != null)
            {
                go = comp.gameObject;
            }
        }

        if (go == null)
            return;

        // Check if any component has changes
        var changedComponents = PlayModeChangesTracker.GetChangedComponents(go);
        bool hasChanges = changedComponents.Count > 0;

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        using (new EditorGUI.DisabledScope(!hasChanges))
        {
            GUIContent buttonContent = new GUIContent("Play Mode Overrides");
            Rect buttonRect = GUILayoutUtility.GetRect(buttonContent, EditorStyles.miniButton, GUILayout.Width(140f));
            if (GUI.Button(buttonRect, buttonContent, EditorStyles.miniButton))
            {
                PopupWindow.Show(buttonRect, new PlayModeOverridesWindow(go));
            }
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(2);
    }
}
