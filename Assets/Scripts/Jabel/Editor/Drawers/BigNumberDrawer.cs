using Jabel.Numbers;
using UnityEditor;
using UnityEngine;

namespace Jabel.Editor
{
    /// <summary>Edits a BigNumber as text: "1500", "1.5K", "2.5e40".</summary>
    [CustomPropertyDrawer(typeof(BigNumber))]
    public class BigNumberDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var m = property.FindPropertyRelative("m");
            var e = property.FindPropertyRelative("e");
            EditorGUI.BeginProperty(position, label, property);

            var current = new BigNumber(m.doubleValue, e.longValue);
            var field = EditorGUI.PrefixLabel(position, label);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            EditorGUI.BeginChangeCheck();
            string text = EditorGUI.DelayedTextField(field, current.ToInvariantString());
            if (EditorGUI.EndChangeCheck() && BigNumber.TryParse(text, out var parsed))
            {
                m.doubleValue = parsed.RawMantissa;
                e.longValue = parsed.RawExponent;
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }
}
