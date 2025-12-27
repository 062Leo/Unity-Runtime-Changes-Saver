using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Inspector GUI genau wie Prefab Overrides - ganz oben im Inspector
/// </summary>
[InitializeOnLoad]
public class PlayModeChangesInspector
{
    private static Dictionary<int, bool> foldoutStates = new Dictionary<int, bool>();
    private static Dictionary<int, bool> componentDetailStates = new Dictionary<int, bool>();

    static PlayModeChangesInspector()
    {
        // Nach dem St andard-Header von Unity zeichnen, gleich wie Prefab Overrides
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

        DrawPlayModeOverridesHeader(go, id, original, current, changes);
    }

    private static void DrawPlayModeOverridesHeader(GameObject go, int id, TransformSnapshot original, TransformSnapshot current, List<string> changes)
    {
        bool hasChanges = changes != null && changes.Count > 0;

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Play Mode Overrides", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        using (new EditorGUI.DisabledScope(!hasChanges))
        {
            GUIContent buttonContent = new GUIContent("Play Mode Overrides");
            Rect buttonRect = GUILayoutUtility.GetRect(buttonContent, EditorStyles.miniButton, GUILayout.Width(140f));
            if (GUI.Button(buttonRect, buttonContent, EditorStyles.miniButton))
            {
                Rect screenRect = GUIUtility.GUIToScreenRect(buttonRect);
                PopupWindow.Show(screenRect, new PlayModeOverridesPopup(go, id, original, current, changes));
            }
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(2);
    }

    internal static void DrawTransformBeforeAfter(GameObject go, int id, TransformSnapshot original, TransformSnapshot current, List<string> changes)
    {
        EditorGUILayout.BeginHorizontal();

        // Left: Before (read-only)
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("Before", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            DrawTransformSnapshotGUI(original, changes);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // Right: After (editable)
        EditorGUILayout.BeginVertical();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("After", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Revert", GUILayout.Width(70)))
        {
            RevertTransform(go.transform, original, changes);
        }
        if (GUILayout.Button("Apply", GUILayout.Width(70)))
        {
            PlayModeChangesTracker.ApplyAll(id, go);
            PlayModeChangesTracker.PersistSelectedChangesForAll();
        }
        EditorGUILayout.EndHorizontal();

        DrawTransformCurrentGUI(go.transform, changes);

        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private static void DrawTransformSnapshotGUI(TransformSnapshot snapshot, List<string> changedProps)
    {
        if (snapshot == null)
            return;

        bool positionChanged = changedProps.Contains("position");
        bool rotationChanged = changedProps.Contains("rotation");
        bool scaleChanged = changedProps.Contains("scale");

        Color oldColor = GUI.color;

        if (positionChanged)
            GUI.color = new Color(0.3f, 0.6f, 1f);
        EditorGUILayout.Vector3Field("Position", snapshot.position);
        GUI.color = oldColor;

        if (rotationChanged)
            GUI.color = new Color(0.3f, 0.6f, 1f);
        Vector3 beforeEuler = snapshot.rotation.eulerAngles;
        EditorGUILayout.Vector3Field("Rotation", beforeEuler);
        GUI.color = oldColor;

        if (scaleChanged)
            GUI.color = new Color(0.3f, 0.6f, 1f);
        EditorGUILayout.Vector3Field("Scale", snapshot.scale);
        GUI.color = oldColor;

        if (snapshot.isRectTransform)
        {
            bool anchoredPositionChanged = changedProps.Contains("anchoredPosition");
            bool anchoredPosition3DChanged = changedProps.Contains("anchoredPosition3D");
            bool anchorMinChanged = changedProps.Contains("anchorMin");
            bool anchorMaxChanged = changedProps.Contains("anchorMax");
            bool pivotChanged = changedProps.Contains("pivot");
            bool sizeDeltaChanged = changedProps.Contains("sizeDelta");
            bool offsetMinChanged = changedProps.Contains("offsetMin");
            bool offsetMaxChanged = changedProps.Contains("offsetMax");

            if (anchoredPositionChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            EditorGUILayout.Vector2Field("Anchored Position", snapshot.anchoredPosition);
            GUI.color = oldColor;

            if (anchoredPosition3DChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            EditorGUILayout.Vector3Field("Anchored Position 3D", snapshot.anchoredPosition3D);
            GUI.color = oldColor;

            if (anchorMinChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            EditorGUILayout.Vector2Field("Anchor Min", snapshot.anchorMin);
            GUI.color = oldColor;

            if (anchorMaxChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            EditorGUILayout.Vector2Field("Anchor Max", snapshot.anchorMax);
            GUI.color = oldColor;

            if (pivotChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            EditorGUILayout.Vector2Field("Pivot", snapshot.pivot);
            GUI.color = oldColor;

            if (sizeDeltaChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            EditorGUILayout.Vector2Field("Size Delta", snapshot.sizeDelta);
            GUI.color = oldColor;

            if (offsetMinChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            EditorGUILayout.Vector2Field("Offset Min", snapshot.offsetMin);
            GUI.color = oldColor;

            if (offsetMaxChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            EditorGUILayout.Vector2Field("Offset Max", snapshot.offsetMax);
            GUI.color = oldColor;
        }
    }

    private static void DrawTransformCurrentGUI(Transform transform, List<string> changedProps)
    {
        if (transform == null)
            return;

        bool positionChanged = changedProps.Contains("position");
        bool rotationChanged = changedProps.Contains("rotation");
        bool scaleChanged = changedProps.Contains("scale");

        Color oldColor = GUI.color;

        if (positionChanged)
            GUI.color = new Color(0.3f, 0.6f, 1f);
        Vector3 newPosition = EditorGUILayout.Vector3Field("Position", transform.localPosition);
        if (newPosition != transform.localPosition)
            transform.localPosition = newPosition;
        GUI.color = oldColor;

        if (rotationChanged)
            GUI.color = new Color(0.3f, 0.6f, 1f);
        Vector3 newEuler = EditorGUILayout.Vector3Field("Rotation", transform.localRotation.eulerAngles);
        if (newEuler != transform.localRotation.eulerAngles)
            transform.localRotation = Quaternion.Euler(newEuler);
        GUI.color = oldColor;

        if (scaleChanged)
            GUI.color = new Color(0.3f, 0.6f, 1f);
        Vector3 newScale = EditorGUILayout.Vector3Field("Scale", transform.localScale);
        if (newScale != transform.localScale)
            transform.localScale = newScale;
        GUI.color = oldColor;

        RectTransform rt = transform as RectTransform;
        if (rt != null)
        {
            bool anchoredPositionChanged = changedProps.Contains("anchoredPosition");
            bool anchoredPosition3DChanged = changedProps.Contains("anchoredPosition3D");
            bool anchorMinChanged = changedProps.Contains("anchorMin");
            bool anchorMaxChanged = changedProps.Contains("anchorMax");
            bool pivotChanged = changedProps.Contains("pivot");
            bool sizeDeltaChanged = changedProps.Contains("sizeDelta");
            bool offsetMinChanged = changedProps.Contains("offsetMin");
            bool offsetMaxChanged = changedProps.Contains("offsetMax");

            if (anchoredPositionChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            Vector2 newAnchoredPosition = EditorGUILayout.Vector2Field("Anchored Position", rt.anchoredPosition);
            if (newAnchoredPosition != rt.anchoredPosition)
                rt.anchoredPosition = newAnchoredPosition;
            GUI.color = oldColor;

            if (anchoredPosition3DChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            Vector3 newAnchoredPosition3D = EditorGUILayout.Vector3Field("Anchored Position 3D", rt.anchoredPosition3D);
            if (newAnchoredPosition3D != rt.anchoredPosition3D)
                rt.anchoredPosition3D = newAnchoredPosition3D;
            GUI.color = oldColor;

            if (anchorMinChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            Vector2 newAnchorMin = EditorGUILayout.Vector2Field("Anchor Min", rt.anchorMin);
            if (newAnchorMin != rt.anchorMin)
                rt.anchorMin = newAnchorMin;
            GUI.color = oldColor;

            if (anchorMaxChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            Vector2 newAnchorMax = EditorGUILayout.Vector2Field("Anchor Max", rt.anchorMax);
            if (newAnchorMax != rt.anchorMax)
                rt.anchorMax = newAnchorMax;
            GUI.color = oldColor;

            if (pivotChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            Vector2 newPivot = EditorGUILayout.Vector2Field("Pivot", rt.pivot);
            if (newPivot != rt.pivot)
                rt.pivot = newPivot;
            GUI.color = oldColor;

            if (sizeDeltaChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            Vector2 newSizeDelta = EditorGUILayout.Vector2Field("Size Delta", rt.sizeDelta);
            if (newSizeDelta != rt.sizeDelta)
                rt.sizeDelta = newSizeDelta;
            GUI.color = oldColor;

            if (offsetMinChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            Vector2 newOffsetMin = EditorGUILayout.Vector2Field("Offset Min", rt.offsetMin);
            if (newOffsetMin != rt.offsetMin)
                rt.offsetMin = newOffsetMin;
            GUI.color = oldColor;

            if (offsetMaxChanged)
                GUI.color = new Color(0.3f, 0.6f, 1f);
            Vector2 newOffsetMax = EditorGUILayout.Vector2Field("Offset Max", rt.offsetMax);
            if (newOffsetMax != rt.offsetMax)
                rt.offsetMax = newOffsetMax;
            GUI.color = oldColor;
        }
    }

    internal static void RevertTransform(Transform transform, TransformSnapshot original, List<string> changedProps)
    {
        if (transform == null || original == null || changedProps == null)
            return;

        RectTransform rt = transform as RectTransform;

        foreach (string prop in changedProps)
        {
            switch (prop)
            {
                case "position":
                    transform.localPosition = original.position;
                    break;
                case "rotation":
                    transform.localRotation = original.rotation;
                    break;
                case "scale":
                    transform.localScale = original.scale;
                    break;
                case "anchoredPosition":
                    if (rt) rt.anchoredPosition = original.anchoredPosition;
                    break;
                case "anchoredPosition3D":
                    if (rt) rt.anchoredPosition3D = original.anchoredPosition3D;
                    break;
                case "anchorMin":
                    if (rt) rt.anchorMin = original.anchorMin;
                    break;
                case "anchorMax":
                    if (rt) rt.anchorMax = original.anchorMax;
                    break;
                case "pivot":
                    if (rt) rt.pivot = original.pivot;
                    break;
                case "sizeDelta":
                    if (rt) rt.sizeDelta = original.sizeDelta;
                    break;
                case "offsetMin":
                    if (rt) rt.offsetMin = original.offsetMin;
                    break;
                case "offsetMax":
                    if (rt) rt.offsetMax = original.offsetMax;
                    break;
            }
        }
    }

    internal static string GetPropertyDisplayName(string property)
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

    internal static string GetPropertyTooltip(string property, TransformSnapshot original, TransformSnapshot current)
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