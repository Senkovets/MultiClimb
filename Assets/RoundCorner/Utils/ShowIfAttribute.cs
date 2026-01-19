using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.Reflection;
#endif

namespace Qjjxk.Round
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ShowIfAttribute : Attribute
    {
        public readonly string fieldName;
        public readonly bool value;

        public ShowIfAttribute(string fieldName, bool value = true)
        {
            this.fieldName = fieldName;
            this.value = value;
        }
    }

    #if UNITY_EDITOR
    [CustomEditor(typeof(RoundCornerImage), true)]
    public class ShowIfEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var targetType = target.GetType();
            var fields = targetType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var field in fields)
            {
                var showIfAttr = field.GetCustomAttribute<ShowIfAttribute>();

                var hideInInspectorAttr = field.GetCustomAttribute<HideInInspector>();
                if (hideInInspectorAttr != null) continue;

                var property = serializedObject.FindProperty(field.Name);
                if (property == null) continue;

                if (showIfAttr != null)
                {
                    if (ShouldShowProperty(showIfAttr))
                    {
                        EditorGUILayout.PropertyField(property, true);
                    }
                }
                else
                {
                    EditorGUILayout.PropertyField(property, true);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private bool ShouldShowProperty(ShowIfAttribute showIf)
        {
            var conditionProperty = serializedObject.FindProperty(showIf.fieldName);
            if (conditionProperty == null) return true;

            return conditionProperty.boolValue == showIf.value;
        }
    }
    #endif
}