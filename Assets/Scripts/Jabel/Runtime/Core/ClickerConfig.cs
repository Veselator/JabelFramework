using System.Collections.Generic;
using Jabel.Buffs;
using Jabel.Localization;
using Jabel.Scripting;
using Jabel.Variables;
using UnityEngine;

namespace Jabel.Core
{
    public enum SaveStorageType
    {
        /// <summary>PlayerPrefs on WebGL, files elsewhere.</summary>
        Auto,
        File,
        PlayerPrefs
    }

    /// <summary>
    /// The whole game design of a clicker in one asset: economy variables, derived formulas,
    /// buffs, timing, save/offline rules and top-level scripts.
    /// </summary>
    [CreateAssetMenu(menuName = "Jabel/Clicker Config", fileName = "ClickerConfig")]
    public class ClickerConfig : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Identifies the game in save files.")]
        public string configId = "my_clicker";
        [Tooltip("Increase when the save layout changes; register ISaveMigration for old versions.")]
        public int saveVersion = 1;

        [Header("Simulation")]
        [Tooltip("Seconds per simulation tick.")]
        [Min(0.01f)] public float tickDuration = 0.25f;
        [Tooltip("Ticks processed individually per frame; the rest is collapsed into one batch (lag spikes).")]
        [Min(1)] public int maxTicksPerFrame = 20;

        [Header("Economy")]
        public List<VariableDefinition> variables = new List<VariableDefinition>();
        public List<DerivedValueDefinition> derivedValues = new List<DerivedValueDefinition>();
        public List<PassiveBuff> passiveBuffs = new List<PassiveBuff>();
        public List<ActiveBuff> activeBuffs = new List<ActiveBuff>();
        [Tooltip("Tables readable from formulas via table('id', index).")]
        public List<JabelTable> tables = new List<JabelTable>();

        [Header("Localization")]
        public LocalizationDatabase localization;

        [Header("Scripts")]
        [Tooltip("Runs on every launch after loading.")]
        public JabelScript onStart = new JabelScript();
        [Tooltip("Runs once, on the very first launch (starting resources, tutorial flags).")]
        public JabelScript onNewGame = new JabelScript();
        [Tooltip("Default click behaviour. Set local 'value' to report the click size to feedback " +
                 "(particles, floating text) and 'critical' = 1 for critical clicks.")]
        public JabelScript onClick = new JabelScript();
        [Tooltip("Runs every tick. Local 'batch' > 1 during catch-up.")]
        public JabelScript onTick = new JabelScript();
        [Tooltip("Runs after offline progress. Locals: seconds (away), simulated (effective seconds).")]
        public JabelScript onReturn = new JabelScript();

        [Header("Save")]
        public SaveStorageType storage = SaveStorageType.Auto;
        public string saveSlot = "main";
        [Tooltip("Seconds between automatic saves. 0 disables autosave.")]
        public float autosaveInterval = 20f;

        [Header("Offline progress")]
        public bool offlineProgress = true;
        [Tooltip("Max simulated seconds (formula, e.g. '8 * 3600 + count(\"long_nap\") * 3600').")]
        public JabelFormula offlineMaxSeconds = new JabelFormula("8 * 3600");
        [Tooltip("Fraction of time that counts (formula, 0..1+).")]
        public JabelFormula offlineEfficiency = new JabelFormula("1");
        [Tooltip("Absences shorter than this are simulated silently (no report popup).")]
        public float offlineReportThreshold = 30f;
        [Tooltip("Offline time is simulated in this many chunks: more = more accurate, slower.")]
        [Range(1, 500)] public int offlineSimulationSteps = 40;

        public IEnumerable<BaseBuff> AllBuffs
        {
            get
            {
                foreach (var b in passiveBuffs) if (b != null) yield return b;
                foreach (var b in activeBuffs) if (b != null) yield return b;
            }
        }
    }
}
