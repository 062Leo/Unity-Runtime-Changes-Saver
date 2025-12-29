using System.Collections.Generic;
using UnityEditor;
using UnityEngine;



internal class PlayModeOverridesWindow : PopupWindowContent
{
    private readonly GameObject targetGO;
    private readonly List<Component> changedComponents;
    private Vector2 scroll;
    private const float RowHeight = 22f;
    private const float HeaderHeight = 28f;
    private const float FooterHeight = 50f;

    public PlayModeOverridesWindow(GameObject go)
    {
        targetGO = go;
        changedComponents = PlayModeChangesTracker.GetChangedComponents(go);
    }

    public override Vector2 GetWindowSize()
    {
        int count = Mathf.Max(1, changedComponents.Count);
        float listHeight = count * RowHeight;
        float totalHeight = HeaderHeight + listHeight + FooterHeight + 10;
        return new Vector2(320, Mathf.Min(500, totalHeight));
    }

    public override void OnGUI(Rect rect)
    {
        // Header
        Rect headerRect = new Rect(rect.x, rect.y, rect.width, HeaderHeight);
        DrawHeader(headerRect);

        if (changedComponents.Count == 0)
        {
            Rect helpRect = new Rect(rect.x + 10, rect.y + HeaderHeight, rect.width - 20, 40);
            GUI.Label(helpRect, "No changed components", EditorStyles.helpBox);
            return;
        }

        // Component list
        float listHeight = rect.height - HeaderHeight - FooterHeight;
        Rect listRect = new Rect(rect.x, rect.y + HeaderHeight, rect.width, listHeight);
        DrawComponentList(listRect);

        // Footer
        Rect footerRect = new Rect(rect.x, rect.y + HeaderHeight + listHeight, rect.width, FooterHeight);
        DrawFooter(footerRect);
    }

    void DrawHeader(Rect rect)
    {
        EditorGUI.LabelField(
            new Rect(rect.x + 6, rect.y + 6, rect.width - 12, 20),
            "Play Mode Overrides",
            EditorStyles.boldLabel
        );
    }

    void DrawComponentList(Rect rect)
    {
        Rect viewRect = new Rect(0, 0, rect.width - 16, changedComponents.Count * RowHeight);
        scroll = GUI.BeginScrollView(rect, scroll, viewRect);

        for (int i = 0; i < changedComponents.Count; i++)
        {
            Rect row = new Rect(0, i * RowHeight, viewRect.width, RowHeight);
            DrawRow(row, changedComponents[i]);
        }

        GUI.EndScrollView();
    }

    void DrawRow(Rect rowRect, Component component)
    {
        if (Event.current.type == EventType.Repaint)
            EditorStyles.helpBox.Draw(rowRect, false, false, false, false);

        var content = EditorGUIUtility.ObjectContent(component, component.GetType());
        Rect labelRect = new Rect(rowRect.x + 6, rowRect.y + 3, rowRect.width - 12, 16);

        if (GUI.Button(labelRect, content, EditorStyles.label))
        {
            bool isTransform = component is Transform;
            bool isRectTransform = component is RectTransform;
            Debug.Log($"[TransformDebug][OverridesWindow.RowClick] GO='{targetGO.name}', Component='{component.GetType().Name}', isTransform={isTransform}, isRectTransform={isRectTransform}");
            PopupWindow.Show(rowRect, new PlayModeOverrideComparePopup(component));
        }
    }

    void DrawFooter(Rect rect)
    {
        // Draw background
        if (Event.current.type == EventType.Repaint)
        {
            Color bgColor = EditorGUIUtility.isProSkin
                ? new Color(0.22f, 0.22f, 0.22f, 0.8f)
                : new Color(0.8f, 0.8f, 0.8f, 0.8f);
            EditorGUI.DrawRect(rect, bgColor);
        }

        // Buttons
        float buttonWidth = 120f;
        float buttonHeight = 30f;
        float spacing = 10f;
        float totalWidth = buttonWidth * 2 + spacing;
        float startX = rect.x + (rect.width - totalWidth) / 2;
        float startY = rect.y + (rect.height - buttonHeight) / 2;

        Rect revertRect = new Rect(startX, startY, buttonWidth, buttonHeight);
        Rect applyRect = new Rect(startX + buttonWidth + spacing, startY, buttonWidth, buttonHeight);

        if (GUI.Button(revertRect, "Revert All"))
        {
            RevertAllChanges();
            editorWindow.Close();
        }

        if (GUI.Button(applyRect, "Apply All"))
        {
            ApplyAllChanges();
            editorWindow.Close();
        }
    }

    void RevertAllChanges()
    {
        string key = PlayModeChangesTracker.GetGameObjectKey(targetGO);
        var originalSnapshot = PlayModeChangesTracker.GetSnapshot(targetGO);

        if (originalSnapshot != null)
        {
            var transform = targetGO.transform;
            var rt = transform as RectTransform;

            // Revert transform
            transform.localPosition = originalSnapshot.position;
            transform.localRotation = originalSnapshot.rotation;
            transform.localScale = originalSnapshot.scale;

            if (rt != null && originalSnapshot.isRectTransform)
            {
                rt.anchoredPosition = originalSnapshot.anchoredPosition;
                rt.anchoredPosition3D = originalSnapshot.anchoredPosition3D;
                rt.anchorMin = originalSnapshot.anchorMin;
                rt.anchorMax = originalSnapshot.anchorMax;
                rt.pivot = originalSnapshot.pivot;
                rt.sizeDelta = originalSnapshot.sizeDelta;
                rt.offsetMin = originalSnapshot.offsetMin;
                rt.offsetMax = originalSnapshot.offsetMax;
            }

            Debug.Log($"[TransformDebug][OverridesWindow.RevertAll] GO='{targetGO.name}', originalPos={originalSnapshot.position}, originalRot={originalSnapshot.rotation.eulerAngles}, originalScale={originalSnapshot.scale}, isRect={originalSnapshot.isRectTransform}");
        }

        // Revert other components
        foreach (var comp in changedComponents)
        {
            if (comp is Transform) continue;

            string compKey = PlayModeChangesTracker.GetComponentKey(comp);
            var snapshot = PlayModeChangesTracker.GetComponentSnapshot(targetGO, compKey);

            if (snapshot != null)
            {
                RevertComponent(comp, snapshot);
            }
        }

        Debug.Log($"[TransformDebug][OverridesWindow.RevertAll] Completed for GO='{targetGO.name}'");
    }

    void ApplyAllChanges()
    {
        // Transform-Änderungen für dieses GameObject annehmen und für den
        // Übergang zurück in den Edit Mode persistieren.
        PlayModeChangesTracker.AcceptTransformChanges(targetGO);
        Debug.Log($"[TransformDebug][OverridesWindow.ApplyAll] Accepted all transform changes on GO='{targetGO.name}' (will be applied when exiting play mode)");
    }

    void RevertComponent(Component comp, ComponentSnapshot snapshot)
    {
        SerializedObject so = new SerializedObject(comp);

        foreach (var kvp in snapshot.properties)
        {
            SerializedProperty prop = so.FindProperty(kvp.Key);
            if (prop == null) continue;

            try
            {
                SetPropertyValue(prop, kvp.Value);
            }
            catch { }
        }

        so.ApplyModifiedProperties();
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
}