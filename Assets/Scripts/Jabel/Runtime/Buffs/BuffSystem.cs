using System;
using System.Collections.Generic;
using Jabel.Events;
using Jabel.Localization;
using Jabel.Numbers;
using Jabel.Save;
using Jabel.Scripting;

namespace Jabel.Buffs
{
    public enum BuffAvailability { Available, TooExpensive, Locked, MaxedOut }

    /// <summary>Mutable runtime state of one buff type.</summary>
    public sealed class BuffState
    {
        public BaseBuff Buff { get; }
        public int Count { get; internal set; }
        public bool Unlocked { get; internal set; }
        /// <summary>Timer of non-instanced active buffs.</summary>
        public long TickCounter { get; internal set; }
        public List<BuffInstance> Instances { get; } = new List<BuffInstance>();

        internal BuffState(BaseBuff buff) { Buff = buff; }
    }

    /// <summary>
    /// Owns buff ownership, prices, unlocks, instance levels and periodic activation.
    /// All mutations go through here so events and state versioning are consistent.
    /// </summary>
    public sealed class BuffSystem
    {
        public const string LocalCount = "count";
        public const string LocalAmount = "amount";
        public const string LocalLevel = "level";
        public const string LocalIndex = "index";

        private readonly IJabelRuntime _runtime;
        private readonly IEventBus _events;
        private readonly List<BuffState> _states = new List<BuffState>();
        private readonly List<BuffState> _activeStates = new List<BuffState>();
        private readonly Dictionary<string, BuffState> _byId = new Dictionary<string, BuffState>(StringComparer.Ordinal);
        private readonly List<BaseBuff> _all = new List<BaseBuff>();
        private readonly System.Random _random = new System.Random();
        private bool _suppressEvents;

        public IReadOnlyList<BaseBuff> All => _all;

        public BuffSystem(IJabelRuntime runtime, IEventBus events, IEnumerable<BaseBuff> buffs)
        {
            _runtime = runtime;
            _events = events;
            foreach (var buff in buffs)
            {
                if (buff == null) continue;
                if (_byId.ContainsKey(buff.Id))
                {
                    UnityEngine.Debug.LogError($"[Jabel] Duplicate buff id '{buff.Id}' ({buff.name}).");
                    continue;
                }
                var state = new BuffState(buff);
                _states.Add(state);
                _byId[buff.Id] = state;
                _all.Add(buff);
                if (buff.IsActive) _activeStates.Add(state);
            }
            _all.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        }

        // ------------------------------------------------------------ queries

        public BaseBuff Find(string id) => id != null && _byId.TryGetValue(id, out var s) ? s.Buff : null;

        public BuffState GetState(BaseBuff buff) => buff != null && _byId.TryGetValue(buff.Id, out var s) ? s : null;

        public int GetCount(string id) => id != null && _byId.TryGetValue(id, out var s) ? s.Count : 0;
        public int GetCount(BaseBuff buff) => GetState(buff)?.Count ?? 0;

        public IReadOnlyList<BuffInstance> GetInstances(ActiveBuff buff) =>
            (IReadOnlyList<BuffInstance>)GetState(buff)?.Instances ?? Array.Empty<BuffInstance>();

        public int GetTotalLevels(string id)
        {
            if (id == null || !_byId.TryGetValue(id, out var s)) return 0;
            int total = 0;
            foreach (var i in s.Instances) total += i.Level;
            return total;
        }

        public int GetHighestLevel(string id)
        {
            if (id == null || !_byId.TryGetValue(id, out var s)) return 0;
            int max = 0;
            foreach (var i in s.Instances) max = Math.Max(max, i.Level);
            return max;
        }

        public bool IsMaxed(BaseBuff buff) => !buff.IsUnlimited && GetCount(buff) >= buff.MaxCount;

        public bool RequirementsMet(BaseBuff buff)
        {
            foreach (var req in buff.Requirements)
                if (_runtime.Variables.Get(req.variable) < req.minimum) return false;
            return buff.Condition.Check(_runtime);
        }

        public bool IsUnlocked(string id) => id != null && _byId.TryGetValue(id, out var s) && (s.Unlocked || RequirementsMet(s.Buff));
        public bool IsUnlocked(BaseBuff buff) => buff != null && IsUnlocked(buff.Id);

        public bool IsVisible(BaseBuff buff)
        {
            switch (buff.Visibility)
            {
                case BuffVisibility.Hidden: return false;
                case BuffVisibility.WhenUnlocked: return IsUnlocked(buff) || GetCount(buff) > 0;
                default: return true;
            }
        }

        /// <summary>Localized explanation of why a buff is locked, or null.</summary>
        public string GetLockReason(BaseBuff buff)
        {
            foreach (var req in buff.Requirements)
            {
                if (_runtime.Variables.Get(req.variable) >= req.minimum) continue;
                var def = _runtime.Variables.GetDefinition(req.variable);
                string varName = def != null && !def.displayName.IsEmpty ? def.displayName.Resolve() : req.variable;
                string value = NumberFormatter.Format(req.minimum, _runtime.Variables.GetFormat(req.variable));
                return Loc.Format("jabel.requires", varName, value);
            }
            if (!buff.Condition.Check(_runtime))
            {
                if (buff.ConditionHint.IsEmpty) return Loc.Get("jabel.locked");
                if (buff.ConditionHintArgument.IsEmpty) return buff.ConditionHint.Resolve();
                return buff.ConditionHint.Resolve(NumberFormatter.Format(buff.ConditionHintArgument.Evaluate(_runtime)));
            }
            return null;
        }

        public BigNumber GetPrice(BaseBuff buff, int amount = 1)
        {
            var ctx = RentContext(buff, null);
            try
            {
                return buff.Price.TotalPrice(GetCount(buff), amount, ctx);
            }
            finally
            {
                ctx.Release();
            }
        }

        public int GetMaxAffordable(BaseBuff buff)
        {
            int owned = GetCount(buff);
            long limit = buff.IsUnlimited ? int.MaxValue : buff.MaxCount - owned;
            var ctx = RentContext(buff, null);
            try
            {
                return (int)buff.Price.MaxAffordable(owned, _runtime.Variables.Get(buff.Price.currency), limit, ctx);
            }
            finally
            {
                ctx.Release();
            }
        }

        public BuffAvailability GetAvailability(BaseBuff buff, int amount = 1)
        {
            if (IsMaxed(buff)) return BuffAvailability.MaxedOut;
            // Requirements unlock (sticky); the extra condition gates every single purchase,
            // which is what multi-level upgrades need ("next level at player level N").
            if (!IsUnlocked(buff) || !buff.Condition.Check(_runtime)) return BuffAvailability.Locked;
            return _runtime.Variables.Get(buff.Price.currency) >= GetPrice(buff, amount)
                ? BuffAvailability.Available
                : BuffAvailability.TooExpensive;
        }

        // ------------------------------------------------------------ purchase

        /// <summary>
        /// Buys <paramref name="amount"/> units (all or nothing). amount &lt;= 0 buys as many as affordable.
        /// Returns the number of units bought.
        /// </summary>
        public int TryBuy(BaseBuff buff, int amount = 1)
        {
            var state = GetState(buff);
            if (state == null || !IsUnlocked(buff) || !buff.Condition.Check(_runtime)) return 0;

            if (amount <= 0) amount = GetMaxAffordable(buff);
            if (!buff.IsUnlimited) amount = Math.Min(amount, buff.MaxCount - state.Count);
            if (amount <= 0) return 0;

            var price = GetPrice(buff, amount);
            if (!_runtime.Variables.TrySpend(buff.Price.currency, price)) return 0;

            AddUnits(state, amount, free: false);
            return amount;
        }

        /// <summary>Adds units without paying (rewards, scripts).</summary>
        public void Grant(BaseBuff buff, int amount)
        {
            var state = GetState(buff);
            if (state == null || amount <= 0) return;
            if (!buff.IsUnlimited) amount = Math.Min(amount, buff.MaxCount - state.Count);
            if (amount > 0) AddUnits(state, amount, free: true);
        }

        public void ForceUnlock(BaseBuff buff)
        {
            var state = GetState(buff);
            if (state == null || state.Unlocked) return;
            state.Unlocked = true;
            _runtime.Variables.Touch();
            if (!_suppressEvents) _events.Publish(new BuffUnlockedEvent { Buff = buff });
        }

        private void AddUnits(BuffState state, int amount, bool free)
        {
            var buff = state.Buff;
            state.Unlocked = true;

            for (int i = 0; i < amount; i++)
            {
                state.Count++;
                BuffInstance instance = null;
                if (buff is ActiveBuff active && active.Instanced)
                {
                    instance = new BuffInstance(active, state.Instances.Count, _random.Next());
                    if (active.StaggerInstances)
                    {
                        long period = Math.Max(1, GetPeriod(active, instance));
                        instance.TickCounter = _random.Next(0, (int)Math.Min(period, int.MaxValue));
                    }
                    state.Instances.Add(instance);
                }

                _runtime.Variables.Touch();

                if (!buff.OnBought.IsEmpty)
                {
                    var ctx = RentContext(buff, instance);
                    ctx.SetLocal(LocalAmount, amount);
                    try { buff.OnBought.Run(ctx); }
                    finally { ctx.Release(); }
                }

                if (instance != null) _events.Publish(new BuffInstanceCreatedEvent { Instance = instance, IsRestored = false });
            }

            var evt = new BuffBoughtEvent { Buff = buff, Amount = amount, NewCount = state.Count, WasFree = free };
            if (buff.IsActive) _events.Publish(new ActiveBuffBoughtEvent { Data = evt });
            else _events.Publish(new PassiveBuffBoughtEvent { Data = evt });
            _events.Publish(new AnyBuffBoughtEvent { Data = evt });
        }

        // ------------------------------------------------------------ instance levels

        public BigNumber GetLevelUpPrice(BuffInstance instance)
        {
            var ctx = RentContext(instance.Buff, instance);
            try
            {
                return instance.Buff.LevelUpPrice.PriceAt(instance.Level, ctx);
            }
            finally
            {
                ctx.Release();
            }
        }

        public BuffAvailability GetLevelUpAvailability(BuffInstance instance)
        {
            var buff = instance.Buff;
            if (instance.Level >= buff.MaxLevel) return BuffAvailability.MaxedOut;
            if (!CheckLevelUpCondition(instance)) return BuffAvailability.Locked;
            return _runtime.Variables.Get(buff.LevelUpPrice.currency) >= GetLevelUpPrice(instance)
                ? BuffAvailability.Available
                : BuffAvailability.TooExpensive;
        }

        public string GetLevelUpLockReason(BuffInstance instance)
        {
            if (CheckLevelUpCondition(instance)) return null;
            var hint = instance.Buff.LevelUpConditionHint;
            if (hint.IsEmpty) return Loc.Get("jabel.locked");
            // The hint may reference the next level: "Requires player level {0}".
            return hint.Resolve(instance.Level + 1);
        }

        private bool CheckLevelUpCondition(BuffInstance instance)
        {
            var condition = instance.Buff.LevelUpCondition;
            if (condition.IsEmpty) return true;
            var ctx = RentContext(instance.Buff, instance);
            try { return condition.Check(ctx); }
            finally { ctx.Release(); }
        }

        public bool TryLevelUp(BuffInstance instance)
        {
            if (GetLevelUpAvailability(instance) != BuffAvailability.Available) return false;
            var buff = instance.Buff;
            if (!_runtime.Variables.TrySpend(buff.LevelUpPrice.currency, GetLevelUpPrice(instance))) return false;

            instance.Level++;
            _runtime.Variables.Touch();

            if (!buff.OnLevelUp.IsEmpty)
            {
                var ctx = RentContext(buff, instance);
                try { buff.OnLevelUp.Run(ctx); }
                finally { ctx.Release(); }
            }

            _events.Publish(new BuffInstanceLevelUpEvent { Instance = instance, NewLevel = instance.Level });
            return true;
        }

        /// <summary>Activation period in ticks for an instance (or the whole buff when instance is null).</summary>
        public long GetPeriod(ActiveBuff buff, BuffInstance instance)
        {
            var ctx = RentContext(buff, instance);
            try
            {
                return Math.Max(1, buff.Period.Evaluate(ctx, 1).ToLong());
            }
            finally
            {
                ctx.Release();
            }
        }

        // ------------------------------------------------------------ ticking

        /// <summary>
        /// Advances all active buffs by <paramref name="ticks"/>. With ticks &gt; 1 each instance runs its
        /// script once with Batch = number of activations (offline / lag catch-up).
        /// </summary>
        public void Tick(long ticks, bool offline)
        {
            for (int s = 0; s < _activeStates.Count; s++)
            {
                var state = _activeStates[s];
                if (state.Count <= 0) continue;
                var buff = (ActiveBuff)state.Buff;

                if (buff.Instanced)
                {
                    var instances = state.Instances;
                    for (int i = 0; i < instances.Count; i++)
                    {
                        var instance = instances[i];
                        long period = GetPeriod(buff, instance);
                        instance.TickCounter += ticks;
                        if (instance.TickCounter < period) continue;

                        long activations = instance.TickCounter / period;
                        instance.TickCounter %= period;
                        instance.Activations += activations;
                        Activate(buff, instance, activations, offline);
                    }
                }
                else
                {
                    long period = GetPeriod(buff, null);
                    state.TickCounter += ticks;
                    if (state.TickCounter < period) continue;
                    long activations = state.TickCounter / period;
                    state.TickCounter %= period;
                    Activate(buff, null, activations, offline);
                }
            }

            CheckUnlocks();
        }

        private void Activate(ActiveBuff buff, BuffInstance instance, long batch, bool offline)
        {
            var ctx = RentContext(buff, instance);
            ctx.Batch = batch;
            ctx.IsOffline = offline;
            try
            {
                buff.OnTick.Run(ctx);
            }
            finally
            {
                ctx.Release();
            }

            if (!_suppressEvents)
                _events.Publish(new BuffActivatedEvent { Buff = buff, Instance = instance, Batch = batch, IsOffline = offline });
        }

        /// <summary>Marks newly qualifying buffs as unlocked (sticky) and announces them.</summary>
        public void CheckUnlocks()
        {
            for (int i = 0; i < _states.Count; i++)
            {
                var state = _states[i];
                if (state.Unlocked || !RequirementsMet(state.Buff)) continue;
                state.Unlocked = true;
                if (!_suppressEvents) _events.Publish(new BuffUnlockedEvent { Buff = state.Buff });
            }
        }

        private JabelContext RentContext(BaseBuff buff, BuffInstance instance)
        {
            var ctx = JabelContext.Rent(_runtime);
            ctx.Buff = buff;
            ctx.Instance = instance;
            var state = GetState(buff);
            ctx.SetLocal(LocalCount, state?.Count ?? 0);
            if (instance != null)
            {
                ctx.SetLocal(LocalLevel, instance.Level);
                ctx.SetLocal(LocalIndex, instance.Index);
            }
            return ctx;
        }

        // ------------------------------------------------------------ persistence

        public List<SavedBuff> Capture()
        {
            var list = new List<SavedBuff>(_states.Count);
            foreach (var state in _states)
            {
                if (state.Count == 0 && !state.Unlocked) continue;
                var saved = new SavedBuff
                {
                    id = state.Buff.Id,
                    count = state.Count,
                    unlocked = state.Unlocked,
                    tickCounter = state.TickCounter
                };
                foreach (var instance in state.Instances)
                {
                    var si = new SavedInstance
                    {
                        level = instance.Level,
                        tickCounter = instance.TickCounter,
                        seed = instance.Seed,
                        activations = instance.Activations
                    };
                    foreach (var pair in instance.Variables)
                        si.variables.Add(new SavedValue(pair.Key, pair.Value.ToInvariantString()));
                    saved.instances.Add(si);
                }
                list.Add(saved);
            }
            return list;
        }

        /// <summary>Restores ownership without running OnBought (effects live in variables/derived values).</summary>
        public void Restore(List<SavedBuff> saved)
        {
            if (saved == null) return;
            _suppressEvents = true;
            try
            {
                foreach (var entry in saved)
                {
                    if (entry == null || !_byId.TryGetValue(entry.id, out var state)) continue;
                    var buff = state.Buff;
                    state.Count = buff.IsUnlimited ? entry.count : Math.Min(entry.count, buff.MaxCount);
                    state.Unlocked = entry.unlocked || state.Count > 0;
                    state.TickCounter = entry.tickCounter;
                    state.Instances.Clear();

                    if (buff is ActiveBuff active && active.Instanced)
                    {
                        for (int i = 0; i < state.Count; i++)
                        {
                            var si = i < entry.instances.Count ? entry.instances[i] : null;
                            var instance = new BuffInstance(active, i, si?.seed ?? _random.Next())
                            {
                                Level = Math.Max(1, Math.Min(active.MaxLevel, si?.level ?? 1)),
                                TickCounter = si?.tickCounter ?? 0,
                                Activations = si?.activations ?? 0
                            };
                            if (si != null)
                                foreach (var v in si.variables) instance.SetVariable(v.key, BigNumber.Parse(v.value));
                            state.Instances.Add(instance);
                        }
                    }
                }
            }
            finally
            {
                _suppressEvents = false;
            }
            _runtime.Variables.Touch();
        }

        public void ResetRun()
        {
            foreach (var state in _states)
            {
                if (!state.Buff.ResetOnRun) continue;
                state.Count = 0;
                state.Unlocked = false;
                state.TickCounter = 0;
                state.Instances.Clear();
            }
            _runtime.Variables.Touch();
        }
    }
}
