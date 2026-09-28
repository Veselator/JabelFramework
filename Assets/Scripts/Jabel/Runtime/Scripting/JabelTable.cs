using System.Collections.Generic;
using Jabel.Numbers;
using UnityEngine;

namespace Jabel.Scripting
{
    /// <summary>
    /// A named list of numbers that formulas can read with table('id', index).
    /// Handy for hand-tuned balance curves that no formula describes well.
    /// </summary>
    [CreateAssetMenu(menuName = "Jabel/Table", fileName = "JabelTable")]
    public class JabelTable : ScriptableObject
    {
        public enum OutOfRange { Clamp, Extrapolate, Zero }

        [SerializeField] private string id;
        [SerializeField] private OutOfRange outOfRange = OutOfRange.Clamp;
        [Tooltip("Blend between neighbouring entries for fractional indices.")]
        [SerializeField] private bool interpolate;
        [SerializeField] private List<BigNumber> values = new List<BigNumber>();

        public string Id => string.IsNullOrEmpty(id) ? name : id;
        public IReadOnlyList<BigNumber> Values => values;

        public BigNumber Get(double index)
        {
            if (values.Count == 0) return BigNumber.Zero;
            int last = values.Count - 1;

            if (index < 0 || index > last)
            {
                switch (outOfRange)
                {
                    case OutOfRange.Zero: return BigNumber.Zero;
                    case OutOfRange.Clamp: return index < 0 ? values[0] : values[last];
                    case OutOfRange.Extrapolate:
                        if (values.Count < 2) return values[0];
                        // Continue the growth ratio of the last two entries.
                        if (index < 0) return values[0];
                        var ratio = values[last - 1].IsZero ? BigNumber.One : values[last] / values[last - 1];
                        return values[last] * BigNumber.Pow(ratio, index - last);
                }
            }

            int lower = (int)index;
            if (!interpolate || lower >= last) return values[lower];
            double t = index - lower;
            return values[lower] + (values[lower + 1] - values[lower]) * t;
        }

        private void OnEnable() => JabelTableRegistry.Register(this);
        private void OnDisable() => JabelTableRegistry.Unregister(this);
    }

    /// <summary>Tables register themselves when loaded; ClickerConfig references keep them loaded.</summary>
    public static class JabelTableRegistry
    {
        private static readonly Dictionary<string, JabelTable> Tables = new Dictionary<string, JabelTable>();

        public static void Register(JabelTable table)
        {
            if (table != null) Tables[table.Id] = table;
        }

        public static void Unregister(JabelTable table)
        {
            if (table != null && Tables.TryGetValue(table.Id, out var existing) && existing == table) Tables.Remove(table.Id);
        }

        public static JabelTable Find(string id) => id != null && Tables.TryGetValue(id, out var t) ? t : null;
    }
}
