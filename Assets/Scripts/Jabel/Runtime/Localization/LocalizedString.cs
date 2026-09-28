using System;
using UnityEngine;

namespace Jabel.Localization
{
    /// <summary>
    /// A reference to a localization key. Use it for every player-facing text field in data,
    /// so translation is never an afterthought.
    /// </summary>
    [Serializable]
    public struct LocalizedString
    {
        [SerializeField] private string key;

        public LocalizedString(string key) { this.key = key; }

        public string Key => key;
        public bool IsEmpty => string.IsNullOrEmpty(key);

        public string Resolve() => Loc.Get(key);
        public string Resolve(params object[] args) => Loc.Format(key, args);

        public override string ToString() => Resolve();

        public static implicit operator LocalizedString(string key) => new LocalizedString(key);
    }
}
