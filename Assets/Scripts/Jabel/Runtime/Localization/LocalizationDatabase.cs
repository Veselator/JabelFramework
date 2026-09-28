using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Jabel.Localization
{
    [Serializable]
    public class LanguageInfo
    {
        [Tooltip("ISO code used as the CSV column header: en, ru, de, pt-BR ...")]
        public string code = "en";
        [Tooltip("Name shown in the language picker, in the language itself: English, Русский.")]
        public string nativeName = "English";
        [Tooltip(".NET culture used for number formatting. Empty = same as code.")]
        public string cultureName;
        [Tooltip("Optional font for scripts the default font lacks (CJK, Arabic...).")]
        public TMP_FontAsset fontOverride;
        [Tooltip("System languages that should map to this one.")]
        public List<SystemLanguage> systemLanguages = new List<SystemLanguage>();
    }

    /// <summary>
    /// All languages and translation tables of a game. Tables are CSV files with a header row:
    /// key,en,ru,... Translators work in any spreadsheet app; multiple tables are merged (later wins).
    /// Plural forms use suffixes: key#one, key#few, key#many, key#other.
    /// </summary>
    [CreateAssetMenu(menuName = "Jabel/Localization Database", fileName = "LocalizationDatabase")]
    public class LocalizationDatabase : ScriptableObject
    {
        [SerializeField] private List<LanguageInfo> languages = new List<LanguageInfo> { new LanguageInfo() };
        [SerializeField] private string defaultLanguage = "en";
        [Tooltip("Pick the player's OS language on first launch.")]
        [SerializeField] private bool detectSystemLanguage = true;
        [SerializeField] private List<TextAsset> tables = new List<TextAsset>();

        public IReadOnlyList<LanguageInfo> Languages => languages;
        public string DefaultLanguage => defaultLanguage;
        public bool DetectSystemLanguage => detectSystemLanguage;
        public IReadOnlyList<TextAsset> Tables => tables;

        public LanguageInfo FindLanguage(string code)
        {
            foreach (var l in languages)
                if (string.Equals(l.code, code, StringComparison.OrdinalIgnoreCase)) return l;
            return null;
        }

        /// <summary>Builds language -> (key -> text) maps from all tables.</summary>
        public Dictionary<string, Dictionary<string, string>> BuildDictionaries()
        {
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var language in languages)
                result[language.code] = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var table in tables)
            {
                if (table == null) continue;
                var rows = CsvParser.Parse(table.text);
                if (rows.Count == 0) continue;

                var header = rows[0];
                for (int r = 1; r < rows.Count; r++)
                {
                    var row = rows[r];
                    if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0]) || row[0].StartsWith("//")) continue;
                    string key = row[0].Trim();
                    for (int c = 1; c < header.Count && c < row.Count; c++)
                    {
                        string code = header[c].Trim();
                        if (!result.TryGetValue(code, out var dict)) continue;
                        if (string.IsNullOrEmpty(row[c])) continue;
                        dict[key] = Unescape(row[c]);
                    }
                }
            }
            return result;
        }

        private static string Unescape(string value) => value.Replace("\\n", "\n").Replace("\\t", "\t");

#if UNITY_EDITOR
        public void EditorSetup(List<LanguageInfo> languageList, string defaultCode, List<TextAsset> tableList)
        {
            languages = languageList;
            defaultLanguage = defaultCode;
            tables = tableList;
        }
#endif
    }
}
