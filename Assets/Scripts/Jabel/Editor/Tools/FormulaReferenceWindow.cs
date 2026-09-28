using System;
using System.Linq;
using Jabel.Core;
using Jabel.Numbers;
using Jabel.Scripting;
using UnityEditor;
using UnityEngine;

namespace Jabel.Editor
{
    /// <summary>Lists formula syntax, functions and (in play mode) live variable values.</summary>
    public class FormulaReferenceWindow : EditorWindow
    {
        private Vector2 _scroll;
        private string _test = "10 * 1.15 ^ 5";

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying) Repaint();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Syntax", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Operators: + - * / % ^   comparisons: < > <= >= == !=   logic: && || ! (and / or / not)\n" +
                "Ternary: cond ? a : b     Numbers: 12, 1.5e30, 2.5K, 3M, 4B, 5T\n" +
                "Names resolve as: script locals → instance variables → derived values → variables → built-ins.\n" +
                "Built-ins: tick, time, tickDuration, awaySeconds, sessions. Buff hooks add: count, amount, level, index, n, batch.",
                MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Try a formula", EditorStyles.boldLabel);
            _test = EditorGUILayout.TextField(_test);
            var compiled = FormulaCompiler.Compile(_test);
            if (!compiled.IsValid) EditorGUILayout.HelpBox(compiled.Error, MessageType.Error);
            else
            {
                IFormulaContext ctx = Application.isPlaying ? ClickerManager.Instance : null;
                string result;
                try { result = NumberFormatter.Format(compiled.Evaluate(ctx)) + "   (" + compiled.Evaluate(ctx).ToInvariantString() + ")"; }
                catch (Exception ex) { result = ex.Message; }
                EditorGUILayout.LabelField("= " + result);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Functions", EditorStyles.boldLabel);
            foreach (var entry in FormulaFunctions.All.OrderBy(e => e.Name))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.SelectableLabel(entry.Signature, EditorStyles.label, GUILayout.Width(230), GUILayout.Height(18));
                EditorGUILayout.LabelField(entry.Description, EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }

            if (Application.isPlaying && ClickerManager.Instance != null)
            {
                var vars = ClickerManager.Instance.Variables;
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Live values", EditorStyles.boldLabel);
                foreach (var key in vars.Keys.Concat(vars.DerivedKeys).OrderBy(k => k))
                    EditorGUILayout.LabelField(key, NumberFormatter.Format(vars.Get(key)) + (vars.IsDerived(key) ? "  (derived)" : ""));
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
