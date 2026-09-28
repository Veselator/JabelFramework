using Jabel.Core;
using Jabel.Scripting;
using UnityEngine;
using UnityEngine.Events;

namespace Jabel.Events
{
    /// <summary>
    /// No-code hook into the most common bus events. Pick an event, optionally filter it,
    /// and respond with a UnityEvent and/or a JabelScript.
    /// </summary>
    [AddComponentMenu("Jabel/Events/Event Listener")]
    public class JabelEventListener : JabelBehaviour
    {
        public enum EventType
        {
            GameStarted,
            Tick,
            AnyBuffBought,
            ActiveBuffBought,
            PassiveBuffBought,
            BuffUnlocked,
            VariableChanged,
            Click,
            OfflineProgress,
            GameSaved
        }

        [SerializeField] private EventType eventType = EventType.AnyBuffBought;
        [Tooltip("Buff id or tag (buff events) / variable key (VariableChanged). Empty = any.")]
        [SerializeField] private string filter;
        [SerializeField] private UnityEvent onEvent = new UnityEvent();
        [SerializeField] private JabelScript response = new JabelScript();

        protected override void OnBind()
        {
            var bus = Manager.Events;
            switch (eventType)
            {
                case EventType.GameStarted: bus.Subscribe<GameStartedEvent>(OnGameStarted); break;
                case EventType.Tick: bus.Subscribe<TickEvent>(OnTick); break;
                case EventType.AnyBuffBought: bus.Subscribe<AnyBuffBoughtEvent>(OnAny); break;
                case EventType.ActiveBuffBought: bus.Subscribe<ActiveBuffBoughtEvent>(OnActive); break;
                case EventType.PassiveBuffBought: bus.Subscribe<PassiveBuffBoughtEvent>(OnPassive); break;
                case EventType.BuffUnlocked: bus.Subscribe<BuffUnlockedEvent>(OnUnlocked); break;
                case EventType.VariableChanged: bus.Subscribe<VariableChangedEvent>(OnVariable); break;
                case EventType.Click: bus.Subscribe<ClickEvent>(OnClick); break;
                case EventType.OfflineProgress: bus.Subscribe<OfflineProgressEvent>(OnOffline); break;
                case EventType.GameSaved: bus.Subscribe<GameSavedEvent>(OnSaved); break;
            }
        }

        protected override void OnUnbind()
        {
            var bus = Manager.Events;
            bus.Unsubscribe<GameStartedEvent>(OnGameStarted);
            bus.Unsubscribe<TickEvent>(OnTick);
            bus.Unsubscribe<AnyBuffBoughtEvent>(OnAny);
            bus.Unsubscribe<ActiveBuffBoughtEvent>(OnActive);
            bus.Unsubscribe<PassiveBuffBoughtEvent>(OnPassive);
            bus.Unsubscribe<BuffUnlockedEvent>(OnUnlocked);
            bus.Unsubscribe<VariableChangedEvent>(OnVariable);
            bus.Unsubscribe<ClickEvent>(OnClick);
            bus.Unsubscribe<OfflineProgressEvent>(OnOffline);
            bus.Unsubscribe<GameSavedEvent>(OnSaved);
        }

        private bool PassesBuff(Buffs.BaseBuff buff) =>
            string.IsNullOrEmpty(filter) || buff == null || buff.Id == filter || buff.Tag == filter;

        private void Fire()
        {
            onEvent.Invoke();
            if (!response.IsEmpty) Manager.RunScript(response);
        }

        private void OnGameStarted(GameStartedEvent e) => Fire();
        private void OnTick(TickEvent e) { if (!e.IsOffline) Fire(); }
        private void OnAny(AnyBuffBoughtEvent e) { if (PassesBuff(e.Data.Buff)) Fire(); }
        private void OnActive(ActiveBuffBoughtEvent e) { if (PassesBuff(e.Data.Buff)) Fire(); }
        private void OnPassive(PassiveBuffBoughtEvent e) { if (PassesBuff(e.Data.Buff)) Fire(); }
        private void OnUnlocked(BuffUnlockedEvent e) { if (PassesBuff(e.Buff)) Fire(); }
        private void OnVariable(VariableChangedEvent e) { if (string.IsNullOrEmpty(filter) || e.Key == filter) Fire(); }
        private void OnClick(ClickEvent e) => Fire();
        private void OnOffline(OfflineProgressEvent e) => Fire();
        private void OnSaved(GameSavedEvent e) => Fire();
    }
}
