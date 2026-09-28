using System;
using Jabel.Localization;
using Jabel.Numbers;
using Jabel.Scripting;
using UnityEngine;

namespace Jabel.Variables
{
    public enum VariablePersistence
    {
        /// <summary>Saved and survives resets (prestige currency, statistics).</summary>
        Permanent,
        /// <summary>Saved, but restored to the initial value on a run reset (money, level).</summary>
        Run,
        /// <summary>Never saved (UI state, combo counters).</summary>
        Session
    }

    /// <summary>A global stored value declared in data: money, level, gems...</summary>
    [Serializable]
    public class VariableDefinition
    {
        [Tooltip("Identifier used in formulas and blocks. Letters, digits, '_' and '.'.")]
        public string key = "money";
        public LocalizedString displayName;
        public Sprite icon;
        public BigNumber initialValue;
        public VariablePersistence persistence = VariablePersistence.Run;

        [Header("Limits")]
        public bool clampMin = true;
        public BigNumber min;
        public bool clampMax;
        public BigNumber max;

        [Tooltip("Decreases by 1 every tick until it reaches 0. Save-safe way to build temporary boosts:\n" +
                 "set 'frenzy = 120' and use 'frenzy > 0 ? 2 : 1' in a derived value.")]
        public bool countdown;

        [Header("Display")]
        public NumberFormat format = NumberFormat.Default;
        [Tooltip("List the change of this value in the 'While you were away' report.")]
        public bool showInOfflineReport;
    }

    /// <summary>
    /// A value computed from a formula, e.g. clickPower = "1 + count('keyboard') * 2".
    /// Derived values are the recommended way to model upgrades: they are recalculated from
    /// ownership, so balance changes apply to old saves and nothing needs to be "re-applied" on load.
    /// </summary>
    [Serializable]
    public class DerivedValueDefinition
    {
        public string key = "clickPower";
        public LocalizedString displayName;
        public JabelFormula formula = new JabelFormula("1");
        public NumberFormat format = NumberFormat.Default;
    }
}
