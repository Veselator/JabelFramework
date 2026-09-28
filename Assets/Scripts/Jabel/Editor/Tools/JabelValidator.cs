using System.Collections.Generic;
using System.Linq;
using Jabel.Buffs;
using Jabel.Core;
using Jabel.Scripting;
using UnityEditor;
using UnityEngine;

namespace Jabel.Editor
{
    /// <summary>
    /// Static checks for a clicker config: broken formulas, unknown identifiers, duplicate ids,
    /// buffs missing from the config and untranslated keys. Catches the typos that otherwise
    /// only show up as "why is my income zero?" at runtime.
    /// </summary>
    public static class JabelValidator
    {
        /// <summary>Locals provided by the framework in various hooks.</summary>
        private static readonly HashSet<string> KnownLocals = new HashSet<string>
        {
            "count", "amount", "level", "index", "seed", "n", "batch", "value", "critical", "seconds", "simulated", "i", "bought",
            "tick", "time", "tickDuration", "awaySeconds", "sessions"
        };

        [MenuItem("Tools/Jabel/Validate Configs", priority = 20)]
        public static void ValidateAll()
        {
            int issues = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:ClickerConfig"))
            {
                var config = AssetDatabase.LoadAssetAtPath<ClickerConfig>(AssetDatabase.GUIDToAssetPath(guid));
                issues += Validate(config);
            }
            if (issues == 0) Debug.Log("[Jabel] Validation passed: no issues found.");
            else Debug.LogWarning($"[Jabel] Validation found {issues} issue(s). See messages above.");
        }

        public static int Validate(ClickerConfig config)
        {
            int issues = 0;
            void Report(string message, Object context)
            {
                issues++;
                Debug.LogWarning($"[Jabel Validate] {config.name}: {message}", context);
            }

            // Known global names.
            var names = new HashSet<string>(KnownLocals);
            foreach (var v in config.variables)
            {
                if (string.IsNullOrEmpty(v.key)) Report("variable with empty key", config);
                else if (!names.Add(v.key) && !KnownLocals.Contains(v.key)) Report($"duplicate variable '{v.key}'", config);
            }
            foreach (var d in config.derivedValues)
                if (!string.IsNullOrEmpty(d.key) && !names.Add(d.key)) Report($"derived value '{d.key}' clashes with another name", config);

            // Collect locals declared by scripts so formulas that use them are not flagged.
            var allObjects = new List<Object> { config };
            allObjects.AddRange(config.AllBuffs);
            foreach (var obj in allObjects) CollectDeclaredNames(obj, names);

            // Buff ids.
            var ids = new Dictionary<string, BaseBuff>();
            foreach (var buff in config.AllBuffs)
            {
                if (ids.TryGetValue(buff.Id, out var other)) Report($"buff id '{buff.Id}' used by {other.name} and {buff.name}", buff);
                else ids[buff.Id] = buff;
                if (buff.DisplayName.IsEmpty) Report($"buff {buff.name} has no display name key", buff);
            }

            // Buffs present in the project but not referenced by the config.
            foreach (var guid in AssetDatabase.FindAssets("t:BaseBuff", new[] { System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(config)) }))
            {
                var buff = AssetDatabase.LoadAssetAtPath<BaseBuff>(AssetDatabase.GUIDToAssetPath(guid));
                if (buff != null && !config.AllBuffs.Contains(buff)) Report($"buff {buff.name} is not listed in the config", buff);
            }

            // Formulas and localization keys.
            foreach (var obj in allObjects)
            {
                var so = new SerializedObject(obj);
                var it = so.GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.String) continue;

                    if (it.name == "expression" && it.propertyPath.Contains("."))
                    {
                        string source = it.stringValue;
                        if (string.IsNullOrWhiteSpace(source)) continue;
                        var compiled = FormulaCompiler.Compile(source);
                        if (!compiled.IsValid)
                        {
                            Report($"{obj.name}.{it.propertyPath}: formula \"{source}\" — {compiled.Error}", obj);
                            continue;
                        }
                        foreach (var id in compiled.Identifiers)
                            if (!names.Contains(id) && !ids.ContainsKey(id))
                                Report($"{obj.name}.{it.propertyPath}: unknown identifier '{id}' in \"{source}\"", obj);
                    }
                    else if (it.name == "key" && it.propertyPath.EndsWith(".key") && IsLocalizedString(it))
                    {
                        if (!string.IsNullOrEmpty(it.stringValue) && !LocalizationEditorCache.HasKey(it.stringValue))
                            Report($"{obj.name}.{it.propertyPath}: missing translation key '{it.stringValue}'", obj);
                    }
                }
            }
            return issues;
        }

        private static bool IsLocalizedString(SerializedProperty keyProperty)
        {
            string parentPath = keyProperty.propertyPath.Substring(0, keyProperty.propertyPath.Length - 4);
            var parent = keyProperty.serializedObject.FindProperty(parentPath);
            return parent != null && parent.type == "LocalizedString";
        }

        /// <summary>Adds names written by Set/Declare/Modify blocks (locals and runtime globals).</summary>
        private static void CollectDeclaredNames(Object obj, HashSet<string> names)
        {
            var so = new SerializedObject(obj);
            var it = so.GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.String) continue;
                if ((it.name == "name" && it.propertyPath.Contains("target")) ||
                    (it.name == "name" && it.propertyPath.Contains("blocks")) ||
                    it.name == "indexLocal" || it.name == "resultLocal")
                {
                    if (!string.IsNullOrEmpty(it.stringValue)) names.Add(it.stringValue);
                }
            }
        }
    }
}
