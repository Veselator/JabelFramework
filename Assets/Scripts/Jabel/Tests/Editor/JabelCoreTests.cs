using System.Collections.Generic;
using Jabel.Buffs;
using Jabel.Localization;
using Jabel.Numbers;
using Jabel.Scripting;
using NUnit.Framework;

namespace Jabel.Tests
{
    /// <summary>Unit tests for the pure parts of the framework (numbers, formulas, prices, localization helpers).</summary>
    public class JabelCoreTests
    {
        private sealed class DictionaryContext : IFormulaContext
        {
            public readonly Dictionary<string, BigNumber> Values = new Dictionary<string, BigNumber>();
            public IJabelRuntime Runtime => null;
            public bool TryResolve(string name, out BigNumber value) => Values.TryGetValue(name, out value);
        }

        // ------------------------------------------------------------ BigNumber

        [Test]
        public void BigNumber_RepeatedAddition_StaysExact()
        {
            BigNumber sum = 0;
            for (int i = 0; i < 60; i++) sum += 1;
            Assert.AreEqual(60.0, sum.ToDouble());
            Assert.IsTrue(sum >= 60);
        }

        [Test]
        public void BigNumber_HandlesValuesBeyondDouble()
        {
            var huge = BigNumber.Pow(10, 500);
            Assert.AreEqual(500, huge.Exponent);
            Assert.IsTrue(huge * huge > huge);
            Assert.AreEqual(1000, (huge * huge).Exponent);
            Assert.IsTrue((huge / huge).ApproximatelyEquals(1));
            Assert.IsTrue(huge > 1e300);
            Assert.IsTrue(-huge < 0);
        }

        [Test]
        public void BigNumber_MixedPlainAndScientificCompare()
        {
            var plain = new BigNumber(5e299);
            var big = new BigNumber(1, 301);
            Assert.IsTrue(plain < big);
            Assert.IsTrue(big + plain > big);
        }

        [Test]
        public void BigNumber_ParseAndRoundTrip()
        {
            Assert.AreEqual(2500.0, BigNumber.Parse("2.5K").ToDouble());
            Assert.AreEqual(3e6, BigNumber.Parse("3M").ToDouble());
            var v = BigNumber.Parse("1.25e400");
            Assert.AreEqual(v, BigNumber.Parse(v.ToInvariantString()));
        }

        [Test]
        public void BigNumber_DivisionByZeroIsZero()
        {
            Assert.AreEqual(BigNumber.Zero, new BigNumber(5) / 0);
        }

        [Test]
        public void NumberFormatter_ShortSuffixes()
        {
            NumberFormatter.LocaleProvider = null;
            Assert.AreEqual("1.5K", NumberFormatter.Format(1500));
            Assert.AreEqual("2M", NumberFormatter.Format(2e6));
            Assert.AreEqual("12", NumberFormatter.Format(12));
            Assert.AreEqual("0.30", NumberFormatter.Format(0.3, NumberFormat.Money));
        }

        // ------------------------------------------------------------ Formulas

        [Test]
        public void Formula_PrecedenceAndPower()
        {
            Assert.AreEqual(14.0, FormulaCompiler.Compile("2 + 3 * 4").Evaluate(null).ToDouble());
            Assert.AreEqual(512.0, FormulaCompiler.Compile("2 ^ 3 ^ 2").Evaluate(null).ToDouble());
            Assert.AreEqual(-4.0, FormulaCompiler.Compile("-2 * 2").Evaluate(null).ToDouble());
            Assert.AreEqual(0.5, FormulaCompiler.Compile("2 ^ -1").Evaluate(null).ToDouble());
        }

        [Test]
        public void Formula_VariablesLogicAndTernary()
        {
            var ctx = new DictionaryContext();
            ctx.Values["money"] = 150;
            ctx.Values["level"] = 3;
            Assert.IsTrue(FormulaCompiler.Compile("money >= 100 and level > 2").Evaluate(ctx).ToBool());
            Assert.AreEqual(2.0, FormulaCompiler.Compile("level >= 3 ? 2 : 1").Evaluate(ctx).ToDouble());
            Assert.AreEqual(150.0, FormulaCompiler.Compile("max(money, 10)").Evaluate(ctx).ToDouble());
            Assert.AreEqual(1500.0, FormulaCompiler.Compile("1.5K").Evaluate(ctx).ToDouble());
        }

        [Test]
        public void Formula_ReportsErrors()
        {
            Assert.IsFalse(FormulaCompiler.Compile("2 + ").IsValid);
            Assert.IsFalse(FormulaCompiler.Compile("(1 + 2").IsValid);
            Assert.IsFalse(FormulaCompiler.Compile("3 # 4").IsValid);
            Assert.IsTrue(FormulaCompiler.Compile("count('monkey')").IsValid);
        }

        [Test]
        public void Formula_ConstantFolding()
        {
            var compiled = FormulaCompiler.Compile("10 * 1.15 ^ 2");
            Assert.IsTrue(compiled.IsConstant);
            Assert.AreEqual(13.225, compiled.ConstantValue.ToDouble(), 1e-9);
        }

        // ------------------------------------------------------------ Prices

        [Test]
        public void GeometricCost_BulkPriceMatchesSum()
        {
            var cost = new BuffCost { baseCost = 10, growth = 1.15 };
            var ctx = JabelContext.Rent(null);
            try
            {
                BigNumber manual = 0;
                for (int i = 0; i < 10; i++) manual += cost.PriceAt(i, ctx);
                var bulk = cost.TotalPrice(0, 10, ctx);
                Assert.AreEqual(manual.ToDouble(), bulk.ToDouble(), 0.1);
                Assert.AreEqual(10, cost.MaxAffordable(0, bulk, 1000, ctx));
            }
            finally
            {
                ctx.Release();
            }
        }

        // ------------------------------------------------------------ Localization helpers

        [Test]
        public void PluralRules_Russian()
        {
            Assert.AreEqual(PluralCategory.One, PluralRules.Get("ru", 1));
            Assert.AreEqual(PluralCategory.Few, PluralRules.Get("ru", 3));
            Assert.AreEqual(PluralCategory.Many, PluralRules.Get("ru", 5));
            Assert.AreEqual(PluralCategory.Many, PluralRules.Get("ru", 11));
            Assert.AreEqual(PluralCategory.One, PluralRules.Get("ru", 21));
            Assert.AreEqual(PluralCategory.Other, PluralRules.Get("en", 2));
        }

        [Test]
        public void CsvParser_QuotesAndCommas()
        {
            var rows = CsvParser.Parse("key,en\nhello,\"Hello, \"\"world\"\"\"\n");
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual("Hello, \"world\"", rows[1][1]);
        }
    }
}
