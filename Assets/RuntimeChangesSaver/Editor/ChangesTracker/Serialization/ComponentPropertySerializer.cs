using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RuntimeChangesSaver.Editor.ChangesTracker.Serialization
{
    public static class ComponentPropertySerializer
    {
        public static void SerializeProperty(SerializedProperty prop, out string typeName, out string serializedValue)
        {
            typeName = "";
            serializedValue = "";

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    typeName = "Integer";
                    serializedValue = prop.intValue.ToString();
                    break;
                case SerializedPropertyType.Boolean:
                    typeName = "Boolean";
                    serializedValue = prop.boolValue.ToString();
                    break;
                case SerializedPropertyType.Float:
                    typeName = "Float";
                    serializedValue = prop.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case SerializedPropertyType.String:
                    typeName = "String";
                    serializedValue = prop.stringValue ?? string.Empty;
                    break;
                case SerializedPropertyType.Color:
                    typeName = "Color";
                    serializedValue = "#" + ColorUtility.ToHtmlStringRGBA(prop.colorValue);
                    break;
                case SerializedPropertyType.Vector2:
                    typeName = "Vector2";
                    serializedValue = SerializeVector2(prop.vector2Value);
                    break;
                case SerializedPropertyType.Vector3:
                    typeName = "Vector3";
                    serializedValue = SerializeVector3(prop.vector3Value);
                    break;
                case SerializedPropertyType.Vector4:
                    typeName = "Vector4";
                    serializedValue = SerializeVector4(prop.vector4Value);
                    break;
                case SerializedPropertyType.Quaternion:
                    typeName = "Quaternion";
                    serializedValue = SerializeQuaternion(prop.quaternionValue);
                    break;
                case SerializedPropertyType.Enum:
                    typeName = "Enum";
                    serializedValue = prop.enumValueIndex.ToString();
                    break;
            }
        }

        public static void ApplyPropertyValue(SerializedProperty prop, string typeName, string value)
        {
            switch (typeName)
            {
                case "Integer":
                    if (int.TryParse(value, out var iVal)) prop.intValue = iVal; break;
                case "Boolean":
                    if (bool.TryParse(value, out var bVal)) prop.boolValue = bVal; break;
                case "Float":
                    if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fVal)) prop.floatValue = fVal; break;
                case "String":
                    prop.stringValue = value; break;
                case "Color":
                    if (ColorUtility.TryParseHtmlString(value, out var col)) prop.colorValue = col; break;
                case "Vector2":
                    prop.vector2Value = SnapshotSerializer.DeserializeVector2(value); break;
                case "Vector3":
                    prop.vector3Value = SnapshotSerializer.DeserializeVector3(value); break;
                case "Vector4":
                    prop.vector4Value = SnapshotSerializer.DeserializeVector4(value); break;
                case "Quaternion":
                    prop.quaternionValue = SnapshotSerializer.DeserializeQuaternion(value); break;
                case "Enum":
                    if (int.TryParse(value, out var eVal)) prop.enumValueIndex = eVal; break;
            }
        }

        private static string SerializeVector2(Vector2 v) => 
            $"{v.x.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        
        private static string SerializeVector3(Vector3 v) => 
            $"{v.x.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v.y.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        
        private static string SerializeVector4(Vector4 v) => 
            $"{v.x.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v.y.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v.z.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v.w.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        
        private static string SerializeQuaternion(Quaternion q) => 
            $"{q.x.ToString(System.Globalization.CultureInfo.InvariantCulture)},{q.y.ToString(System.Globalization.CultureInfo.InvariantCulture)},{q.z.ToString(System.Globalization.CultureInfo.InvariantCulture)},{q.w.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

        public static object GetPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue;
                case SerializedPropertyType.Boolean: return prop.boolValue;
                case SerializedPropertyType.Float: return prop.floatValue;
                case SerializedPropertyType.String: return prop.stringValue;
                case SerializedPropertyType.Color: return prop.colorValue;
                case SerializedPropertyType.Vector2: return prop.vector2Value;
                case SerializedPropertyType.Vector3: return prop.vector3Value;
                case SerializedPropertyType.Vector4: return prop.vector4Value;
                case SerializedPropertyType.Quaternion: return prop.quaternionValue;
                case SerializedPropertyType.Enum: return prop.enumValueIndex;
                default: return null;
            }
        }
    }
}
