using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RuntimeChangesSaver.Editor
{
    [InitializeOnLoad]
    public class ChangesInspector
    {
        static ChangesInspector()
        {
            UnityEditor.Editor.finishedDefaultHeaderGUI += OnPostHeaderGUI;
        }

        private static void OnPostHeaderGUI(UnityEditor.Editor editor)
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

            // changed components presence check
            var changedComponents = ChangesTracker.GetChangedComponents(go);
            bool hasChanges = changedComponents.Count > 0;

            bool hasTransformChange = changedComponents.Any(t => t is Transform or RectTransform);

            Debug.Log($"[TransformDebug][Inspector.Header] GO='{go.name}', changedCount={changedComponents.Count}, hasTransform={hasTransformChange}");

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            using (new EditorGUI.DisabledScope(!hasChanges))
            {
                GUIContent buttonContent = new GUIContent("Play Mode Overrides");
                Rect buttonRect = GUILayoutUtility.GetRect(buttonContent, EditorStyles.miniButton, GUILayout.Width(140f));
                if (GUI.Button(buttonRect, buttonContent, EditorStyles.miniButton))
                {
                    PopupWindow.Show(buttonRect, new OverridesWindow(go));
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }
    }
}
