using System.Collections.Generic;
using Jabel.Localization;
using UnityEditor;
using UnityEngine;

namespace Jabel.Editor
{
    /// <summary>
    /// Localization key field with a key picker and an inline preview of the translation,
    /// so designers see the actual text while editing data.
    /// </summary>
    [CustomPropertyDrawer(typeof(LocalizedString))]
    public class LocalizedStringDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUIUtility.singleLineHeight * 2 + 2;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var key = property.FindPropertyRelative("key");
            EditorGUI.BeginProperty(position, label, property);

            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var field = EditorGUI.PrefixLabel(line, label);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var textRect = new Rect(field.x, field.y, field.width - 22, field.height);
            var buttonRect = new Rect(field.xMax - 20, field.y, 20, field.height);
            key.stringValue = EditorGUI.TextField(textRect, key.stringValue);

            if (GUI.Button(buttonRect, "▾", EditorStyles.miniButton))
                ShowPicker(key);

            var previewRect = new Rect(field.x, line.yMax + 2, field.width, EditorGUIUtility.singleLineHeight);
            string preview = LocalizationEditorCache.Preview(key.stringValue);
            var style = new GUIStyle(EditorStyles.miniLabel) { richText = false };
            if (preview == null)
            {
                style.normal.textColor = string.IsNullOrEmpty(key.stringValue) ? Color.gray : new Color(1f, 0.55f, 0.3f);
                EditorGUI.LabelField(previewRect, string.IsNullOrEmpty(key.stringValue) ? "(no key)" : "(missing translation)", style);
            }
            else
            {
                style.normal.textColor = new Color(0.55f, 0.8f, 1f);
                EditorGUI.LabelField(previewRect, "“" + preview.Replace("\n", " ⏎ ") + "”", style);
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }

        private static void ShowPicker(SerializedProperty key)
        {
            var menu = new GenericMenu();
            var so = key.serializedObject;
            string path = key.propertyPath;
            foreach (var k in LocalizationEditorCache.Keys)
            {
                string captured = k;
                menu.AddItem(new GUIContent(k.Replace('.', '/')), k == key.stringValue, () =>
                {
                    so.Update();
                    so.FindProperty(path).stringValue = captured;
                    so.ApplyModifiedProperties();
                });
            }
            if (menu.GetItemCount() == 0) menu.AddDisabledItem(new GUIContent("No LocalizationDatabase found"));
            menu.ShowAsContext();
        }
    }

    /// <summary>Loads all localization databases in the project for editor previews. Refreshes on asset changes.</summary>
    public class LocalizationEditorCache : AssetPostprocessor
    {
        private static Dictionary<string, string> _preview;
        private static List<string> _keys;

        public static IReadOnlyList<string> Keys
        {
            get { Ensure(); return _keys; }
        }

        public static string Preview(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            Ensure();
            return _preview.TryGetValue(key, out var v) ? v : null;
        }

        public static bool HasKey(string key)
        {
            Ensure();
            return key != null && _preview.ContainsKey(key);
        }

        public static void Invalidate() => _preview = null;

        private static void Ensure()
        {
            if (_preview != null) return;
            _preview = new Dictionary<string, string>();
            var keySet = new SortedSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:LocalizationDatabase"))
            {
                var db = AssetDatabase.LoadAssetAtPath<LocalizationDatabase>(AssetDatabase.GUIDToAssetPath(guid));
                if (db == null) continue;
                var tables = db.BuildDictionaries();
                if (!tables.TryGetValue(db.DefaultLanguage, out var main)) continue;
                foreach (var pair in main)
                {
                    _preview[pair.Key] = pair.Value;
                    string baseKey = pair.Key.Contains("#") ? pair.Key.Substring(0, pair.Key.IndexOf('#')) : pair.Key;
                    keySet.Add(baseKey);
                    if (!_preview.ContainsKey(baseKey)) _preview[baseKey] = pair.Value;
                }
            }
            _keys = new List<string>(keySet);
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
                if (path.EndsWith(".csv") || path.EndsWith(".asset")) { Invalidate(); return; }
        }
    }
}
