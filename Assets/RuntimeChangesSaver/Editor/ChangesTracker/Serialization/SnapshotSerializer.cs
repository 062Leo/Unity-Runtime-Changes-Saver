using System;
using UnityEngine;

namespace RuntimeChangesSaver.Editor.ChangesTracker.Serialization
{
    public static class SnapshotSerializer
    {
        private static string SerializeVector2(Vector2 v) => 
            $"{v.x.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        
        private static string SerializeVector3(Vector3 v) => 
            $"{v.x.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v.y.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        
        private static string SerializeVector4(Vector4 v) => 
            $"{v.x.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v.y.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v.z.ToString(System.Globalization.CultureInfo.InvariantCulture)},{v.w.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        
        private static string SerializeQuaternion(Quaternion q) => 
            $"{q.x.ToString(System.Globalization.CultureInfo.InvariantCulture)},{q.y.ToString(System.Globalization.CultureInfo.InvariantCulture)},{q.z.ToString(System.Globalization.CultureInfo.InvariantCulture)},{q.w.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

        public static Vector2 DeserializeVector2(string s)
        {
            var parts = s.Split(',');
            if (parts.Length != 2) return Vector2.zero;
            float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
            float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y);
            return new Vector2(x, y);
        }

        public static Vector3 DeserializeVector3(string s)
        {
            var parts = s.Split(',');
            if (parts.Length != 3) return Vector3.zero;
            float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x);
            float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y);
            float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z);
            return new Vector3(x, y, z);
        }

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

        public static void SerializeValue(object value, out string typeName, out string serializedValue)
        {
            typeName = string.Empty;
            serializedValue = string.Empty;

            if (value == null)
                return;

            switch (value)
            {
                case int i:
                    typeName = "Integer";
                    serializedValue = i.ToString();
                    break;
                case bool b:
                    typeName = "Boolean";
                    serializedValue = b.ToString();
                    break;
                case float f:
                    typeName = "Float";
                    serializedValue = f.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case string s:
                    typeName = "String";
                    serializedValue = s;
                    break;
                case Color c:
                    typeName = "Color";
                    serializedValue = "#" + ColorUtility.ToHtmlStringRGBA(c);
                    break;
                case Vector2 v2:
                    typeName = "Vector2";
                    serializedValue = SerializeVector2(v2);
                    break;
                case Vector3 v3:
                    typeName = "Vector3";
                    serializedValue = SerializeVector3(v3);
                    break;
                case Vector4 v4:
                    typeName = "Vector4";
                    serializedValue = SerializeVector4(v4);
                    break;
                case Quaternion q:
                    typeName = "Quaternion";
                    serializedValue = SerializeQuaternion(q);
                    break;
                case Enum e:
                    typeName = "Enum";
                    serializedValue = System.Convert.ToInt32(e).ToString();
                    break;
            }
        }
    }
}
