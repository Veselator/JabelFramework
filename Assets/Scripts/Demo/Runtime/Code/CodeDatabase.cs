using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneKMonkeys
{
    /// <summary>
    /// The texts the monkeys "type". Files are written one after another; after the last one the
    /// cycle starts again. Whitespace and punctuation are free: they appear instantly and cost nothing.
    /// </summary>
    [CreateAssetMenu(menuName = "1000 Monkeys/Code Database", fileName = "CodeDatabase")]
    public class CodeDatabase : ScriptableObject
    {
        [Serializable]
        public class CodeFile
        {
            public string fileName = "main.cs";
            public TextAsset content;
        }

        [SerializeField] private List<CodeFile> files = new List<CodeFile>();
        [Tooltip("Header written before each file. {0} = file name.")]
        [SerializeField] private string header = "// ===== {0} =====\n";

        /// <summary>Preprocessed file: full text plus prefix sums for O(log n) seeking.</summary>
        public sealed class Page
        {
            public string Name;
            public string Text;
            /// <summary>RealPrefix[i] = number of paid characters in Text[0..i).</summary>
            public int[] RealPrefix;
            /// <summary>LinePrefix[i] = number of '\n' in Text[0..i).</summary>
            public int[] LinePrefix;
            public int RealTotal => RealPrefix[RealPrefix.Length - 1];
            public int LineTotal => LinePrefix[LinePrefix.Length - 1];
        }

        [NonSerialized] private List<Page> _pages;

        /// <summary>Configured files in writing order.</summary>
        public IReadOnlyList<CodeFile> Files => files;

        public IReadOnlyList<Page> Pages
        {
            get
            {
                if (_pages == null) Build();
                return _pages;
            }
        }

        public double TotalReal { get; private set; }
        public double TotalLines { get; private set; }

        /// <summary>Spaces, tabs, newlines and punctuation are typed instantly and for free.</summary>
        /// <summary>
        /// Free characters cost nothing and are written together with the paid character before them.
        /// Brackets are never free: they carry the structure of the code.
        /// </summary>
        public static bool IsFree(char c) => (char.IsWhiteSpace(c) || char.IsPunctuation(c)) && !IsBracket(c);

        public static bool IsBracket(char c) => c == '(' || c == ')' || c == '[' || c == ']' || c == '{' || c == '}';

        private void OnEnable() => _pages = null;

        private void Build()
        {
            _pages = new List<Page>();
            TotalReal = 0;
            TotalLines = 0;
            foreach (var file in files)
            {
                if (file?.content == null) continue;
                string text = string.Format(header, file.fileName) + file.content.text.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd() + "\n\n";
                var page = new Page { Name = file.fileName, Text = text, RealPrefix = new int[text.Length + 1], LinePrefix = new int[text.Length + 1] };
                for (int i = 0; i < text.Length; i++)
                {
                    page.RealPrefix[i + 1] = page.RealPrefix[i] + (IsFree(text[i]) ? 0 : 1);
                    page.LinePrefix[i + 1] = page.LinePrefix[i] + (text[i] == '\n' ? 1 : 0);
                }
                TotalReal += page.RealTotal;
                TotalLines += page.LineTotal;
                _pages.Add(page);
            }

            if (_pages.Count == 0)
            {
                const string fallback = "// no code files assigned\nprint(\"hello, monkey\");\n\n";
                var page = new Page { Name = "empty", Text = fallback, RealPrefix = new int[fallback.Length + 1], LinePrefix = new int[fallback.Length + 1] };
                for (int i = 0; i < fallback.Length; i++)
                {
                    page.RealPrefix[i + 1] = page.RealPrefix[i] + (IsFree(fallback[i]) ? 0 : 1);
                    page.LinePrefix[i + 1] = page.LinePrefix[i] + (fallback[i] == '\n' ? 1 : 0);
                }
                TotalReal = page.RealTotal;
                TotalLines = page.LineTotal;
                _pages.Add(page);
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(List<CodeFile> codeFiles)
        {
            files = codeFiles;
            _pages = null;
        }
#endif
    }
}
