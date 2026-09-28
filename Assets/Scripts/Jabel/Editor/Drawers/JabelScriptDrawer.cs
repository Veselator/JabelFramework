using System;
using System.Collections.Generic;
using System.Linq;
using Jabel.Scripting;
using UnityEditor;
using UnityEngine;

namespace Jabel.Editor
{
    /// <summary>
    /// Block editor for JabelScript: colored, collapsible blocks with enable toggles, reordering,
    /// duplication and a categorized "Add block" menu. Nested scripts (If/Repeat bodies) render
    /// recursively, which is what makes JabelScript feel like block programming inside the inspector.
    /// </summary>
    [CustomPropertyDrawer(typeof(JabelScript))]
    public class JabelScriptDrawer : PropertyDrawer
    {
        private const float HeaderHeight = 20f;
        private const float BlockHeaderHeight = 20f;
        private const float Padding = 4f;
        private const float BlockSpacing = 3f;
        private const float AddButtonHeight = 20f;

        private static List<(string path, Type type, JabelBlockAttribute info)> _blockTypes;
        private static GUIStyle _titleStyle, _summaryStyle;

        private static GUIStyle TitleStyle => _titleStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = Color.white },
            fontSize = 11
        };

        private static GUIStyle SummaryStyle => _summaryStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(1f, 1f, 1f, 0.75f) },
            clipping = TextClipping.Clip
        };

        private static IEnumerable<(string path, Type type, JabelBlockAttribute info)> BlockTypes
        {
            get
            {
                if (_blockTypes != null) return _blockTypes;
                _blockTypes = TypeCache.GetTypesDerivedFrom<JabelBlock>()
                    .Where(t => !t.IsAbstract && !t.IsGenericType && t.GetConstructor(Type.EmptyTypes) != null)
                    .Select(t =>
                    {
                        var info = (JabelBlockAttribute)Attribute.GetCustomAttribute(t, typeof(JabelBlockAttribute));
                        string path = info?.Path ?? "Other/" + ObjectNames.NicifyVariableName(t.Name.Replace("Block", ""));
                        return (path, t, info);
                    })
                    .OrderBy(x => x.path)
                    .ToList();
                return _blockTypes;
            }
        }

        private static JabelBlockAttribute InfoFor(Type type) =>
            type == null ? null : (JabelBlockAttribute)Attribute.GetCustomAttribute(type, typeof(JabelBlockAttribute));

        private static Color ColorFor(Type type)
        {
            var info = InfoFor(type);
            if (info != null && ColorUtility.TryParseHtmlString(info.Color, out var c)) return c;
            return new Color(0.36f, 0.42f, 0.48f);
        }

        // ------------------------------------------------------------ height

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = HeaderHeight;
            if (!property.isExpanded) return h;

            var blocks = property.FindPropertyRelative("blocks");
            h += Padding;
            for (int i = 0; i < blocks.arraySize; i++) h += BlockHeight(blocks.GetArrayElementAtIndex(i)) + BlockSpacing;
            h += AddButtonHeight + Padding * 2;
            return h;
        }

        private static float BlockHeight(SerializedProperty element)
        {
            float h = BlockHeaderHeight;
            if (!element.isExpanded || element.managedReferenceValue == null) return h;
            h += Padding;
            foreach (var child in Children(element))
                h += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
            return h + Padding;
        }

        private static IEnumerable<SerializedProperty> Children(SerializedProperty element)
        {
            var it = element.Copy();
            var end = element.GetEndProperty();
            bool enter = true;
            while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
            {
                enter = false;
                yield return it.Copy();
            }
        }

        // ------------------------------------------------------------ drawing

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var blocks = property.FindPropertyRelative("blocks");

            // Container background.
            var bg = new Rect(position.x, position.y, position.width, position.height);
            EditorGUI.DrawRect(bg, EditorGUIUtility.isProSkin ? new Color(0.16f, 0.16f, 0.17f) : new Color(0.78f, 0.78f, 0.8f));

            var header = new Rect(position.x + 2, position.y + 1, position.width - 4, HeaderHeight - 2);
            string title = $"{label.text}   ▸ {blocks.arraySize} block{(blocks.arraySize == 1 ? "" : "s")}";
            property.isExpanded = EditorGUI.Foldout(header, property.isExpanded, title, true, EditorStyles.foldoutHeader);

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            float y = position.y + HeaderHeight + Padding;
            float x = position.x + 12;
            float width = position.width - 16;
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            int removeAt = -1, moveFrom = -1, moveTo = -1, duplicateAt = -1;
            for (int i = 0; i < blocks.arraySize; i++)
            {
                var element = blocks.GetArrayElementAtIndex(i);
                float h = BlockHeight(element);
                var rect = new Rect(x, y, width, h);
                DrawBlock(rect, element, i, blocks.arraySize, ref removeAt, ref moveFrom, ref moveTo, ref duplicateAt);
                y += h + BlockSpacing;
            }

            var addRect = new Rect(x, y + Padding, Mathf.Min(160, width), AddButtonHeight);
            if (GUI.Button(addRect, "+ Add block", EditorStyles.miniButton)) ShowAddMenu(blocks, blocks.arraySize);

            EditorGUI.indentLevel = indent;

            // Structural edits are applied after drawing to keep indices stable during the loop.
            if (removeAt >= 0)
            {
                blocks.GetArrayElementAtIndex(removeAt).managedReferenceValue = null;
                blocks.DeleteArrayElementAtIndex(removeAt);
            }
            else if (moveFrom >= 0 && moveTo >= 0 && moveTo < blocks.arraySize)
            {
                blocks.MoveArrayElement(moveFrom, moveTo);
            }
            else if (duplicateAt >= 0)
            {
                var source = blocks.GetArrayElementAtIndex(duplicateAt).managedReferenceValue;
                if (source != null)
                {
                    var copy = JsonUtility.FromJson(JsonUtility.ToJson(source), source.GetType());
                    blocks.InsertArrayElementAtIndex(duplicateAt + 1);
                    blocks.GetArrayElementAtIndex(duplicateAt + 1).managedReferenceValue = copy;
                }
            }

            EditorGUI.EndProperty();
        }

        private static void DrawBlock(Rect rect, SerializedProperty element, int index, int count,
            ref int removeAt, ref int moveFrom, ref int moveTo, ref int duplicateAt)
        {
            var value = element.managedReferenceValue as JabelBlock;
            var type = value?.GetType();
            var color = ColorFor(type);

            var enabledProp = element.FindPropertyRelative("enabled");
            bool isEnabled = enabledProp == null || enabledProp.boolValue;
            if (!isEnabled) color = Color.Lerp(color, Color.gray, 0.7f);

            // Body + colored header.
            var body = new Color(color.r * 0.35f, color.g * 0.35f, color.b * 0.35f, 1f);
            if (!EditorGUIUtility.isProSkin) body = Color.Lerp(color, Color.white, 0.8f);
            EditorGUI.DrawRect(rect, body);
            var headerRect = new Rect(rect.x, rect.y, rect.width, BlockHeaderHeight);
            EditorGUI.DrawRect(headerRect, color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height), color * 1.3f);

            // Header controls.
            var foldRect = new Rect(headerRect.x + 4, headerRect.y + 2, 14, 16);
            element.isExpanded = EditorGUI.Foldout(foldRect, element.isExpanded, GUIContent.none, true);

            if (enabledProp != null)
            {
                var toggleRect = new Rect(headerRect.x + 18, headerRect.y + 2, 16, 16);
                enabledProp.boolValue = EditorGUI.Toggle(toggleRect, enabledProp.boolValue);
            }

            string name = value == null ? "<missing block type>" : BlockName(type);
            var info = InfoFor(type);
            var titleRect = new Rect(headerRect.x + 38, headerRect.y + 1, 150, 18);
            GUI.Label(titleRect, new GUIContent(name, info?.Help), TitleStyle);

            float buttons = 4 * 20;
            float summaryX = titleRect.x + Mathf.Min(150, TitleStyle.CalcSize(new GUIContent(name)).x + 8);
            var summaryRect = new Rect(summaryX, headerRect.y + 2, Mathf.Max(0, headerRect.xMax - buttons - summaryX - 4), 16);
            if (value != null && !element.isExpanded)
            {
                string summary;
                try { summary = value.Describe(); }
                catch { summary = string.Empty; }
                GUI.Label(summaryRect, summary, SummaryStyle);
            }

            float bx = headerRect.xMax - buttons;
            if (GUI.Button(new Rect(bx, headerRect.y + 1, 20, 18), new GUIContent("▲", "Move up"), EditorStyles.miniButtonLeft) && index > 0)
            { moveFrom = index; moveTo = index - 1; }
            if (GUI.Button(new Rect(bx + 20, headerRect.y + 1, 20, 18), new GUIContent("▼", "Move down"), EditorStyles.miniButtonMid) && index < count - 1)
            { moveFrom = index; moveTo = index + 1; }
            if (GUI.Button(new Rect(bx + 40, headerRect.y + 1, 20, 18), new GUIContent("⧉", "Duplicate"), EditorStyles.miniButtonMid))
                duplicateAt = index;
            if (GUI.Button(new Rect(bx + 60, headerRect.y + 1, 20, 18), new GUIContent("✕", "Delete"), EditorStyles.miniButtonRight))
                removeAt = index;

            if (!element.isExpanded || value == null) return;

            // Block fields.
            float y = headerRect.yMax + Padding;
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Min(120, rect.width * 0.35f);
            foreach (var child in Children(element))
            {
                float h = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(new Rect(rect.x + 8, y, rect.width - 12, h), child, true);
                y += h + EditorGUIUtility.standardVerticalSpacing;
            }
            EditorGUIUtility.labelWidth = labelWidth;
        }

        private static string BlockName(Type type)
        {
            var info = InfoFor(type);
            if (info != null)
            {
                int slash = info.Path.LastIndexOf('/');
                return slash >= 0 ? info.Path.Substring(slash + 1) : info.Path;
            }
            return ObjectNames.NicifyVariableName(type.Name.Replace("Block", ""));
        }

        private static void ShowAddMenu(SerializedProperty blocks, int insertAt)
        {
            var menu = new GenericMenu();
            var so = blocks.serializedObject;
            string path = blocks.propertyPath;
            foreach (var entry in BlockTypes)
            {
                var type = entry.type;
                menu.AddItem(new GUIContent(entry.path, entry.info?.Help), false, () =>
                {
                    so.Update();
                    var list = so.FindProperty(path);
                    list.arraySize++;
                    var element = list.GetArrayElementAtIndex(list.arraySize - 1);
                    element.managedReferenceValue = Activator.CreateInstance(type);
                    element.isExpanded = true;
                    so.ApplyModifiedProperties();
                });
            }
            menu.ShowAsContext();
        }
    }
}
