using Jabel.Scripting;
using UnityEngine;

namespace Jabel.Buffs
{
    /// <summary>
    /// Buff that acts periodically: generators, workers, auto-clickers.
    /// Every <see cref="Period"/> ticks its OnTick script runs.
    /// With <see cref="Instanced"/> every unit is a separate instance with its own level, timer,
    /// random seed and variables (e.g. each monkey can be upgraded individually).
    /// </summary>
    [CreateAssetMenu(menuName = "Jabel/Active Buff", fileName = "ActiveBuff")]
    public class ActiveBuff : BaseBuff
    {
        [Header("Timing")]
        [Tooltip("Ticks between activations. Formula; locals: level, index, count.")]
        [SerializeField] private JabelFormula period = new JabelFormula("20");
        [Tooltip("Runs every period. Locals: level, index, count, batch.\nNon-instanced: runs once for all units (use 'count').")]
        [SerializeField] private JabelScript onTick = new JabelScript();

        [Header("Instances")]
        [Tooltip("Each unit is tracked separately (own level/timer/variables).")]
        [SerializeField] private bool instanced;
        [Tooltip("Spread instance timers so they don't all fire on the same tick.")]
        [SerializeField] private bool staggerInstances = true;

        [Header("Instance levels")]
        [SerializeField] private int maxLevel = 1;
        [Tooltip("Price of the next level. Formula mode locals: n = level, level, index.")]
        [SerializeField] private BuffCost levelUpPrice = new BuffCost();
        [Tooltip("Extra condition to level up an instance. Locals: level, index. Empty = none.")]
        [SerializeField] private JabelFormula levelUpCondition = new JabelFormula();
        [SerializeField] private Jabel.Localization.LocalizedString levelUpConditionHint;
        [Tooltip("Runs after an instance levels up. Locals: level, index.")]
        [SerializeField] private JabelScript onLevelUp = new JabelScript();

        public JabelFormula Period => period;
        public JabelScript OnTick => onTick;
        public bool Instanced => instanced;
        public bool StaggerInstances => staggerInstances;
        public int MaxLevel => Mathf.Max(1, maxLevel);
        public BuffCost LevelUpPrice => levelUpPrice;
        public JabelFormula LevelUpCondition => levelUpCondition;
        public Jabel.Localization.LocalizedString LevelUpConditionHint => levelUpConditionHint;
        public JabelScript OnLevelUp => onLevelUp;

        public override bool IsActive => true;

#if UNITY_EDITOR
        public void EditorSetupActive(string periodFormula, JabelScript tick, bool isInstanced, int levels,
            BuffCost levelCost, string levelCondition, string levelConditionHintKey, JabelScript levelUp)
        {
            period = new JabelFormula(periodFormula);
            onTick = tick ?? new JabelScript();
            instanced = isInstanced;
            maxLevel = levels;
            levelUpPrice = levelCost ?? new BuffCost();
            levelUpCondition = new JabelFormula(levelCondition ?? string.Empty);
            levelUpConditionHint = new Jabel.Localization.LocalizedString(levelConditionHintKey);
            onLevelUp = levelUp ?? new JabelScript();
        }
#endif
    }
}
