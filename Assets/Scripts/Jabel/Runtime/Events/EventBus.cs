using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jabel.Events
{
    /// <summary>Marker interface for every event that travels through the bus.</summary>
    public interface IJabelEvent { }

    public interface IEventBus
    {
        void Subscribe<T>(Action<T> handler) where T : struct, IJabelEvent;
        void Unsubscribe<T>(Action<T> handler) where T : struct, IJabelEvent;
        void Publish<T>(T evt) where T : struct, IJabelEvent;
        void Clear();
    }

    /// <summary>
    /// Typed, allocation-free (after warm-up) publish/subscribe hub. Everything in Jabel talks
    /// through it: systems never reference each other's concrete classes for notifications.
    /// Safe against subscribe/unsubscribe from inside a handler.
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        private interface IChannel { void Clear(); }

        private sealed class Channel<T> : IChannel where T : struct, IJabelEvent
        {
            private readonly List<Action<T>> _handlers = new List<Action<T>>();
            private int _publishDepth;
            private bool _hasRemovals;

            public void Add(Action<T> handler)
            {
                if (!_handlers.Contains(handler)) _handlers.Add(handler);
            }

            public void Remove(Action<T> handler)
            {
                int index = _handlers.IndexOf(handler);
                if (index < 0) return;
                if (_publishDepth > 0)
                {
                    // Defer structural changes until iteration completes.
                    _handlers[index] = null;
                    _hasRemovals = true;
                }
                else
                {
                    _handlers.RemoveAt(index);
                }
            }

            public void Publish(T evt)
            {
                _publishDepth++;
                try
                {
                    // Count is captured so handlers added during publish are not called this round.
                    int count = _handlers.Count;
                    for (int i = 0; i < count; i++)
                    {
                        var handler = _handlers[i];
                        if (handler == null) continue;
                        try
                        {
                            handler(evt);
                        }
                        catch (Exception ex)
                        {
                            // One faulty listener must not break the whole game loop.
                            Debug.LogException(ex);
                        }
                    }
                }
                finally
                {
                    _publishDepth--;
                    if (_publishDepth == 0 && _hasRemovals)
                    {
                        _handlers.RemoveAll(h => h == null);
                        _hasRemovals = false;
                    }
                }
            }

            public void Clear() => _handlers.Clear();
        }

        private readonly Dictionary<Type, IChannel> _channels = new Dictionary<Type, IChannel>();

        private Channel<T> GetChannel<T>() where T : struct, IJabelEvent
        {
            if (!_channels.TryGetValue(typeof(T), out var channel))
            {
                channel = new Channel<T>();
                _channels.Add(typeof(T), channel);
            }
            return (Channel<T>)channel;
        }

        public void Subscribe<T>(Action<T> handler) where T : struct, IJabelEvent
        {
            if (handler == null) return;
            GetChannel<T>().Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct, IJabelEvent
        {
            if (handler == null) return;
            if (_channels.TryGetValue(typeof(T), out var channel)) ((Channel<T>)channel).Remove(handler);
        }

        public void Publish<T>(T evt) where T : struct, IJabelEvent
        {
            if (_channels.TryGetValue(typeof(T), out var channel)) ((Channel<T>)channel).Publish(evt);
        }

        public void Clear()
        {
            foreach (var channel in _channels.Values) channel.Clear();
            _channels.Clear();
        }
    }
}
