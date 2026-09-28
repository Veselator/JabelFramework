using System.Collections.Generic;
using Jabel.Numbers;

namespace Jabel.Buffs
{
    /// <summary>Runtime state of one unit of an instanced active buff.</summary>
    public sealed class BuffInstance
    {
        private Dictionary<string, BigNumber> _variables;

        public ActiveBuff Buff { get; }
        public int Index { get; }
        public int Level { get; internal set; } = 1;
        /// <summary>Ticks accumulated towards the next activation.</summary>
        public long TickCounter { get; internal set; }
        /// <summary>Stable random seed for cosmetics (colors, names) that must survive reloads.</summary>
        public int Seed { get; internal set; }
        /// <summary>Total activations, handy for statistics and achievements.</summary>
        public long Activations { get; internal set; }

        internal BuffInstance(ActiveBuff buff, int index, int seed)
        {
            Buff = buff;
            Index = index;
            Seed = seed;
        }

        public bool HasVariable(string name) => _variables != null && _variables.ContainsKey(name);

        public bool TryGetVariable(string name, out BigNumber value)
        {
            if (_variables != null && _variables.TryGetValue(name, out value)) return true;
            // Built-in instance values are readable by formulas.
            switch (name)
            {
                case "level": value = Level; return true;
                case "index": value = Index; return true;
                case "seed": value = Seed; return true;
            }
            value = BigNumber.Zero;
            return false;
        }

        public BigNumber GetVariable(string name) => TryGetVariable(name, out var v) ? v : BigNumber.Zero;

        public void SetVariable(string name, BigNumber value)
        {
            _variables ??= new Dictionary<string, BigNumber>();
            _variables[name] = value;
        }

        public IEnumerable<KeyValuePair<string, BigNumber>> Variables =>
            _variables ?? (IEnumerable<KeyValuePair<string, BigNumber>>)System.Array.Empty<KeyValuePair<string, BigNumber>>();
    }
}
