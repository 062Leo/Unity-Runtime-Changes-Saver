using System;
using UnityEngine;





[Serializable]
public class TransformSnapshot
{
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;

    public bool isRectTransform;
    public Vector2 anchoredPosition;
    public Vector3 anchoredPosition3D;
    public Vector2 anchorMin;
    public Vector2 anchorMax;
    public Vector2 pivot;
    public Vector2 sizeDelta;
    public Vector2 offsetMin;
    public Vector2 offsetMax;

    public TransformSnapshot(GameObject go)
    {
        Transform t = go.transform;
        position = t.localPosition;
        rotation = t.localRotation;
        scale = t.localScale;

        RectTransform rt = t as RectTransform;
        isRectTransform = rt != null;

        if (isRectTransform)
        {
            anchoredPosition = rt.anchoredPosition;
            anchoredPosition3D = rt.anchoredPosition3D;
            anchorMin = rt.anchorMin;
            anchorMax = rt.anchorMax;
            pivot = rt.pivot;
            sizeDelta = rt.sizeDelta;
            offsetMin = rt.offsetMin;
            offsetMax = rt.offsetMax;
        }

        Debug.Log($"[TransformDebug][Snapshot.Create] GO='{go.name}', isRect={isRectTransform}, pos={position}, rot={rotation.eulerAngles}, scale={scale}{(isRectTransform ? $", anchoredPos={anchoredPosition}, sizeDelta={sizeDelta}" : string.Empty)}");
    }
}
