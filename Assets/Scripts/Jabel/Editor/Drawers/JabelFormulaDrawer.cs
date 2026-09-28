using Jabel.Scripting;
using UnityEditor;
using UnityEngine;

namespace Jabel.Editor
{
    /// <summary>Single-line formula field with live syntax validation (red when invalid, error in tooltip).</summary>
    [CustomPropertyDrawer(typeof(JabelFormula))]
    public class JabelFormulaDrawer : PropertyDrawer
    {
        private static GUIStyle _fieldStyle;
        private static GUIStyle _badgeStyle;

        private static GUIStyle FieldStyle => _fieldStyle ??= new GUIStyle(EditorStyles.textField)
        {
            font = EditorGUIUtility.Load("Fonts/RobotoMono/RobotoMono-Regular.ttf") as Font,
            fontSize = 11
        };

        private static GUIStyle BadgeStyle => _badgeStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Italic
        };

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var expression = property.FindPropertyRelative("expression");
            EditorGUI.BeginProperty(position, label, property);

            var compiled = FormulaCompiler.Compile(expression.stringValue);
            string tooltip = compiled.IsValid
                ? (compiled.IsConstant && !string.IsNullOrWhiteSpace(expression.stringValue)
                    ? "Constant: " + compiled.ConstantValue
                    : "Formula. Identifiers: " + string.Join(", ", compiled.Identifiers))
                : "Error: " + compiled.Error;

            var content = new GUIContent(label.text, (label.tooltip ?? string.Empty) + "\n" + tooltip);
            var field = EditorGUI.PrefixLabel(position, content);

            var badge = new Rect(field.x, field.y, 18, field.height);
            var text = new Rect(field.x + 18, field.y, field.width - 18, field.height);

            var old = GUI.backgroundColor;
            if (!compiled.IsValid) GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
            GUI.Label(badge, new GUIContent("ƒ", tooltip), BadgeStyle);

            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            EditorGUI.BeginChangeCheck();
            string value = EditorGUI.TextField(text, expression.stringValue, FieldStyle);
            if (EditorGUI.EndChangeCheck()) expression.stringValue = value;
            EditorGUI.indentLevel = indent;

            GUI.backgroundColor = old;
            EditorGUI.EndProperty();
        }
    }
}
