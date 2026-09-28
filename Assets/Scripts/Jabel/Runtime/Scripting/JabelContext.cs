using System.Collections.Generic;
using Jabel.Buffs;
using Jabel.Numbers;
using UnityEngine;

namespace Jabel.Scripting
{
    /// <summary>
    /// Execution context of a JabelScript run: local variables, the buff/instance that triggered it,
    /// batching information for offline catch-up, and a pointer back to the runtime.
    /// Contexts are pooled; always pair <see cref="Rent"/> with <see cref="Release"/>.
    /// </summary>
    public sealed class JabelContext : IFormulaContext
    {
        public const int MaxDepth = 64;

        private static readonly Stack<JabelContext> Pool = new Stack<JabelContext>();

        private readonly Dictionary<string, BigNumber> _locals = new Dictionary<string, BigNumber>(8);

        public IJabelRuntime Runtime { get; private set; }

        /// <summary>Buff whose script is running (null for global scripts).</summary>
        public BaseBuff Buff { get; set; }

        /// <summary>Active buff instance whose script is running (instanced buffs only).</summary>
        public BuffInstance Instance { get; set; }

        /// <summary>
        /// How many activations this single run represents. 1 while playing; larger during offline or
        /// lag catch-up, where scripts run once per instance instead of thousands of times.
        /// "Modify variable" and "Call function" blocks multiply by it automatically.
        /// </summary>
        public long Batch { get; set; } = 1;

        public bool IsOffline { get; set; }

        /// <summary>Optional scene object that caused the run (click area, listener...).</summary>
        public Object Source { get; set; }

        /// <summary>World position of the trigger, when meaningful (clicks).</summary>
        public Vector3 Position { get; set; }
        public bool HasPosition { get; set; }

        public int Depth { get; set; }

        public IReadOnlyDictionary<string, BigNumber> Locals => _locals;

        private JabelContext() { }

        public static JabelContext Rent(IJabelRuntime runtime)
        {
            var ctx = Pool.Count > 0 ? Pool.Pop() : new JabelContext();
            ctx.Runtime = runtime;
            return ctx;
        }

        /// <summary>Child context that inherits trigger info but has fresh locals (used for sub-scripts).</summary>
        public JabelContext CreateChild()
        {
            var child = Rent(Runtime);
            child.Buff = Buff;
            child.Instance = Instance;
            child.Batch = Batch;
            child.IsOffline = IsOffline;
            child.Source = Source;
            child.Position = Position;
            child.HasPosition = HasPosition;
            child.Depth = Depth + 1;
            return child;
        }

        public void Release()
        {
            _locals.Clear();
            Runtime = null;
            Buff = null;
            Instance = null;
            Batch = 1;
            IsOffline = false;
            Source = null;
            Position = default;
            HasPosition = false;
            Depth = 0;
            if (Pool.Count < 64) Pool.Push(this);
        }

        public void SetLocal(string name, BigNumber value)
        {
            if (!string.IsNullOrEmpty(name)) _locals[name] = value;
        }

        public bool TryGetLocal(string name, out BigNumber value) => _locals.TryGetValue(name, out value);

        public BigNumber GetLocal(string name, BigNumber fallback = default) =>
            _locals.TryGetValue(name, out var v) ? v : fallback;

        public bool HasLocal(string name) => _locals.ContainsKey(name);

        /// <summary>Lookup order: locals, instance variables, then global (derived values, variables, built-ins).</summary>
        public bool TryResolve(string name, out BigNumber value)
        {
            if (_locals.TryGetValue(name, out value)) return true;
            if (Instance != null && Instance.TryGetVariable(name, out value)) return true;
            if (Runtime != null) return Runtime.TryResolve(name, out value);
            value = BigNumber.Zero;
            return false;
        }
    }
}
