using System;
using System.Collections.Generic;
using Jabel.Numbers;

namespace Jabel.Scripting
{
    public delegate BigNumber FormulaFunction(FormulaCall call);

    /// <summary>
    /// Registry of functions callable from formulas: "min(a, b)", "count('monkey')", "rate('money')"...
    /// Games can register their own (e.g. a lookup into a balance table) via <see cref="Register"/>.
    /// </summary>
    public static class FormulaFunctions
    {
        public sealed class Entry
        {
            public string Name;
            public FormulaFunction Function;
            /// <summary>Result may differ between calls with equal inputs (random, time).</summary>
            public bool Volatile;
            /// <summary>Result depends only on arguments (allows constant folding).</summary>
            public bool Pure;
            public string Signature;
            public string Description;
        }

        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private static readonly System.Random Random = new System.Random();

        /// <summary>Bumped on each registration so compiled call nodes re-resolve.</summary>
        public static int Version { get; private set; }

        public static IEnumerable<Entry> All => Entries.Values;

        static FormulaFunctions()
        {
            RegisterBuiltins();
        }

        public static void Register(string name, FormulaFunction function, string signature = null, string description = null,
            bool isVolatile = false, bool pure = false)
        {
            Entries[name] = new Entry
            {
                Name = name, Function = function, Volatile = isVolatile, Pure = pure,
                Signature = signature ?? name + "(...)", Description = description ?? string.Empty
            };
            Version++;
        }

        public static Entry Find(string name) => Entries.TryGetValue(name, out var e) ? e : null;

        private static BigNumber B(bool v) => v ? BigNumber.One : BigNumber.Zero;

        private static void RegisterBuiltins()
        {
            // ----- math (pure)
            Register("min", c =>
            {
                var r = c.Arg(0);
                for (int i = 1; i < c.Count; i++) r = BigNumber.Min(r, c.Arg(i));
                return r;
            }, "min(a, b, ...)", "Smallest argument.", pure: true);
            Register("max", c =>
            {
                var r = c.Arg(0);
                for (int i = 1; i < c.Count; i++) r = BigNumber.Max(r, c.Arg(i));
                return r;
            }, "max(a, b, ...)", "Largest argument.", pure: true);
            Register("clamp", c => BigNumber.Clamp(c.Arg(0), c.Arg(1), c.Arg(2)), "clamp(x, min, max)", "Limits x to a range.", pure: true);
            Register("floor", c => BigNumber.Floor(c.Arg(0)), "floor(x)", "Rounds down.", pure: true);
            Register("ceil", c => BigNumber.Ceil(c.Arg(0)), "ceil(x)", "Rounds up.", pure: true);
            Register("round", c => BigNumber.Round(c.Arg(0)), "round(x)", "Rounds to nearest integer.", pure: true);
            Register("abs", c => BigNumber.Abs(c.Arg(0)), "abs(x)", "Absolute value.", pure: true);
            Register("sqrt", c => BigNumber.Sqrt(c.Arg(0)), "sqrt(x)", "Square root.", pure: true);
            Register("pow", c => BigNumber.Pow(c.Arg(0), c.Arg(1)), "pow(x, p)", "x to the power p.", pure: true);
            Register("log10", c => c.Arg(0).Log10(), "log10(x)", "Base-10 logarithm.", pure: true);
            Register("ln", c => c.Arg(0).Ln(), "ln(x)", "Natural logarithm.", pure: true);
            Register("log", c =>
            {
                double baseLog = c.Count > 1 ? c.Arg(1).Log10() : Math.Log10(Math.E);
                return baseLog == 0 ? BigNumber.Zero : new BigNumber(c.Arg(0).Log10() / baseLog);
            }, "log(x, base)", "Logarithm (natural when base omitted).", pure: true);
            Register("lerp", c =>
            {
                var a = c.Arg(0); var b = c.Arg(1);
                return a + (b - a) * c.Arg(2);
            }, "lerp(a, b, t)", "Linear interpolation.", pure: true);
            Register("if", c => c.Arg(0).ToBool() ? c.Arg(1) : c.Arg(2), "if(cond, a, b)", "Conditional value.", pure: true);
            Register("geomsum", c => BigNumber.GeometricSum(c.Arg(0), c.ArgDouble(1), c.ArgDouble(2)),
                "geomsum(first, ratio, n)", "Total price of n items with geometric price growth.", pure: true);
            Register("softcap", c =>
            {
                // Values above cap grow with the given power (typical diminishing returns).
                var x = c.Arg(0); var cap = c.Arg(1); double power = c.ArgDouble(2, 0.5);
                if (x <= cap || cap <= BigNumber.Zero) return x;
                return cap * BigNumber.Pow(x / cap, power);
            }, "softcap(x, cap, power)", "Diminishing returns above cap.", pure: true);

            // ----- randomness (volatile)
            Register("random", c =>
            {
                double min = c.Count > 1 ? c.ArgDouble(0) : 0;
                double max = c.Count > 1 ? c.ArgDouble(1) : (c.Count == 1 ? c.ArgDouble(0) : 1);
                return min + Random.NextDouble() * (max - min);
            }, "random(min, max)", "Random real number.", isVolatile: true);
            Register("randomInt", c =>
            {
                int min = (int)c.ArgDouble(0);
                int max = (int)c.ArgDouble(1);
                return Random.Next(min, max + 1);
            }, "randomInt(min, max)", "Random integer, inclusive.", isVolatile: true);
            Register("chance", c => B(Random.NextDouble() < c.ArgDouble(0)), "chance(p)", "1 with probability p (0..1), else 0.", isVolatile: true);

            // ----- runtime queries
            Register("count", c => c.Context?.Runtime?.Buffs?.GetCount(c.StringArg(0)) ?? 0,
                "count('buffId')", "How many of a buff the player owns.");
            Register("owned", c => B((c.Context?.Runtime?.Buffs?.GetCount(c.StringArg(0)) ?? 0) > 0),
                "owned('buffId')", "1 when at least one is owned.");
            Register("unlocked", c => B(c.Context?.Runtime?.Buffs?.IsUnlocked(c.StringArg(0)) ?? false),
                "unlocked('buffId')", "1 when the buff has been unlocked.");
            Register("totalLevels", c => c.Context?.Runtime?.Buffs?.GetTotalLevels(c.StringArg(0)) ?? 0,
                "totalLevels('buffId')", "Sum of levels of all instances of an active buff.");
            Register("maxLevel", c => c.Context?.Runtime?.Buffs?.GetHighestLevel(c.StringArg(0)) ?? 0,
                "maxLevel('buffId')", "Highest instance level of an active buff.");
            Register("rate", c => c.Context?.Runtime?.Variables?.GetRate(c.StringArg(0)) ?? BigNumber.Zero,
                "rate('variable')", "Average gain per second over the last few seconds.", isVolatile: true);
            Register("var", c =>
            {
                string name = c.StringArg(0);
                return c.Context != null && c.Context.TryResolve(name, out var v) ? v : BigNumber.Zero;
            }, "var('name')", "Reads a value by name (for names that clash with functions).");
            Register("defined", c => B(c.Context != null && c.Context.TryResolve(c.StringArg(0), out _)),
                "defined('name')", "1 when a value with this name exists.");
            Register("table", c =>
            {
                var table = JabelTableRegistry.Find(c.StringArg(0));
                return table != null ? table.Get(c.ArgDouble(1)) : BigNumber.Zero;
            }, "table('tableId', index)", "Reads a value from a JabelTable asset.");
        }
    }
}
