using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Qjjxk.Round
{
    public class VectorRangeAttribute : PropertyAttribute
    {
        public readonly float min;
        public readonly float max;
        public readonly string[] names;

        public VectorRangeAttribute(float min, float max, params string[] names)
        {
            this.min = min;
            this.max = max;
            this.names = names;
        }
    }

    #if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(VectorRangeAttribute))]
    public class VectorRangeDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 5 + EditorGUIUtility.standardVerticalSpacing * 4;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Vector4)
            {
                EditorGUI.LabelField(position, label.text, "仅支持 Vector4");
                return;
            }

            var range = (VectorRangeAttribute)attribute;
            EditorGUI.BeginProperty(position, label, property);

            var titleRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(titleRect, label.text, EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            var yOffset = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            var value = property.vector4Value;

            for (int i = 0; i < 4; i++)
            {
                string name = (range.names != null && range.names.Length > i) ? range.names[i] : "XYZW"[i].ToString();
                var rect = new Rect(position.x, position.y + yOffset * (i + 1), position.width, EditorGUIUtility.singleLineHeight);
                value[i] = EditorGUI.Slider(rect, name, value[i], range.min, range.max);
            }

            property.vector4Value = value;
            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }
    }
    #endif
}