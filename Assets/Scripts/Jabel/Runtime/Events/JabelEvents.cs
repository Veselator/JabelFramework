using Jabel.Buffs;
using Jabel.Numbers;
using Jabel.Scripting;
using UnityEngine;

namespace Jabel.Events
{
    /// <summary>OnTick: one simulation step. Batch &gt; 1 when several ticks were collapsed (lag catch-up).</summary>
    public struct TickEvent : IJabelEvent
    {
        public long Tick;
        public long Batch;
        public bool IsOffline;
    }

    public struct BuffBoughtEvent
    {
        public BaseBuff Buff;
        public int Amount;
        public int NewCount;
        public bool WasFree;
    }

    /// <summary>OnActiveBuffBought.</summary>
    public struct ActiveBuffBoughtEvent : IJabelEvent { public BuffBoughtEvent Data; }

    /// <summary>OnPassiveBuffBought.</summary>
    public struct PassiveBuffBoughtEvent : IJabelEvent { public BuffBoughtEvent Data; }

    /// <summary>OnAnyBuffBought.</summary>
    public struct AnyBuffBoughtEvent : IJabelEvent { public BuffBoughtEvent Data; }

    public struct BuffUnlockedEvent : IJabelEvent { public BaseBuff Buff; }

    /// <summary>A new unit of an instanced active buff exists (bought, granted, or restored from a save).</summary>
    public struct BuffInstanceCreatedEvent : IJabelEvent
    {
        public BuffInstance Instance;
        public bool IsRestored;
    }

    public struct BuffInstanceLevelUpEvent : IJabelEvent
    {
        public BuffInstance Instance;
        public int NewLevel;
    }

    /// <summary>An active buff (or one of its instances) fired its OnTick script.</summary>
    public struct BuffActivatedEvent : IJabelEvent
    {
        public ActiveBuff Buff;
        public BuffInstance Instance;
        public long Batch;
        public bool IsOffline;
    }

    /// <summary>OnFunction: raised for every "Call function" block.</summary>
    public struct FunctionCalledEvent : IJabelEvent
    {
        public string Name;
        public FunctionArgs Args;
        public JabelContext Context;
    }

    public struct VariableChangedEvent : IJabelEvent
    {
        public string Key;
        public BigNumber OldValue;
        public BigNumber NewValue;
    }

    /// <summary>A click was processed. Value = local 'value' set by the click script (for feedback scale).</summary>
    public struct ClickEvent : IJabelEvent
    {
        public Vector3 WorldPosition;
        public Vector2 ScreenPosition;
        public BigNumber Value;
        public bool IsCritical;
        public Object Source;
    }

    public struct GameStartedEvent : IJabelEvent
    {
        public bool IsNewGame;
    }

    public struct GameSavedEvent : IJabelEvent { }

    public struct RunResetEvent : IJabelEvent { }

    /// <summary>Raised after offline progress was applied. Report lists variable changes.</summary>
    public struct OfflineProgressEvent : IJabelEvent
    {
        public double SecondsAway;
        public double SecondsSimulated;
        public long Ticks;
        public Jabel.Save.OfflineReport Report;
    }

    public enum NotificationStyle { Info, Success, Warning, Achievement }

    public struct NotificationEvent : IJabelEvent
    {
        public string Text;
        public NotificationStyle Style;
        public Sprite Icon;
    }
}
