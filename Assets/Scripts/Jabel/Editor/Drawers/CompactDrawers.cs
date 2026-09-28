using Jabel.Buffs;
using Jabel.Scripting.Blocks;
using UnityEditor;
using UnityEngine;

namespace Jabel.Editor
{
    /// <summary>"[Global ▾] [name]" on one line.</summary>
    [CustomPropertyDrawer(typeof(VariableTarget))]
    public class VariableTargetDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var field = EditorGUI.PrefixLabel(position, label);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            var scopeRect = new Rect(field.x, field.y, 80, field.height);
            var nameRect = new Rect(field.x + 84, field.y, field.width - 84, field.height);
            EditorGUI.PropertyField(scopeRect, property.FindPropertyRelative("scope"), GUIContent.none);
            EditorGUI.PropertyField(nameRect, property.FindPropertyRelative("name"), GUIContent.none);
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }

    /// <summary>"name = formula [x batch]" on one line.</summary>
    [CustomPropertyDrawer(typeof(FunctionArgument))]
    public class FunctionArgumentDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            float nameWidth = Mathf.Min(100, position.width * 0.3f);
            var nameRect = new Rect(position.x, position.y, nameWidth, position.height);
            var formulaRect = new Rect(position.x + nameWidth + 4, position.y, position.width - nameWidth - 70, position.height);
            var batchRect = new Rect(position.xMax - 62, position.y, 62, position.height);

            EditorGUI.PropertyField(nameRect, property.FindPropertyRelative("name"), GUIContent.none);
            EditorGUI.PropertyField(formulaRect, property.FindPropertyRelative("value"), GUIContent.none);
            var batch = property.FindPropertyRelative("scaleWithBatch");
            batch.boolValue = EditorGUI.ToggleLeft(batchRect, new GUIContent("×batch", batch.tooltip), batch.boolValue);

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }

    /// <summary>"variable ≥ minimum" on one line.</summary>
    [CustomPropertyDrawer(typeof(BuffRequirement))]
    public class BuffRequirementDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            float half = (position.width - 24) / 2;
            EditorGUI.PropertyField(new Rect(position.x, position.y, half, position.height), property.FindPropertyRelative("variable"), GUIContent.none);
            EditorGUI.LabelField(new Rect(position.x + half + 4, position.y, 16, position.height), "≥");
            EditorGUI.PropertyField(new Rect(position.x + half + 24, position.y, half, position.height), property.FindPropertyRelative("minimum"), GUIContent.none);
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }
}
