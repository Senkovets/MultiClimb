using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Qjjxk.Round
{
    public class ToggleButtonAttribute : PropertyAttribute
    {
        public string TrueLabel { get; }
        public string FalseLabel { get; }

        public ToggleButtonAttribute(string trueLabel = "ON", string falseLabel = "OFF")
        {
            TrueLabel = trueLabel;
            FalseLabel = falseLabel;
        }
    }

    #if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(ToggleButtonAttribute))]
    public class ToggleButtonDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var toggleAttr = (ToggleButtonAttribute)attribute;

            EditorGUI.BeginProperty(position, label, property);

            position = EditorGUI.PrefixLabel(position, label);
            var buttonWidth = position.width / 2;

            var trueRect = new Rect(position.x, position.y, buttonWidth, position.height);
            var falseRect = new Rect(position.x + buttonWidth, position.y, buttonWidth, position.height);

            var currentValue = property.boolValue;
            var newValue = currentValue;

            var trueToggle = GUI.Toggle(trueRect, currentValue, toggleAttr.TrueLabel, "ButtonLeft");
            var falseToggle = GUI.Toggle(falseRect, !currentValue, toggleAttr.FalseLabel, "ButtonRight");

            if (trueToggle != currentValue)
            {
                newValue = trueToggle;
            }
            else if (falseToggle != !currentValue)
            {
                newValue = !falseToggle;
            }

            if (newValue != currentValue)
            {
                property.boolValue = newValue;
            }

            EditorGUI.EndProperty();
        }
    }
    #endif
}