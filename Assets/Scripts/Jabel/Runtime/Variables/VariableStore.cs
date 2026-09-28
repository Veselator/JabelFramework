using System;
using System.Collections.Generic;
using Jabel.Events;
using Jabel.Numbers;
using Jabel.Scripting;
using UnityEngine;

namespace Jabel.Variables
{
    /// <summary>
    /// Holds all global variables and derived values. Derived values are cached and invalidated by
    /// <see cref="Version"/>, which also changes whenever buffs change (via <see cref="Touch"/>).
    /// </summary>
    public sealed class VariableStore
    {
        private sealed class Entry
        {
            public BigNumber Value;
            public VariableDefinition Definition;
            public VariablePersistence Persistence;
            public RateTracker Rate;
        }

        private sealed class DerivedEntry
        {
            public DerivedValueDefinition Definition;
            public BigNumber Cached;
            public int CachedVersion = -1;
            public bool Evaluating;
        }

        private readonly Dictionary<string, Entry> _variables = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Dictionary<string, DerivedEntry> _derived = new Dictionary<string, DerivedEntry>(StringComparer.Ordinal);
        private readonly List<string> _countdowns = new List<string>();
        private readonly IEventBus _events;
        private readonly IFormulaContext _globalContext;

        /// <summary>Monotonic state counter; any change to variables or buffs bumps it.</summary>
        public int Version { get; private set; }

        /// <summary>While true, gains are not recorded in rate trackers (offline catch-up).</summary>
        public bool SuppressRateTracking { get; set; }

        public IEnumerable<string> Keys => _variables.Keys;
        public IEnumerable<string> DerivedKeys => _derived.Keys;

        public VariableStore(IEventBus events, IFormulaContext globalContext)
        {
            _events = events;
            _globalContext = globalContext;
        }

        // ------------------------------------------------------------ declaration

        public void Declare(VariableDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.key)) return;
            if (_variables.ContainsKey(definition.key))
            {
                Debug.LogWarning($"[Jabel] Variable '{definition.key}' declared twice.");
                return;
            }
            _variables[definition.key] = new Entry
            {
                Value = definition.initialValue,
                Definition = definition,
                Persistence = definition.persistence
            };
            if (definition.countdown) _countdowns.Add(definition.key);
            Version++;
        }

        /// <summary>Declares a variable at runtime (from a script). No-op when it exists.</summary>
        public void DeclareRuntime(string key, BigNumber initial, VariablePersistence persistence)
        {
            if (string.IsNullOrEmpty(key) || _variables.ContainsKey(key)) return;
            _variables[key] = new Entry { Value = initial, Persistence = persistence };
            Version++;
        }

        public void DeclareDerived(DerivedValueDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.key)) return;
            _derived[definition.key] = new DerivedEntry { Definition = definition };
        }

        public bool Exists(string key) => _variables.ContainsKey(key) || _derived.ContainsKey(key);
        public bool IsDerived(string key) => _derived.ContainsKey(key);

        public VariableDefinition GetDefinition(string key) =>
            _variables.TryGetValue(key, out var e) ? e.Definition : null;

        public DerivedValueDefinition GetDerivedDefinition(string key) =>
            _derived.TryGetValue(key, out var e) ? e.Definition : null;

        public VariablePersistence GetPersistence(string key) =>
            _variables.TryGetValue(key, out var e) ? e.Persistence : VariablePersistence.Session;

        public NumberFormat GetFormat(string key)
        {
            if (_variables.TryGetValue(key, out var e) && e.Definition != null) return e.Definition.format;
            if (_derived.TryGetValue(key, out var d)) return d.Definition.format;
            return NumberFormat.Default;
        }

        // ------------------------------------------------------------ access

        public bool TryGet(string key, out BigNumber value)
        {
            if (_variables.TryGetValue(key, out var entry))
            {
                value = entry.Value;
                return true;
            }
            if (_derived.TryGetValue(key, out var derived))
            {
                value = EvaluateDerived(derived);
                return true;
            }
            value = BigNumber.Zero;
            return false;
        }

        public BigNumber Get(string key) => TryGet(key, out var v) ? v : BigNumber.Zero;

        private BigNumber EvaluateDerived(DerivedEntry entry)
        {
            var formula = entry.Definition.formula;
            bool volatileFormula = formula.Compiled.IsVolatile;
            if (!volatileFormula && entry.CachedVersion == Version) return entry.Cached;

            if (entry.Evaluating)
            {
                Debug.LogError($"[Jabel] Derived value '{entry.Definition.key}' references itself.");
                return entry.Cached;
            }

            entry.Evaluating = true;
            try
            {
                entry.Cached = formula.Evaluate(_globalContext);
                entry.CachedVersion = Version;
            }
            finally
            {
                entry.Evaluating = false;
            }
            return entry.Cached;
        }

        public void Set(string key, BigNumber value)
        {
            if (!_variables.TryGetValue(key, out var entry))
            {
                if (_derived.ContainsKey(key))
                {
                    Debug.LogWarning($"[Jabel] '{key}' is a derived value and cannot be assigned.");
                    return;
                }
                // Implicit declaration keeps prototyping fast; it is reported once so typos surface.
                Debug.LogWarning($"[Jabel] Variable '{key}' was not declared; creating it (Run persistence).");
                entry = new Entry { Persistence = VariablePersistence.Run };
                _variables[key] = entry;
            }

            var def = entry.Definition;
            if (def != null)
            {
                if (def.clampMin && value < def.min) value = def.min;
                if (def.clampMax && value > def.max) value = def.max;
            }

            var old = entry.Value;
            if (old == value) return;

            entry.Value = value;
            Version++;

            if (entry.Rate != null && !SuppressRateTracking && value > old) entry.Rate.Add((value - old).ToDoubleClamped());

            _events?.Publish(new VariableChangedEvent { Key = key, OldValue = old, NewValue = value });
        }

        public void Add(string key, BigNumber delta) => Set(key, Get(key) + delta);

        /// <summary>Spends an amount if affordable. Returns false (and changes nothing) otherwise.</summary>
        public bool TrySpend(string key, BigNumber amount)
        {
            var current = Get(key);
            if (current < amount) return false;
            Set(key, current - amount);
            return true;
        }

        /// <summary>Marks state as changed without touching a variable (used by the buff system).</summary>
        public void Touch() => Version++;

        // ------------------------------------------------------------ rate

        public BigNumber GetRate(string key)
        {
            if (string.IsNullOrEmpty(key) || !_variables.TryGetValue(key, out var entry)) return BigNumber.Zero;
            entry.Rate ??= new RateTracker();
            return entry.Rate.PerSecond;
        }

        // ------------------------------------------------------------ ticks & persistence

        /// <summary>Advances countdown variables by <paramref name="ticks"/>.</summary>
        public void ProcessCountdowns(long ticks)
        {
            for (int i = 0; i < _countdowns.Count; i++)
            {
                string key = _countdowns[i];
                var value = Get(key);
                if (value <= BigNumber.Zero) continue;
                var next = value - ticks;
                Set(key, next < BigNumber.Zero ? BigNumber.Zero : next);
            }
        }

        public IEnumerable<KeyValuePair<string, BigNumber>> GetPersistent()
        {
            foreach (var pair in _variables)
                if (pair.Value.Persistence != VariablePersistence.Session)
                    yield return new KeyValuePair<string, BigNumber>(pair.Key, pair.Value.Value);
        }

        /// <summary>Restores a saved value without events (called before the game starts).</summary>
        public void Restore(string key, BigNumber value)
        {
            if (!_variables.TryGetValue(key, out var entry))
            {
                // Variable removed from the config since the save was made: keep it, it is harmless.
                entry = new Entry { Persistence = VariablePersistence.Run };
                _variables[key] = entry;
            }
            entry.Value = value;
            Version++;
        }

        /// <summary>Returns variables to their initial values. Permanent ones survive unless <paramref name="includePermanent"/>.</summary>
        public void ResetToInitial(bool includePermanent)
        {
            foreach (var pair in _variables)
            {
                var entry = pair.Value;
                if (!includePermanent && entry.Persistence == VariablePersistence.Permanent) continue;
                var initial = entry.Definition != null ? entry.Definition.initialValue : BigNumber.Zero;
                if (entry.Value != initial) Set(pair.Key, initial);
            }
        }

        /// <summary>Sliding-window gain counter backing rate('var').</summary>
        private sealed class RateTracker
        {
            private const int Buckets = 5;
            private readonly double[] _buckets = new double[Buckets];
            private long _currentSecond = -1;

            private static long Now => (long)Time.realtimeSinceStartupAsDouble;

            private void Rotate()
            {
                long now = Now;
                if (_currentSecond < 0) _currentSecond = now;
                while (_currentSecond < now)
                {
                    _currentSecond++;
                    _buckets[_currentSecond % Buckets] = 0;
                    if (now - _currentSecond > Buckets) _currentSecond = now - Buckets;
                }
            }

            public void Add(double amount)
            {
                Rotate();
                _buckets[_currentSecond % Buckets] += amount;
            }

            public BigNumber PerSecond
            {
                get
                {
                    Rotate();
                    double sum = 0;
                    // Exclude the current (partial) second for a stable reading.
                    for (int i = 1; i < Buckets; i++) sum += _buckets[(_currentSecond - i + Buckets * 1000) % Buckets];
                    return sum / (Buckets - 1);
                }
            }
        }
    }
}
