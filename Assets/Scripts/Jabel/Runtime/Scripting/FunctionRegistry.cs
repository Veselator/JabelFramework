using System;
using System.Collections.Generic;
using Jabel.Events;
using Jabel.Numbers;

namespace Jabel.Scripting
{
    /// <summary>Named arguments of a function call. Pooled, do not keep references after the call.</summary>
    public sealed class FunctionArgs
    {
        private static readonly Stack<FunctionArgs> Pool = new Stack<FunctionArgs>();

        private readonly List<string> _names = new List<string>(4);
        private readonly List<BigNumber> _values = new List<BigNumber>(4);

        /// <summary>Value a handler may return; stored into a local by the calling block.</summary>
        public BigNumber Result { get; set; }
        public bool HasResult { get; set; }

        public int Count => _names.Count;
        public string NameAt(int index) => _names[index];
        public BigNumber ValueAt(int index) => _values[index];

        public static FunctionArgs Rent() => Pool.Count > 0 ? Pool.Pop() : new FunctionArgs();

        public void Release()
        {
            _names.Clear();
            _values.Clear();
            Result = BigNumber.Zero;
            HasResult = false;
            if (Pool.Count < 32) Pool.Push(this);
        }

        public FunctionArgs Set(string name, BigNumber value)
        {
            int index = _names.IndexOf(name);
            if (index >= 0) _values[index] = value;
            else
            {
                _names.Add(name);
                _values.Add(value);
            }
            return this;
        }

        public bool TryGet(string name, out BigNumber value)
        {
            int index = _names.IndexOf(name);
            value = index >= 0 ? _values[index] : BigNumber.Zero;
            return index >= 0;
        }

        public BigNumber Get(string name, BigNumber fallback = default) => TryGet(name, out var v) ? v : fallback;

        /// <summary>First argument, whatever its name (for single-argument functions).</summary>
        public BigNumber First(BigNumber fallback = default) => _values.Count > 0 ? _values[0] : fallback;

        public void Return(BigNumber value)
        {
            Result = value;
            HasResult = true;
        }
    }

    public delegate void JabelFunctionHandler(JabelContext context, FunctionArgs args);

    /// <summary>
    /// Bridge between data (the "Call function" block) and code. C# systems register handlers by name;
    /// scene objects can listen through the event bus (<see cref="FunctionCalledEvent"/>) or a
    /// FunctionListener component. A call reaches all of them.
    /// </summary>
    public sealed class FunctionRegistry
    {
        private readonly Dictionary<string, List<JabelFunctionHandler>> _handlers =
            new Dictionary<string, List<JabelFunctionHandler>>(StringComparer.Ordinal);

        private readonly IEventBus _events;

        public FunctionRegistry(IEventBus events)
        {
            _events = events;
        }

        public IEnumerable<string> RegisteredNames => _handlers.Keys;

        public void Register(string name, JabelFunctionHandler handler)
        {
            if (string.IsNullOrEmpty(name) || handler == null) return;
            if (!_handlers.TryGetValue(name, out var list))
            {
                list = new List<JabelFunctionHandler>();
                _handlers[name] = list;
            }
            if (!list.Contains(handler)) list.Add(handler);
        }

        public void Unregister(string name, JabelFunctionHandler handler)
        {
            if (name != null && _handlers.TryGetValue(name, out var list)) list.Remove(handler);
        }

        public bool HasHandler(string name) => _handlers.TryGetValue(name, out var list) && list.Count > 0;

        /// <summary>Invokes all handlers and publishes <see cref="FunctionCalledEvent"/>.</summary>
        public void Call(string name, JabelContext context, FunctionArgs args)
        {
            if (string.IsNullOrEmpty(name)) return;

            if (_handlers.TryGetValue(name, out var list))
            {
                // Copy-free iteration tolerant to removal of the current handler.
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (i >= list.Count) continue;
                    try
                    {
                        list[i](context, args);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogException(ex);
                    }
                }
            }

            _events?.Publish(new FunctionCalledEvent { Name = name, Args = args, Context = context });
        }
    }
}
