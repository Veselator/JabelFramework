using System;
using System.Globalization;
using System.Text;

namespace Jabel.Numbers
{
    public enum NumberNotation
    {
        /// <summary>1.23K, 4.56M, 7.89B ... then aa, ab, ac ...</summary>
        Short,
        /// <summary>1.23e45</summary>
        Scientific,
        /// <summary>12.3e45 (exponent multiple of 3)</summary>
        Engineering,
        /// <summary>1,234,567 (grouped, falls back to Short past 1e15)</summary>
        Grouped
    }

    /// <summary>Per-value display settings. Serializable so designers can tune it per variable.</summary>
    [Serializable]
    public struct NumberFormat
    {
        public NumberNotation notation;
        /// <summary>Decimals used once the value is abbreviated (>= 1000).</summary>
        public int decimals;
        /// <summary>Decimals used for small values (&lt; 1000), e.g. 2 for money "$0.30".</summary>
        public int smallDecimals;
        /// <summary>Remove trailing zeros ("1.50K" -> "1.5K").</summary>
        public bool trimZeros;

        public static NumberFormat Default => new NumberFormat { notation = NumberNotation.Short, decimals = 2, smallDecimals = 0, trimZeros = true };
        public static NumberFormat Money => new NumberFormat { notation = NumberNotation.Short, decimals = 2, smallDecimals = 2, trimZeros = false };
    }

    /// <summary>
    /// Supplies localized short-scale suffixes and culture. Implemented by the localization module,
    /// so numbers read "1.5M" in English and "1,5 млн" in Russian.
    /// </summary>
    public interface INumberLocaleProvider
    {
        CultureInfo Culture { get; }
        /// <summary>Suffix for 10^(3*tier). Tier 1 = thousand. Return null to use the default.</summary>
        string GetSuffix(int tier);
    }

    /// <summary>
    /// Formats BigNumbers for display. Stateless apart from the pluggable locale provider.
    /// </summary>
    public static class NumberFormatter
    {
        private static readonly string[] DefaultSuffixes =
        {
            "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No",
            "Dc", "UDc", "DDc", "TDc", "QaDc", "QiDc", "SxDc", "SpDc", "OcDc", "NoDc", "Vg"
        };

        public static INumberLocaleProvider LocaleProvider { get; set; }

        private static CultureInfo Culture => LocaleProvider?.Culture ?? CultureInfo.InvariantCulture;

        [ThreadStatic] private static StringBuilder _builder;

        public static string Format(BigNumber value) => Format(value, NumberFormat.Default);

        public static string Format(BigNumber value, NumberFormat format)
        {
            if (value.IsZero) return FormatSmall(0, format);

            double log = value.Log10();
            if (log < 3)
            {
                return FormatSmall(value.ToDouble(), format);
            }

            switch (format.notation)
            {
                case NumberNotation.Scientific:
                    return FormatScientific(value, format, 1);
                case NumberNotation.Engineering:
                    return FormatScientific(value, format, 3);
                case NumberNotation.Grouped:
                    if (log < 15) return value.ToDouble().ToString("N0", Culture);
                    return FormatShort(value, format);
                default:
                    return FormatShort(value, format);
            }
        }

        private static string FormatSmall(double value, NumberFormat format)
        {
            int decimals = Math.Max(0, format.smallDecimals);
            // Never show "0" for a tiny positive value when decimals are hidden.
            string text = value.ToString("F" + decimals, Culture);
            return format.trimZeros ? TrimZeros(text) : text;
        }

        private static string FormatShort(BigNumber value, NumberFormat format)
        {
            long tier = value.Exponent / 3;
            double scaled = value.Mantissa * Math.Pow(10, value.Exponent - tier * 3);

            string suffix = GetSuffix(tier);
            string number = scaled.ToString("F" + Math.Max(0, format.decimals), Culture);
            if (format.trimZeros) number = TrimZeros(number);

            var sb = _builder ??= new StringBuilder(32);
            sb.Clear();
            sb.Append(number);
            if (!string.IsNullOrEmpty(suffix))
            {
                // Latin abbreviations stick to the number; localized words get a space ("1,5 млн").
                if (suffix.Length > 0 && !IsLatinAbbreviation(suffix)) sb.Append(' ');
                sb.Append(suffix);
            }
            return sb.ToString();
        }

        private static bool IsLatinAbbreviation(string suffix)
        {
            foreach (char c in suffix)
                if (c > 127) return false;
            return suffix.Length <= 4;
        }

        private static string FormatScientific(BigNumber value, NumberFormat format, int step)
        {
            long exp = value.Exponent - (((value.Exponent % step) + step) % step);
            double mant = value.Mantissa * Math.Pow(10, value.Exponent - exp);
            string number = mant.ToString("F" + Math.Max(0, format.decimals), Culture);
            if (format.trimZeros) number = TrimZeros(number);
            return number + "e" + exp.ToString(CultureInfo.InvariantCulture);
        }

        private static string GetSuffix(long tier)
        {
            if (tier <= 0) return string.Empty;
            string localized = tier < int.MaxValue ? LocaleProvider?.GetSuffix((int)tier) : null;
            if (!string.IsNullOrEmpty(localized)) return localized;
            if (tier < DefaultSuffixes.Length) return DefaultSuffixes[tier];
            return AlphabeticSuffix(tier - DefaultSuffixes.Length);
        }

        /// <summary>aa, ab, ... az, ba, ... zz, aaa ... (standard idle-game "letter" notation).</summary>
        private static string AlphabeticSuffix(long index)
        {
            var sb = new StringBuilder();
            long n = index + 26; // start at two letters
            while (n >= 0)
            {
                sb.Insert(0, (char)('a' + n % 26));
                n = n / 26 - 1;
            }
            return sb.ToString();
        }

        private static string TrimZeros(string text)
        {
            string separator = Culture.NumberFormat.NumberDecimalSeparator;
            int sepIndex = text.IndexOf(separator, StringComparison.Ordinal);
            if (sepIndex < 0) return text;
            int end = text.Length;
            while (end > sepIndex + 1 && text[end - 1] == '0') end--;
            if (end == sepIndex + separator.Length) end = sepIndex;
            return text.Substring(0, end);
        }

        /// <summary>Formats seconds as "2h 13m", "45s", "3d 4h". Unit labels are passed in for localization.</summary>
        public static string FormatDuration(double seconds, string d = "d", string h = "h", string m = "m", string s = "s")
        {
            if (seconds < 0) seconds = 0;
            var span = TimeSpan.FromSeconds(Math.Min(seconds, TimeSpan.MaxValue.TotalSeconds - 1));
            if (span.TotalDays >= 1) return $"{(int)span.TotalDays}{d} {span.Hours}{h}";
            if (span.TotalHours >= 1) return $"{span.Hours}{h} {span.Minutes}{m}";
            if (span.TotalMinutes >= 1) return $"{span.Minutes}{m} {span.Seconds}{s}";
            return $"{span.Seconds}{s}";
        }
    }
}
