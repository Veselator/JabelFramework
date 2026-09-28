using System;

namespace Jabel.Localization
{
    public enum PluralCategory { Zero, One, Two, Few, Many, Other }

    /// <summary>
    /// Simplified CLDR cardinal plural rules. English has 2 forms, Russian 3 ("1 обезьяна,
    /// 2 обезьяны, 5 обезьян") — a classic localization bug when only "#one/#other" exist.
    /// </summary>
    public static class PluralRules
    {
        public static PluralCategory Get(string languageCode, double count)
        {
            string lang = string.IsNullOrEmpty(languageCode) ? "en" : languageCode.Split('-')[0].ToLowerInvariant();
            bool isInteger = Math.Abs(count % 1) < double.Epsilon;
            long n = (long)Math.Abs(Math.Floor(count));
            long mod10 = n % 10, mod100 = n % 100;

            switch (lang)
            {
                case "ru":
                case "uk":
                case "be":
                    if (!isInteger) return PluralCategory.Other;
                    if (mod10 == 1 && mod100 != 11) return PluralCategory.One;
                    if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return PluralCategory.Few;
                    return PluralCategory.Many;

                case "pl":
                    if (!isInteger) return PluralCategory.Other;
                    if (n == 1) return PluralCategory.One;
                    if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return PluralCategory.Few;
                    return PluralCategory.Many;

                case "cs":
                case "sk":
                    if (!isInteger) return PluralCategory.Many;
                    if (n == 1) return PluralCategory.One;
                    if (n >= 2 && n <= 4) return PluralCategory.Few;
                    return PluralCategory.Other;

                case "fr":
                case "pt":
                    return n <= 1 ? PluralCategory.One : PluralCategory.Other;

                case "ja":
                case "zh":
                case "ko":
                case "vi":
                case "th":
                case "id":
                    return PluralCategory.Other;

                default:
                    return (isInteger && n == 1) ? PluralCategory.One : PluralCategory.Other;
            }
        }

        public static string Suffix(PluralCategory category)
        {
            switch (category)
            {
                case PluralCategory.Zero: return "#zero";
                case PluralCategory.One: return "#one";
                case PluralCategory.Two: return "#two";
                case PluralCategory.Few: return "#few";
                case PluralCategory.Many: return "#many";
                default: return "#other";
            }
        }
    }
}
