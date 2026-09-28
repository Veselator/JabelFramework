using System;
using Jabel.Numbers;
using Jabel.Scripting;
using UnityEngine;

namespace Jabel.Buffs
{
    public enum CostMode
    {
        /// <summary>price(n) = baseCost * growth^n * multiplier. Bulk price and "max" are computed in closed form.</summary>
        Geometric,
        /// <summary>price(n) = formula, with local 'n' (units already owned / current level).</summary>
        Formula
    }

    /// <summary>
    /// Price definition shared by purchases and level-ups. Growth is the single most tuned
    /// number in any clicker, so it is data, and bulk buying is solved once here.
    /// </summary>
    [Serializable]
    public class BuffCost
    {
        [Tooltip("Variable that is spent.")]
        public string currency = "money";
        public CostMode mode = CostMode.Geometric;
        public BigNumber baseCost = 10;
        [Tooltip("Price multiplier per owned unit (1.07 - 1.15 is typical).")]
        public double growth = 1.15;
        [Tooltip("Formula mode: price of the next unit. Locals: n (owned count / level), level, count.")]
        public JabelFormula formula = new JabelFormula();
        [Tooltip("Optional global multiplier, e.g. a discount derived value. Empty = 1.")]
        public JabelFormula multiplier = new JabelFormula();

        public const string LocalN = "n";

        /// <summary>Price of the unit number <paramref name="n"/> (0-based).</summary>
        public BigNumber PriceAt(long n, JabelContext context)
        {
            BigNumber price;
            if (mode == CostMode.Geometric)
            {
                price = baseCost * BigNumber.Pow(growth, n);
            }
            else
            {
                context.SetLocal(LocalN, n);
                price = formula.Evaluate(context);
            }
            return BigNumber.Ceil(price * multiplier.Evaluate(context, BigNumber.One) * 100) / 100;
        }

        /// <summary>Total price of <paramref name="amount"/> units starting from owned count <paramref name="owned"/>.</summary>
        public BigNumber TotalPrice(long owned, long amount, JabelContext context)
        {
            if (amount <= 0) return BigNumber.Zero;
            if (mode == CostMode.Geometric)
            {
                var first = baseCost * BigNumber.Pow(growth, owned) * multiplier.Evaluate(context, BigNumber.One);
                return BigNumber.Ceil(BigNumber.GeometricSum(first, growth, amount) * 100) / 100;
            }

            var total = BigNumber.Zero;
            // Formula mode has no closed form; cap the iteration to keep the UI responsive.
            long capped = Math.Min(amount, 10000);
            for (long i = 0; i < capped; i++) total += PriceAt(owned + i, context);
            return total;
        }

        /// <summary>How many units are affordable with <paramref name="budget"/>, up to <paramref name="limit"/>.</summary>
        public long MaxAffordable(long owned, BigNumber budget, long limit, JabelContext context)
        {
            if (limit <= 0) return 0;
            if (mode == CostMode.Geometric)
            {
                var first = baseCost * BigNumber.Pow(growth, owned) * multiplier.Evaluate(context, BigNumber.One);
                long n = BigNumber.MaxAffordableGeometric(budget, first, growth);
                n = Math.Min(n, limit);
                // Rounding in TotalPrice may push the exact boundary either way; settle it precisely.
                while (n > 0 && TotalPrice(owned, n, context) > budget) n--;
                while (n < limit && TotalPrice(owned, n + 1, context) <= budget) n++;
                return n;
            }

            long count = 0;
            var spent = BigNumber.Zero;
            long cap = Math.Min(limit, 10000);
            while (count < cap)
            {
                var next = PriceAt(owned + count, context);
                if (spent + next > budget) break;
                spent += next;
                count++;
            }
            return count;
        }
    }
}
