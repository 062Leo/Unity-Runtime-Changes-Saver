﻿using UnityEditor;
using UnityEngine;

namespace RuntimeChangesSaver.Editor.OverrideComparePopup
{
    /// <summary>
    /// Handles serialization and deserialization of component properties.
    /// </summary>
    internal static class OverrideComparePopupSerialization
    {
        /// <summary>
        /// Sets a property value from a serialized representation.
        /// </summary>
        public static void SetPropertyValue(SerializedProperty prop, object value)
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

        /// <summary>
        /// Applies a serialized component value from string representation.
        /// </summary>
        public static void ApplySerializedComponentValue(SerializedProperty prop, string typeName, string value)
        {
            switch (typeName)
            {
                case "Integer":
                    if (int.TryParse(value, out var iVal)) prop.intValue = iVal;
                    break;
                case "Boolean":
                    if (bool.TryParse(value, out var bVal)) prop.boolValue = bVal;
                    break;
                case "Float":
                    if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fVal)) prop.floatValue = fVal;
                    break;
                case "String":
                    prop.stringValue = value;
                    break;
                case "Color":
                    if (ColorUtility.TryParseHtmlString(value, out var col)) prop.colorValue = col;
                    break;
                case "Vector2":
                    prop.vector2Value = DeserializeVector2(value);
                    break;
                case "Vector3":
                    prop.vector3Value = DeserializeVector3(value);
                    break;
                case "Vector4":
                    prop.vector4Value = DeserializeVector4(value);
                    break;
                case "Quaternion":
                    prop.quaternionValue = DeserializeQuaternion(value);
                    break;
                case "Enum":
                    if (int.TryParse(value, out var eVal)) prop.enumValueIndex = eVal;
                    break;
            }
        }

        /// <summary>
        /// Deserializes a Vector2 from string format.
        /// </summary>
        public static Vector2 DeserializeVector2(string s)
        {
            var parts = s.Split(',');
            if (parts.Length != 2) return Vector2.zero;
            float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
            float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y);
            return new Vector2(x, y);
        }

        /// <summary>
        /// Deserializes a Vector3 from string format.
        /// </summary>
        public static Vector3 DeserializeVector3(string s)
        {
            var parts = s.Split(',');
            if (parts.Length != 3) return Vector3.zero;
            float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
            float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y);
            float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z);
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Deserializes a Vector4 from string format.
        /// </summary>
        public static Vector4 DeserializeVector4(string s)
        {
            var parts = s.Split(',');
            if (parts.Length != 4) return Vector4.zero;
            float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
            float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y);
            float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z);
            float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w);
            return new Vector4(x, y, z, w);
        }

        /// <summary>
        /// Deserializes a Quaternion from string format.
        /// </summary>
        public static Quaternion DeserializeQuaternion(string s)
        {
            var parts = s.Split(',');
            if (parts.Length != 4) return Quaternion.identity;
            float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
            float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y);
            float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z);
            float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w);
            return new Quaternion(x, y, z, w);
        }

        /// <summary>
        /// Serializes a property to string representation.
        /// </summary>
        public static string SerializeProperty(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue.ToString();
                case SerializedPropertyType.Boolean: return prop.boolValue.ToString();
                case SerializedPropertyType.Float: return prop.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case SerializedPropertyType.String: return prop.stringValue;
                case SerializedPropertyType.Color: return "#" + ColorUtility.ToHtmlStringRGBA(prop.colorValue);
                case SerializedPropertyType.Vector2:
                    var v2 = prop.vector2Value;
                    return $"{v2.x.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v2.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                case SerializedPropertyType.Vector3:
                    var v3 = prop.vector3Value;
                    return $"{v3.x.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v3.y.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v3.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                case SerializedPropertyType.Vector4:
                    var v4 = prop.vector4Value;
                    return $"{v4.x.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v4.y.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v4.z.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v4.w.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                case SerializedPropertyType.Quaternion:
                    var q = prop.quaternionValue;
                    return $"{q.x.ToString(System.Globalization.CultureInfo.InvariantCulture)},{q.y.ToString(System.Globalization.CultureInfo.InvariantCulture)},{q.z.ToString(System.Globalization.CultureInfo.InvariantCulture)},{q.w.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                case SerializedPropertyType.Enum: return prop.enumValueIndex.ToString();
                default: return string.Empty;
            }
        }

        /// <summary>
        /// Checks if two serialized properties differ in value.
        /// </summary>
        public static bool PropertiesDiffer(SerializedProperty a, SerializedProperty b)
        {
            switch (a.propertyType)
            {
                case SerializedPropertyType.Integer: return a.intValue != b.intValue;
                case SerializedPropertyType.Boolean: return a.boolValue != b.boolValue;
                case SerializedPropertyType.Float: return !Mathf.Approximately(a.floatValue, b.floatValue);
                case SerializedPropertyType.String: return a.stringValue != b.stringValue;
                case SerializedPropertyType.Color: return a.colorValue != b.colorValue;
                case SerializedPropertyType.Vector2: return a.vector2Value != b.vector2Value;
                case SerializedPropertyType.Vector3: return a.vector3Value != b.vector3Value;
                case SerializedPropertyType.Vector4: return a.vector4Value != b.vector4Value;
                case SerializedPropertyType.Quaternion: return a.quaternionValue != b.quaternionValue;
                case SerializedPropertyType.Enum: return a.enumValueIndex != b.enumValueIndex;
                default: return false;
            }
        }
    }
}

