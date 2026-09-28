using System;
using System.Text;
using Jabel.Buffs;
using Jabel.Core;
using Jabel.Events;
using Jabel.Numbers;
using Jabel.Save;
using Jabel.Scripting;
using UnityEngine;

namespace OneKMonkeys
{
    /// <summary>Raised after code was written (by a click, a monkey or offline catch-up).</summary>
    public struct CodeWrittenEvent : IJabelEvent
    {
        public BigNumber Chars;
        public BigNumber Lines;
        public BigNumber Money;
        public BuffInstance Instance;
        public bool IsOffline;
    }

    /// <summary>
    /// The shared program everybody types. Exposes the "WriteCode(amount)" function to JabelScript:
    /// it advances the cursor through the code database, counts paid characters and completed lines,
    /// then runs the data-driven <see cref="onWritten"/> script (money, levels...) with locals
    /// chars, lines and files.
    /// </summary>
    [AddComponentMenu("1000 Monkeys/Code Writer")]
    public class CodeWriter : JabelBehaviour, ISaveParticipant
    {
        [SerializeField] private CodeDatabase database;
        [SerializeField] private string functionName = "WriteCode";
        [Tooltip("Variable used to measure the income of each write (shown in floating texts).")]
        [SerializeField] private string moneyVariable = "money";
        [Tooltip("Runs after each write. Locals: chars, lines, files (completed files).")]
        [SerializeField] private JabelScript onWritten = new JabelScript();

        [Serializable]
        private class State
        {
            public int page;
            public int position;
            public double stream;
            public string totalChars = "0";
            public string totalLines = "0";
            public double completedFiles;
        }

        private int _page;
        private int _position;

        /// <summary>Total characters (including free ones) ever written. Views diff it to animate new text.</summary>
        public double StreamPosition { get; private set; }
        public BigNumber TotalChars { get; private set; }
        public BigNumber TotalLines { get; private set; }
        public double CompletedFiles { get; private set; }
        public CodeDatabase Database => database;
        public string CurrentFileName => database.Pages[_page].Name;

        public string SaveKey => "code_writer";

        protected override void OnBind()
        {
            Manager.Functions.Register(functionName, OnWriteCode);
            Manager.Save.Register(this);
        }

        protected override void OnUnbind()
        {
            Manager.Functions.Unregister(functionName, OnWriteCode);
        }

        private void OnWriteCode(JabelContext context, FunctionArgs args)
        {
            var amount = args.Get("amount", args.First(BigNumber.One));
            if (amount <= BigNumber.Zero) return;

            var moneyBefore = Manager.Variables.Get(moneyVariable);
            Advance(amount.ToDoubleClamped(), out double chars, out double lines, out double files);

            var ctx = context.CreateChild();
            ctx.Batch = 1; // amount already contains the batch
            try
            {
                ctx.SetLocal("chars", chars);
                ctx.SetLocal("lines", lines);
                ctx.SetLocal("files", files);
                onWritten.Run(ctx);
            }
            finally
            {
                ctx.Release();
            }

            var earned = Manager.Variables.Get(moneyVariable) - moneyBefore;
            // Returned value = income of this write, so click feedback scales with money, not characters.
            args.Return(earned);
            Manager.Events.Publish(new CodeWrittenEvent
            {
                Chars = chars,
                Lines = lines,
                Money = earned,
                Instance = context.Instance,
                IsOffline = context.IsOffline
            });
        }

        /// <summary>Consumes <paramref name="amount"/> paid characters. Free characters after them are included.</summary>
        public void Advance(double amount, out double chars, out double lines, out double files)
        {
            chars = 0;
            lines = 0;
            files = 0;
            var pages = database.Pages;
            double remaining = Math.Floor(amount);
            if (remaining <= 0) return;

            // Whole cycles through the database are skipped arithmetically (huge offline batches).
            double cycleReal = database.TotalReal;
            if (cycleReal > 0 && remaining > cycleReal * 2)
            {
                double cycles = Math.Floor(remaining / cycleReal) - 1;
                chars += cycles * cycleReal;
                lines += cycles * database.TotalLines;
                files += cycles * pages.Count;
                StreamPosition += cycles * TotalLength(pages);
                remaining -= cycles * cycleReal;
            }

            int guard = 0;
            while (remaining > 0 && guard++ < 100000)
            {
                var page = pages[_page];
                int realHere = page.RealTotal - page.RealPrefix[_position];
                if (remaining >= realHere)
                {
                    // Finish this file and move to the next one.
                    chars += realHere;
                    lines += page.LineTotal - page.LinePrefix[_position];
                    StreamPosition += page.Text.Length - _position;
                    remaining -= realHere;
                    files++;
                    _page = (_page + 1) % pages.Count;
                    _position = 0;
                    page = pages[_page];
                    SkipFree(page, ref lines);
                    continue;
                }

                // Binary search the first position that contains enough paid characters.
                int target = page.RealPrefix[_position] + (int)remaining;
                int lo = _position, hi = page.Text.Length;
                while (lo < hi)
                {
                    int mid = (lo + hi) / 2;
                    if (page.RealPrefix[mid] >= target) hi = mid;
                    else lo = mid + 1;
                }
                chars += remaining;
                lines += page.LinePrefix[lo] - page.LinePrefix[_position];
                StreamPosition += lo - _position;
                _position = lo;
                remaining = 0;
                SkipFree(page, ref lines);
            }

            TotalChars += chars;
            TotalLines += lines;
            CompletedFiles += files;
        }

        /// <summary>Free characters following the cursor are written immediately.</summary>
        private void SkipFree(CodeDatabase.Page page, ref double lines)
        {
            int start = _position;
            while (_position < page.Text.Length && CodeDatabase.IsFree(page.Text[_position])) _position++;
            lines += page.LinePrefix[_position] - page.LinePrefix[start];
            StreamPosition += _position - start;
        }

        private static double TotalLength(System.Collections.Generic.IReadOnlyList<CodeDatabase.Page> pages)
        {
            double total = 0;
            foreach (var p in pages) total += p.Text.Length;
            return total;
        }

        /// <summary>Returns the last <paramref name="count"/> written characters (crossing file boundaries).</summary>
        public string GetTail(int count)
        {
            var pages = database.Pages;
            count = (int)Math.Min(count, StreamPosition);
            if (count <= 0) return string.Empty;

            var parts = new System.Collections.Generic.List<string>();
            int page = _page, end = _position, need = count;
            int guard = 0;
            while (need > 0 && guard++ < pages.Count * 4)
            {
                int take = Math.Min(need, end);
                if (take > 0) parts.Add(pages[page].Text.Substring(end - take, take));
                need -= take;
                page = (page - 1 + pages.Count) % pages.Count;
                end = pages[page].Text.Length;
            }
            var sb = new StringBuilder(count);
            for (int i = parts.Count - 1; i >= 0; i--) sb.Append(parts[i]);
            return sb.ToString();
        }

        // ------------------------------------------------------------ save

        public string CaptureState() => JsonUtility.ToJson(new State
        {
            page = _page,
            position = _position,
            stream = StreamPosition,
            totalChars = TotalChars.ToInvariantString(),
            totalLines = TotalLines.ToInvariantString(),
            completedFiles = CompletedFiles
        });

        public void RestoreState(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            if (state == null) return;
            var pages = database.Pages;
            _page = Mathf.Clamp(state.page, 0, pages.Count - 1);
            _position = Mathf.Clamp(state.position, 0, pages[_page].Text.Length);
            StreamPosition = state.stream;
            TotalChars = BigNumber.Parse(state.totalChars);
            TotalLines = BigNumber.Parse(state.totalLines);
            CompletedFiles = state.completedFiles;
        }

        public void ResetState(bool isRunReset)
        {
            _page = 0;
            _position = 0;
            StreamPosition = 0;
            TotalChars = BigNumber.Zero;
            TotalLines = BigNumber.Zero;
            CompletedFiles = 0;
        }

#if UNITY_EDITOR
        public void EditorSetup(CodeDatabase codeDatabase, JabelScript written)
        {
            database = codeDatabase;
            onWritten = written;
        }
#endif
    }
}
