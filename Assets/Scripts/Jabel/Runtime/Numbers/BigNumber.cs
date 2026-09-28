using System;
using System.Globalization;
using UnityEngine;

namespace Jabel.Numbers
{
    /// <summary>
    /// Arbitrary-magnitude number used for every economy value in Jabel.
    /// Hybrid representation:
    ///  • |value| &lt; 1e300 — stored as a plain double (m = value, e = 0): integers stay exact,
    ///    so "clicks + 1" sixty times is exactly 60 and comparisons behave;
    ///  • larger — stored as mantissa * 10^exponent with |mantissa| in [1, 10).
    /// Clicker economies routinely exceed double.MaxValue (1e308); the exponent range is ±9e18.
    /// </summary>
    [Serializable]
    public struct BigNumber : IComparable<BigNumber>, IEquatable<BigNumber>
    {
        // Raw fields (serialized): plain when e == 0, scientific otherwise.
        [SerializeField] private double m;
        [SerializeField] private long e;

        /// <summary>Magnitude from which values switch to scientific storage.</summary>
        private const int PlainLimitExponent = 300;
        // Above this exponent difference the smaller operand is below double precision.
        private const int PrecisionDigits = 17;

        public static readonly BigNumber Zero = default;
        public static readonly BigNumber One = new BigNumber(1.0);

        public bool IsZero => m == 0;
        public bool IsNegative => m < 0;
        public int Sign => m > 0 ? 1 : (m < 0 ? -1 : 0);

        /// <summary>Raw serialized parts (for editors and custom serializers).</summary>
        public double RawMantissa => m;
        public long RawExponent => e;

        /// <summary>Scientific mantissa in [1, 10) (sign included).</summary>
        public double Mantissa
        {
            get
            {
                var c = Canonical();
                if (c.e != 0 || c.m == 0) return c.m;
                long exp = (long)Math.Floor(Math.Log10(Math.Abs(c.m)));
                double mant = c.m / Pow10(exp);
                // Guard against floating point drift at the boundaries.
                if (Math.Abs(mant) >= 10) mant /= 10;
                else if (Math.Abs(mant) < 1) mant *= 10;
                return mant;
            }
        }

        /// <summary>Scientific exponent (value = Mantissa * 10^Exponent).</summary>
        public long Exponent
        {
            get
            {
                var c = Canonical();
                if (c.e != 0 || c.m == 0) return c.e;
                long exp = (long)Math.Floor(Math.Log10(Math.Abs(c.m)));
                double mant = c.m / Pow10(exp);
                if (Math.Abs(mant) >= 10) exp++;
                else if (Math.Abs(mant) < 1) exp--;
                return exp;
            }
        }

        public BigNumber(double value)
        {
            m = value;
            e = 0;
            Normalize();
        }

        public BigNumber(double mantissa, long exponent)
        {
            m = mantissa;
            e = exponent;
            Normalize();
        }

        private static readonly double[] Pow10Table = BuildPow10Table();

        private static double[] BuildPow10Table()
        {
            var table = new double[633];
            for (int i = 0; i < table.Length; i++) table[i] = Math.Pow(10, i - 316);
            return table;
        }

        private static double Pow10(long power)
        {
            long index = power + 316;
            if (index >= 0 && index < Pow10Table.Length) return Pow10Table[index];
            return Math.Pow(10, power);
        }

        /// <summary>Brings the raw fields into canonical form (plain below 1e300, scientific above).</summary>
        private void Normalize()
        {
            if (m == 0 || double.IsNaN(m))
            {
                m = 0;
                e = 0;
                return;
            }

            if (double.IsInfinity(m))
            {
                // Clamp instead of propagating infinities through the economy.
                m = m > 0 ? 9.999999999999 : -9.999999999999;
                e = 308;
                return;
            }

            if (e == 0)
            {
                if (Math.Abs(m) < 1e300) return;
                ToScientific();
                return;
            }

            // Scientific input: fold into plain when small enough.
            double abs = Math.Abs(m);
            long magnitude = e + (long)Math.Floor(Math.Log10(abs));
            if (magnitude < PlainLimitExponent && magnitude > -PlainLimitExponent)
            {
                m *= Pow10(e);
                e = 0;
                return;
            }
            ToScientific();
        }

        private void ToScientific()
        {
            double abs = Math.Abs(m);
            int shift = (int)Math.Floor(Math.Log10(abs));
            m /= Pow10(shift);
            e += shift;
            abs = Math.Abs(m);
            if (abs >= 10) { m /= 10; e++; }
            else if (abs < 1) { m *= 10; e--; }
        }

        /// <summary>Returns canonical form (raw data from older assets may be scientific while small).</summary>
        private BigNumber Canonical()
        {
            if (e == 0) return this;
            var c = this;
            c.Normalize();
            return c;
        }

        private bool IsPlain => e == 0;

        // ---------------------------------------------------------------- conversions

        public static implicit operator BigNumber(double value) => new BigNumber(value);
        public static implicit operator BigNumber(float value) => new BigNumber(value);
        public static implicit operator BigNumber(int value) => new BigNumber(value);
        public static implicit operator BigNumber(long value) => new BigNumber(value);

        /// <summary>Converts to double. Returns ±Infinity when out of range.</summary>
        public double ToDouble()
        {
            if (e == 0) return m;
            if (m == 0) return 0;
            if (e > 308) return m > 0 ? double.PositiveInfinity : double.NegativeInfinity;
            if (e < -324) return 0;
            return m * Pow10(e);
        }

        /// <summary>Converts to double, clamping to the finite range.</summary>
        public double ToDoubleClamped()
        {
            double d = ToDouble();
            if (double.IsPositiveInfinity(d)) return double.MaxValue;
            if (double.IsNegativeInfinity(d)) return double.MinValue;
            return d;
        }

        public int ToInt()
        {
            double d = ToDouble();
            if (d >= int.MaxValue) return int.MaxValue;
            if (d <= int.MinValue) return int.MinValue;
            return (int)d;
        }

        public long ToLong()
        {
            double d = ToDouble();
            if (d >= long.MaxValue) return long.MaxValue;
            if (d <= long.MinValue) return long.MinValue;
            return (long)d;
        }

        public bool ToBool() => m != 0;

        public static explicit operator double(BigNumber value) => value.ToDouble();

        // ---------------------------------------------------------------- arithmetic

        public static BigNumber operator +(BigNumber a, BigNumber b)
        {
            a = a.Canonical();
            b = b.Canonical();
            if (a.m == 0) return b;
            if (b.m == 0) return a;
            if (a.IsPlain && b.IsPlain) return new BigNumber(a.m + b.m);

            long ea = a.Exponent, eb = b.Exponent;
            double ma = a.Mantissa, mb = b.Mantissa;
            long diff = ea - eb;
            if (diff > PrecisionDigits) return a;
            if (diff < -PrecisionDigits) return b;
            return diff >= 0
                ? new BigNumber(ma + mb * Pow10(-diff), ea)
                : new BigNumber(mb + ma * Pow10(diff), eb);
        }

        public static BigNumber operator -(BigNumber a)
        {
            a.m = -a.m;
            return a;
        }

        public static BigNumber operator -(BigNumber a, BigNumber b) => a + (-b);

        public static BigNumber operator *(BigNumber a, BigNumber b)
        {
            a = a.Canonical();
            b = b.Canonical();
            if (a.m == 0 || b.m == 0) return Zero;
            if (a.IsPlain && b.IsPlain)
            {
                double product = a.m * b.m;
                if (!double.IsInfinity(product) && Math.Abs(product) < 1e300) return new BigNumber(product);
            }
            return new BigNumber(a.Mantissa * b.Mantissa, a.Exponent + b.Exponent);
        }

        public static BigNumber operator /(BigNumber a, BigNumber b)
        {
            a = a.Canonical();
            b = b.Canonical();
            // Division by zero is treated as zero: designers should never crash the economy.
            if (b.m == 0 || a.m == 0) return Zero;
            if (a.IsPlain && b.IsPlain)
            {
                double quotient = a.m / b.m;
                if (!double.IsInfinity(quotient) && Math.Abs(quotient) < 1e300) return new BigNumber(quotient);
            }
            return new BigNumber(a.Mantissa / b.Mantissa, a.Exponent - b.Exponent);
        }

        public static BigNumber operator %(BigNumber a, BigNumber b)
        {
            a = a.Canonical();
            b = b.Canonical();
            if (b.m == 0) return Zero;
            // Modulo is only meaningful inside double range; beyond that it collapses to zero.
            if (!a.IsPlain || !b.IsPlain) return Zero;
            return new BigNumber(a.m % b.m);
        }

        public static BigNumber operator ++(BigNumber a) => a + One;
        public static BigNumber operator --(BigNumber a) => a - One;

        public static BigNumber Pow(BigNumber value, double power)
        {
            value = value.Canonical();
            if (power == 0) return One;
            if (value.m == 0) return Zero;
            if (power == 1) return value;

            if (value.IsPlain)
            {
                double direct = Math.Pow(value.m, power);
                if (!double.IsNaN(direct) && !double.IsInfinity(direct) && Math.Abs(direct) < 1e300) return new BigNumber(direct);
            }

            bool negative = value.m < 0;
            // Negative base: only integer powers are defined.
            if (negative && Math.Abs(power % 1) > double.Epsilon) return Zero;

            double resultLog = value.Log10() * power;
            if (double.IsNaN(resultLog)) return Zero;
            if (resultLog > 9e18) resultLog = 9e18;
            if (resultLog < -9e18) return Zero;

            double exponent = Math.Floor(resultLog);
            double mantissa = Math.Pow(10, resultLog - exponent);
            if (negative && Math.Abs(power % 2) == 1) mantissa = -mantissa;
            return new BigNumber(mantissa, (long)exponent);
        }

        public static BigNumber Pow(BigNumber value, BigNumber power) => Pow(value, power.ToDoubleClamped());

        /// <summary>Base-10 logarithm of |value|. Returns double.NegativeInfinity for zero.</summary>
        public double Log10()
        {
            if (m == 0) return double.NegativeInfinity;
            if (e == 0) return Math.Log10(Math.Abs(m));
            return Math.Log10(Math.Abs(m)) + e;
        }

        public double Ln() => Log10() * 2.302585092994046;

        public static BigNumber Sqrt(BigNumber value) => Pow(value, 0.5);

        public static BigNumber Abs(BigNumber value) => value.m < 0 ? -value : value;

        public static BigNumber Min(BigNumber a, BigNumber b) => a < b ? a : b;
        public static BigNumber Max(BigNumber a, BigNumber b) => a > b ? a : b;

        public static BigNumber Clamp(BigNumber value, BigNumber min, BigNumber max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        // Past 1e15 every double is already an integer, so rounding is a no-op for big values.
        public static BigNumber Floor(BigNumber value)
        {
            value = value.Canonical();
            return value.IsPlain ? new BigNumber(Math.Floor(value.m)) : value;
        }

        public static BigNumber Ceil(BigNumber value)
        {
            value = value.Canonical();
            return value.IsPlain ? new BigNumber(Math.Ceiling(value.m)) : value;
        }

        public static BigNumber Round(BigNumber value)
        {
            value = value.Canonical();
            return value.IsPlain ? new BigNumber(Math.Round(value.m, MidpointRounding.AwayFromZero)) : value;
        }

        // ---------------------------------------------------------------- comparison

        public int CompareTo(BigNumber other)
        {
            var a = Canonical();
            var b = other.Canonical();
            if (a.IsPlain && b.IsPlain) return a.m.CompareTo(b.m);

            int signA = a.Sign, signB = b.Sign;
            if (signA != signB) return signA.CompareTo(signB);
            if (signA == 0) return 0;

            // Same sign, at least one scientific (so |value| >= 1e300).
            int magnitude;
            if (a.IsPlain) magnitude = -1;
            else if (b.IsPlain) magnitude = 1;
            else if (a.e != b.e) magnitude = a.e.CompareTo(b.e);
            else magnitude = Math.Abs(a.m).CompareTo(Math.Abs(b.m));

            return signA > 0 ? magnitude : -magnitude;
        }

        public bool Equals(BigNumber other) => CompareTo(other) == 0;
        public override bool Equals(object obj) => obj is BigNumber other && Equals(other);

        public override int GetHashCode()
        {
            var c = Canonical();
            return c.m.GetHashCode() ^ c.e.GetHashCode();
        }

        public static bool operator ==(BigNumber a, BigNumber b) => a.CompareTo(b) == 0;
        public static bool operator !=(BigNumber a, BigNumber b) => a.CompareTo(b) != 0;
        public static bool operator <(BigNumber a, BigNumber b) => a.CompareTo(b) < 0;
        public static bool operator >(BigNumber a, BigNumber b) => a.CompareTo(b) > 0;
        public static bool operator <=(BigNumber a, BigNumber b) => a.CompareTo(b) <= 0;
        public static bool operator >=(BigNumber a, BigNumber b) => a.CompareTo(b) >= 0;

        /// <summary>Relative closeness check, useful for UI "did it change" tests.</summary>
        public bool ApproximatelyEquals(BigNumber other, double relativeTolerance = 1e-9)
        {
            if (this == other) return true;
            BigNumber diff = Abs(this - other);
            BigNumber scale = Max(Abs(this), Abs(other));
            return diff <= scale * relativeTolerance;
        }

        // ---------------------------------------------------------------- text

        /// <summary>Round-trip invariant representation, e.g. "1234.5" or "1.2345e678". Used by saves.</summary>
        public string ToInvariantString()
        {
            var c = Canonical();
            if (c.m == 0) return "0";
            if (c.IsPlain) return c.m.ToString("R", CultureInfo.InvariantCulture);
            return c.m.ToString("R", CultureInfo.InvariantCulture) + "e" + c.e.ToString(CultureInfo.InvariantCulture);
        }

        public override string ToString() => ToInvariantString();

        /// <summary>Parses "123", "1.5e30", "2.5K", "3M", "1B", "4T" (invariant culture).</summary>
        public static bool TryParse(string text, out BigNumber value)
        {
            value = Zero;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim().Replace(" ", "").Replace("_", "");

            long multiplierExp = 0;
            switch (char.ToUpperInvariant(text[text.Length - 1]))
            {
                case 'K': multiplierExp = 3; break;
                case 'M': multiplierExp = 6; break;
                case 'B': multiplierExp = 9; break;
                case 'T': multiplierExp = 12; break;
            }
            if (multiplierExp > 0) text = text.Substring(0, text.Length - 1);

            int eIndex = text.IndexOfAny(new[] { 'e', 'E' });
            if (eIndex > 0)
            {
                if (!double.TryParse(text.Substring(0, eIndex), NumberStyles.Float, CultureInfo.InvariantCulture, out double mant)) return false;
                if (!long.TryParse(text.Substring(eIndex + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out long exp)) return false;
                value = new BigNumber(mant, exp + multiplierExp);
                return true;
            }

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double plain)) return false;
            value = multiplierExp == 0 ? new BigNumber(plain) : new BigNumber(plain * Pow10(multiplierExp));
            return true;
        }

        public static BigNumber Parse(string text) => TryParse(text, out var v) ? v : Zero;

        // ---------------------------------------------------------------- economy helpers

        /// <summary>
        /// Sum of a geometric series: first * (ratio^count - 1) / (ratio - 1).
        /// The canonical "buy N items with exponential price" formula.
        /// </summary>
        public static BigNumber GeometricSum(BigNumber first, double ratio, double count)
        {
            if (count <= 0) return Zero;
            if (Math.Abs(ratio - 1) < 1e-12) return first * count;
            return first * (Pow(ratio, count) - One) / (ratio - 1);
        }

        /// <summary>How many items with geometric price can be bought with the budget. first = price of the next item.</summary>
        public static long MaxAffordableGeometric(BigNumber budget, BigNumber first, double ratio)
        {
            if (first <= Zero) return long.MaxValue;
            if (budget < first) return 0;
            if (Math.Abs(ratio - 1) < 1e-12) return Floor(budget / first).ToLong();

            // n = floor(log_r(budget * (r - 1) / first + 1))
            BigNumber inner = budget * (ratio - 1) / first + One;
            // Small epsilon: log round-off must not turn an exact 10 into 9.999999.
            double n = Math.Floor(inner.Log10() / Math.Log10(ratio) + 1e-9);
            if (n < 0) return 0;
            if (n > long.MaxValue / 2) return long.MaxValue / 2;
            return (long)n;
        }
    }
}
