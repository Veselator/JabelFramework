using System;
using System.Collections.Generic;
using System.Globalization;
using Jabel.Numbers;
using UnityEngine;

namespace Jabel.Localization
{
    public interface ILocalizationService
    {
        string CurrentLanguage { get; }
        IReadOnlyList<LanguageInfo> Languages { get; }
        event Action LanguageChanged;
        void SetLanguage(string code);
        string Get(string key);
        bool Has(string key);
    }

    /// <summary>
    /// Global localization entry point. Static on purpose: text components exist before and outside
    /// of any game session (menus, loading screens), so they need a service that is always reachable.
    /// The implementation is swappable via <see cref="Service"/> for tests or third-party backends.
    /// </summary>
    public static class Loc
    {
        private const string PrefsKey = "jabel.language";

        private static ILocalizationService _service;

        public static ILocalizationService Service
        {
            get => _service;
            set
            {
                if (_service != null) _service.LanguageChanged -= RaiseChanged;
                _service = value;
                if (_service != null) _service.LanguageChanged += RaiseChanged;
                NumberFormatter.LocaleProvider = _service as INumberLocaleProvider;
                RaiseChanged();
            }
        }

        public static bool IsReady => _service != null;

        /// <summary>Raised after the language changes (and once when the service is installed).</summary>
        public static event Action LanguageChanged;

        public static string CurrentLanguage => _service?.CurrentLanguage ?? "en";

        public static IReadOnlyList<LanguageInfo> Languages => _service?.Languages ?? Array.Empty<LanguageInfo>();

        /// <summary>
        /// Notifies listeners one by one: a failing or destroyed listener must not stop the others
        /// (otherwise, e.g., the language button would keep showing the old language).
        /// </summary>
        private static void RaiseChanged()
        {
            var handlers = LanguageChanged;
            if (handlers == null) return;
            foreach (var @delegate in handlers.GetInvocationList())
            {
                var handler = (Action)@delegate;
                if (handler.Target is UnityEngine.Object unityObject && unityObject == null)
                {
                    // Listener destroyed without unsubscribing: drop it.
                    LanguageChanged -= handler;
                    continue;
                }
                try
                {
                    handler();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        /// <summary>Static state must not leak between play sessions when domain reload is disabled.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            LanguageChanged = null;
        }

        /// <summary>Installs the default CSV-backed service for a database (idempotent per database).</summary>
        public static void Initialize(LocalizationDatabase database)
        {
            if (database == null) return;
            if (_service is CsvLocalizationService existing && existing.Database == database) return;

            string saved = PlayerPrefs.GetString(PrefsKey, null);
            Service = new CsvLocalizationService(database, saved);
        }

        public static void SetLanguage(string code)
        {
            if (_service == null) return;
            _service.SetLanguage(code);
            PlayerPrefs.SetString(PrefsKey, _service.CurrentLanguage);
        }

        /// <summary>Cycles through available languages (for a simple language button).</summary>
        public static void NextLanguage()
        {
            var list = Languages;
            if (list.Count == 0) return;
            int index = 0;
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i].code, CurrentLanguage, StringComparison.OrdinalIgnoreCase)) index = i;
            SetLanguage(list[(index + 1) % list.Count].code);
        }

        public static bool Has(string key) => _service != null && _service.Has(key);

        /// <summary>Translated text, or the key itself when missing (so gaps are visible, not blank).</summary>
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            return _service?.Get(key) ?? key;
        }

        /// <summary>Translated text with string.Format placeholders: "Level {0}".</summary>
        public static string Format(string key, params object[] args)
        {
            string pattern = Get(key);
            if (args == null || args.Length == 0) return pattern;
            try
            {
                return string.Format(Culture, pattern, args);
            }
            catch (FormatException)
            {
                return pattern;
            }
        }

        /// <summary>
        /// Plural-aware lookup: tries key#one/#few/#many/#other for the current language, then key.
        /// {0} is the count, extra args follow.
        /// </summary>
        public static string Plural(string key, double count, string countText = null, params object[] extraArgs)
        {
            var category = PluralRules.Get(CurrentLanguage, count);
            string fullKey = key + PluralRules.Suffix(category);
            if (!Has(fullKey)) fullKey = Has(key + "#other") ? key + "#other" : key;

            var args = new object[1 + (extraArgs?.Length ?? 0)];
            args[0] = countText ?? count.ToString(Culture);
            if (extraArgs != null) Array.Copy(extraArgs, 0, args, 1, extraArgs.Length);
            return Format(fullKey, args);
        }

        public static CultureInfo Culture => (_service as INumberLocaleProvider)?.Culture ?? CultureInfo.InvariantCulture;

        public static LanguageInfo CurrentLanguageInfo
        {
            get
            {
                foreach (var l in Languages)
                    if (string.Equals(l.code, CurrentLanguage, StringComparison.OrdinalIgnoreCase)) return l;
                return null;
            }
        }
    }

    /// <summary>Default implementation reading CSV tables from a <see cref="LocalizationDatabase"/>.</summary>
    public sealed class CsvLocalizationService : ILocalizationService, INumberLocaleProvider
    {
        public LocalizationDatabase Database { get; }

        private readonly Dictionary<string, Dictionary<string, string>> _tables;
        private Dictionary<string, string> _current;
        private Dictionary<string, string> _fallback;
        private readonly string[] _suffixCache = new string[64];

        public string CurrentLanguage { get; private set; }
        public IReadOnlyList<LanguageInfo> Languages => Database.Languages;
        public CultureInfo Culture { get; private set; } = CultureInfo.InvariantCulture;
        public event Action LanguageChanged;

        public CsvLocalizationService(LocalizationDatabase database, string preferredLanguage)
        {
            Database = database;
            _tables = database.BuildDictionaries();
            _tables.TryGetValue(database.DefaultLanguage, out _fallback);

            string initial = preferredLanguage;
            if (string.IsNullOrEmpty(initial) || database.FindLanguage(initial) == null)
                initial = database.DetectSystemLanguage ? DetectSystem(database) : database.DefaultLanguage;
            Apply(initial, notify: false);
        }

        private static string DetectSystem(LocalizationDatabase database)
        {
            var system = Application.systemLanguage;
            foreach (var language in database.Languages)
                if (language.systemLanguages.Contains(system)) return language.code;
            return database.DefaultLanguage;
        }

        public void SetLanguage(string code) => Apply(code, notify: true);

        private void Apply(string code, bool notify)
        {
            var info = Database.FindLanguage(code) ?? Database.FindLanguage(Database.DefaultLanguage);
            if (info == null) return;

            CurrentLanguage = info.code;
            _tables.TryGetValue(info.code, out _current);
            Array.Clear(_suffixCache, 0, _suffixCache.Length);

            try
            {
                Culture = CultureInfo.GetCultureInfo(string.IsNullOrEmpty(info.cultureName) ? info.code : info.cultureName);
            }
            catch (CultureNotFoundException)
            {
                Culture = CultureInfo.InvariantCulture;
            }

            if (notify) LanguageChanged?.Invoke();
        }

        public bool Has(string key) =>
            (_current != null && _current.ContainsKey(key)) || (_fallback != null && _fallback.ContainsKey(key));

        public string Get(string key)
        {
            if (_current != null && _current.TryGetValue(key, out var text)) return text;
            if (_fallback != null && _fallback.TryGetValue(key, out text)) return text;
            return null;
        }

        public string GetSuffix(int tier)
        {
            if (tier < _suffixCache.Length && _suffixCache[tier] != null) return _suffixCache[tier];
            // Keys: number.suffix.1 (thousand), number.suffix.2 (million)...
            string value = Get("number.suffix." + tier) ?? string.Empty;
            if (tier < _suffixCache.Length) _suffixCache[tier] = value;
            return value;
        }
    }
}
