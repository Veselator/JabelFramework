using System;
using System.Collections.Generic;
using Jabel.Localization;
using Jabel.Numbers;
using Jabel.Scripting;
using UnityEngine;

namespace Jabel.Buffs
{
    public enum BuffVisibility
    {
        /// <summary>Always listed; locked ones show their requirements.</summary>
        Always,
        /// <summary>Appears once requirements are met (progressive reveal keeps the shop readable).</summary>
        WhenUnlocked,
        /// <summary>Never listed in shops (granted by scripts or bought through custom UI).</summary>
        Hidden
    }

    /// <summary>"Requires {variable} ≥ {value}" — auto-generates a localized hint for locked items.</summary>
    [Serializable]
    public class BuffRequirement
    {
        public string variable = "level";
        public BigNumber minimum = 1;
    }

    /// <summary>
    /// Base data of anything the player can buy. Behaviour is data: price, requirements and
    /// the OnBought script. Subclasses add timing (ActiveBuff) or stay pure data (PassiveBuff).
    /// </summary>
    public abstract class BaseBuff : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id used by saves and formulas (count('id')). Never change it after release.")]
        [SerializeField] private string id;
        [SerializeField] private Sprite icon;
        [Tooltip("Optional icon per owned count (multi-level upgrades): element N is shown while N units are owned. " +
                 "Missing entries fall back to the last one, then to Icon.")]
        [SerializeField] private List<Sprite> iconsByCount = new List<Sprite>();
        [SerializeField] private LocalizedString displayName;
        [SerializeField] private LocalizedString description;
        [Tooltip("Free-form tag for grouping and filtering (shop tabs, listeners).")]
        [SerializeField] private string tag;
        [SerializeField] private int sortOrder;

        [Header("Price")]
        [SerializeField] private BuffCost price = new BuffCost();
        [Tooltip("Maximum owned count. 0 = unlimited, 1 = one-time upgrade.")]
        [SerializeField] private int maxCount;

        [Header("Availability")]
        [SerializeField] private BuffVisibility visibility = BuffVisibility.Always;
        [SerializeField] private List<BuffRequirement> requirements = new List<BuffRequirement>();
        [Tooltip("Extra condition formula. Empty = none.")]
        [SerializeField] private JabelFormula condition = new JabelFormula();
        [Tooltip("Hint shown while the extra condition fails.")]
        [SerializeField] private LocalizedString conditionHint;
        [Tooltip("Optional formula for {0} in the hint, e.g. the level needed for the next upgrade level.")]
        [SerializeField] private JabelFormula conditionHintArgument = new JabelFormula();
        [Tooltip("Reset on prestige (Reset run block).")]
        [SerializeField] private bool resetOnRun = true;

        [Header("Effect")]
        [Tooltip("Runs once per bought unit. Locals: count (owned after purchase), amount (units in this purchase).\n" +
                 "Prefer derived values (formulas with count('id')) for permanent stat bonuses — they survive balance changes.")]
        [SerializeField] private JabelScript onBought = new JabelScript();

        public string Id => string.IsNullOrEmpty(id) ? name : id;
        public Sprite Icon => icon;
        public JabelFormula ConditionHintArgument => conditionHintArgument;

        /// <summary>Icon for the given owned count (multi-level upgrades show their next level).</summary>
        public Sprite IconFor(int count)
        {
            if (iconsByCount == null || iconsByCount.Count == 0) return icon;
            var sprite = iconsByCount[Mathf.Clamp(count, 0, iconsByCount.Count - 1)];
            return sprite != null ? sprite : icon;
        }
        public LocalizedString DisplayName => displayName;
        public LocalizedString Description => description;
        public string Tag => tag;
        public int SortOrder => sortOrder;
        public BuffCost Price => price;
        public int MaxCount => maxCount;
        public bool IsUnlimited => maxCount <= 0;
        public BuffVisibility Visibility => visibility;
        public IReadOnlyList<BuffRequirement> Requirements => requirements;
        public JabelFormula Condition => condition;
        public LocalizedString ConditionHint => conditionHint;
        public bool ResetOnRun => resetOnRun;
        public JabelScript OnBought => onBought;

        public abstract bool IsActive { get; }

        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(id)) id = name.Replace(' ', '_').ToLowerInvariant();
        }

#if UNITY_EDITOR
        /// <summary>Editor-only setter used by scene/asset builders.</summary>
        public void EditorSetup(string buffId, Sprite buffIcon, string nameKey, string descriptionKey, string buffTag,
            int order, BuffCost cost, int max, BuffVisibility vis, List<BuffRequirement> reqs, JabelScript bought,
            string conditionFormula = null, string conditionHintKey = null)
        {
            id = buffId;
            icon = buffIcon;
            displayName = new LocalizedString(nameKey);
            description = new LocalizedString(descriptionKey);
            tag = buffTag;
            sortOrder = order;
            price = cost;
            maxCount = max;
            visibility = vis;
            requirements = reqs ?? new List<BuffRequirement>();
            onBought = bought ?? new JabelScript();
            condition = new JabelFormula(conditionFormula ?? string.Empty);
            conditionHint = new LocalizedString(conditionHintKey);
        }
#endif
    }
}
