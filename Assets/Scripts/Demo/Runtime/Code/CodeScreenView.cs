using System.Collections.Generic;
using System.Text;
using Jabel.Core;
using TMPro;
using UnityEngine;

namespace OneKMonkeys
{
    /// <summary>
    /// Renders the shared program on a monitor: new characters slide up and fade in,
    /// free characters (whitespace, punctuation) appear together with the character typed before them,
    /// lines scroll up smoothly behind a mask, and a tiny tokenizer adds syntax colors. Only a window of recent lines is kept in the mesh.
    /// </summary>
    [AddComponentMenu("1000 Monkeys/Code Screen View")]
    public class CodeScreenView : JabelBehaviour
    {
        [SerializeField] private CodeWriter writer;
        [SerializeField] private TMP_Text text;
        [Tooltip("Mask rect; its height defines how many lines are visible.")]
        [SerializeField] private RectTransform viewport;
        [SerializeField] private int maxLines = 40;
        [SerializeField] private float charAnimTime = 0.28f;
        [Tooltip("Delay between consecutive new characters (capped so bursts never lag behind).")]
        [SerializeField] private float stagger = 0.02f;
        [SerializeField] private float maxRevealTime = 0.35f;
        [Tooltip("How far new characters slide up, in font heights.")]
        [SerializeField] private float flyDistance = 0.7f;
        [SerializeField] private float scrollSmoothing = 12f;

        [Header("Syntax colors")]
        [SerializeField] private Color plainColor = new Color(0.78f, 1f, 0.8f);
        [SerializeField] private Color keywordColor = new Color(0.5f, 0.86f, 1f);
        [SerializeField] private Color stringColor = new Color(1f, 0.82f, 0.45f);
        [SerializeField] private Color commentColor = new Color(0.45f, 0.7f, 0.45f);
        [SerializeField] private Color numberColor = new Color(1f, 0.6f, 0.45f);
        [SerializeField] private Color punctuationColor = new Color(0.62f, 0.8f, 0.62f);

        private static readonly HashSet<string> Keywords = new HashSet<string>
        {
            "if", "else", "for", "foreach", "while", "do", "return", "break", "continue", "switch", "case", "default",
            "class", "struct", "interface", "enum", "public", "private", "protected", "internal", "static", "readonly",
            "const", "void", "int", "float", "double", "bool", "string", "var", "new", "null", "true", "false", "this",
            "using", "namespace", "def", "import", "from", "as", "in", "not", "and", "or", "None", "True", "False",
            "function", "let", "async", "await", "yield", "lambda", "self", "try", "catch", "finally", "throw",
            "fn", "mut", "impl", "pub", "loop", "match", "print", "println", "elif", "pass", "with", "is", "long",
            "char", "include", "main", "printf", "sizeof", "unsigned", "SELECT", "FROM", "WHERE"
        };

        private enum CharClass : byte { Plain, Keyword, String, Comment, Number, Punctuation }

        private readonly StringBuilder _buffer = new StringBuilder(4096);
        private readonly List<float> _appear = new List<float>(4096);
        private CharClass[] _classes = new CharClass[4096];
        private double _shownStream;
        private float _nextReveal;
        private bool _dirty;
        private float _scroll;
        private float _lineAdvance = 30f;
        private float _contentHeight;
        private float _topMargin;
        private bool _marginCaptured;
        private RectTransform _textRect;
        private TMP_MeshInfo[] _cachedMeshInfo;

        public CodeWriter Writer
        {
            get => writer;
            set => writer = value;
        }

        protected override void OnBind()
        {
            _textRect = text.rectTransform;
            if (!_marginCaptured)
            {
                // The designed distance between the viewport top and the text is kept while scrolling.
                _topMargin = Mathf.Max(0, -_textRect.anchoredPosition.y);
                _marginCaptured = true;
            }
            text.richText = false;
            // Code contains literal "\n" sequences; they must not become line breaks (indices must match the buffer).
            text.parseCtrlCharacters = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            Resync();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            // Screens get hidden; catch up instantly when shown again.
            if (IsBound) Resync();
        }

        private void Resync()
        {
            if (writer == null) return;
            _buffer.Clear();
            _appear.Clear();
            string tail = writer.GetTail(maxLines * 60);
            TrimToLastLines(ref tail, maxLines);
            AppendInstant(tail);
            _shownStream = writer.StreamPosition;
            _dirty = true;
            _scroll = -1; // snap
        }

        private static void TrimToLastLines(ref string s, int lines)
        {
            int count = 0;
            for (int i = s.Length - 1; i >= 0; i--)
            {
                if (s[i] != '\n') continue;
                if (++count >= lines)
                {
                    s = s.Substring(i + 1);
                    return;
                }
            }
        }

        private void AppendInstant(string s)
        {
            _buffer.Append(s);
            for (int i = 0; i < s.Length; i++) _appear.Add(-100f);
        }

        private void Update()
        {
            if (!IsBound || writer == null) return;
            float now = Time.time;

            double delta = writer.StreamPosition - _shownStream;
            if (delta > 0)
            {
                if (delta > maxLines * 60)
                {
                    Resync();
                }
                else
                {
                    string fresh = writer.GetTail((int)delta);
                    float start = Mathf.Max(now, _nextReveal);
                    // Cap the reveal so big writes never queue up behind the typing animation.
                    float step = Mathf.Min(stagger, maxRevealTime / Mathf.Max(1, fresh.Length));
                    // Free characters (spaces, punctuation) appear together with the typed character before them.
                    float previous = _appear.Count > 0 ? _appear[_appear.Count - 1] : now;
                    for (int i = 0; i < fresh.Length; i++)
                    {
                        char c = fresh[i];
                        _buffer.Append(c);
                        if (CodeDatabase.IsFree(c))
                        {
                            _appear.Add(previous);
                            continue;
                        }
                        _appear.Add(start);
                        previous = start;
                        start += step;
                    }
                    _nextReveal = Mathf.Min(start, now + maxRevealTime);
                    _shownStream = writer.StreamPosition;
                    TrimLines();
                    _dirty = true;
                }
            }
        }

        private void TrimLines()
        {
            int lines = 0;
            for (int i = 0; i < _buffer.Length; i++) if (_buffer[i] == '\n') lines++;
            int excess = lines - maxLines;
            if (excess <= 0) return;

            int cut = 0, seen = 0;
            while (cut < _buffer.Length && seen < excess)
            {
                if (_buffer[cut] == '\n') seen++;
                cut++;
            }
            _buffer.Remove(0, cut);
            _appear.RemoveRange(0, cut);
            // Keep the visual position continuous: removed lines were above the view.
            if (_scroll > 0) _scroll -= excess * _lineAdvance;
        }

        private void LateUpdate()
        {
            if (!IsBound || text == null) return;

            if (_dirty)
            {
                _dirty = false;
                text.text = _buffer.ToString() + "_";
                text.ForceMeshUpdate();
                _cachedMeshInfo = text.textInfo.CopyMeshInfoVertexData();
                Classify();
                var info = text.textInfo;
                // Real extents (including line spacing): from the rect top to the last line's descender.
                int lines = info.lineCount;
                _contentHeight = lines > 0 ? -info.lineInfo[lines - 1].descender : 0;
                if (lines > 1) _lineAdvance = info.lineInfo[0].baseline - info.lineInfo[1].baseline;
                else if (lines == 1) _lineAdvance = info.lineInfo[0].lineHeight;
            }

            UpdateScroll();
            AnimateCharacters();
        }

        private void UpdateScroll()
        {
            float viewHeight = viewport != null ? viewport.rect.height : ((RectTransform)transform).rect.height;
            // Same margin above the first and below the last visible line.
            float overflow = Mathf.Max(0, _contentHeight - (viewHeight - _topMargin * 2));
            // Scroll by whole lines so the top line is never cut in half.
            float target = _lineAdvance > 0 ? Mathf.Ceil(overflow / _lineAdvance - 0.01f) * _lineAdvance : overflow;
            if (_scroll < 0) _scroll = target;
            _scroll = Mathf.Lerp(_scroll, target, 1 - Mathf.Exp(-scrollSmoothing * Time.deltaTime));
            var pos = _textRect.anchoredPosition;
            pos.y = -_topMargin + _scroll;
            _textRect.anchoredPosition = pos;
        }

        private void AnimateCharacters()
        {
            var info = text.textInfo;
            if (_cachedMeshInfo == null || info.characterCount == 0) return;
            float now = Time.time;
            bool caretOn = (now % 1f) < 0.55f;

            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible) continue;

                int mat = ch.materialReferenceIndex;
                int v = ch.vertexIndex;
                var src = _cachedMeshInfo[mat].vertices;
                var dst = info.meshInfo[mat].vertices;
                var colors = info.meshInfo[mat].colors32;

                bool isCaret = i == info.characterCount - 1 && ch.character == '_';
                float p = isCaret || i >= _appear.Count ? 1 : Mathf.Clamp01((now - _appear[i]) / charAnimTime);

                Color color = isCaret ? plainColor : ColorFor(i < _classes.Length ? _classes[i] : CharClass.Plain);

                // New characters slide up from below the baseline and fade in; nothing is scaled,
                // so the rest of the text stays perfectly still.
                float ease = 1 - (1 - p) * (1 - p) * (1 - p);
                Vector3 offset = new Vector3(0, -(1 - ease) * flyDistance * text.fontSize, 0);
                color.a = isCaret ? (caretOn ? 1 : 0) : ease;

                for (int k = 0; k < 4; k++)
                {
                    dst[v + k] = src[v + k] + offset;
                    colors[v + k] = color;
                }
            }

            // Only meshes in use: stale sub-meshes (e.g. a fallback font no longer needed) must stay hidden.
            for (int m = 0; m < info.materialCount && m < info.meshInfo.Length; m++)
            {
                info.meshInfo[m].mesh.vertices = info.meshInfo[m].vertices;
                info.meshInfo[m].mesh.colors32 = info.meshInfo[m].colors32;
                text.UpdateGeometry(info.meshInfo[m].mesh, m);
            }
        }

        private Color ColorFor(CharClass c)
        {
            switch (c)
            {
                case CharClass.Keyword: return keywordColor;
                case CharClass.String: return stringColor;
                case CharClass.Comment: return commentColor;
                case CharClass.Number: return numberColor;
                case CharClass.Punctuation: return punctuationColor;
                default: return plainColor;
            }
        }

        /// <summary>Tiny single-pass tokenizer: comments, strings, numbers, keywords, punctuation.</summary>
        private void Classify()
        {
            int n = _buffer.Length;
            if (_classes.Length < n + 1) _classes = new CharClass[Mathf.NextPowerOfTwo(n + 1)];

            int i = 0;
            while (i < n)
            {
                char c = _buffer[i];
                if ((c == '/' && i + 1 < n && _buffer[i + 1] == '/') || c == '#')
                {
                    while (i < n && _buffer[i] != '\n') _classes[i++] = CharClass.Comment;
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    char quote = c;
                    _classes[i++] = CharClass.String;
                    while (i < n && _buffer[i] != quote && _buffer[i] != '\n') _classes[i++] = CharClass.String;
                    if (i < n && _buffer[i] == quote) _classes[i++] = CharClass.String;
                    continue;
                }
                if (char.IsDigit(c))
                {
                    while (i < n && (char.IsLetterOrDigit(_buffer[i]) || _buffer[i] == '.')) _classes[i++] = CharClass.Number;
                    continue;
                }
                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < n && (char.IsLetterOrDigit(_buffer[i]) || _buffer[i] == '_')) i++;
                    var kind = Keywords.Contains(_buffer.ToString(start, i - start)) ? CharClass.Keyword : CharClass.Plain;
                    for (int k = start; k < i; k++) _classes[k] = kind;
                    continue;
                }
                _classes[i++] = char.IsPunctuation(c) || char.IsSymbol(c) ? CharClass.Punctuation : CharClass.Plain;
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(CodeWriter codeWriter, TMP_Text label, RectTransform mask)
        {
            writer = codeWriter;
            text = label;
            viewport = mask;
        }
#endif
    }
}
