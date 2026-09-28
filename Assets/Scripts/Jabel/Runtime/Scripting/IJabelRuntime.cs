using Jabel.Buffs;
using Jabel.Events;
using Jabel.Variables;
using UnityEngine;

namespace Jabel.Scripting
{
    /// <summary>
    /// Services a script can reach. Implemented by the clicker session; blocks depend on this
    /// abstraction only, so they can be unit-tested or hosted outside ClickerManager.
    /// </summary>
    public interface IJabelRuntime : IFormulaContext
    {
        IEventBus Events { get; }
        VariableStore Variables { get; }
        BuffSystem Buffs { get; }
        FunctionRegistry Functions { get; }

        long CurrentTick { get; }
        float TickDuration { get; }

        void PlaySound(AudioClip clip, float volume);
        void RequestSave();
        /// <summary>Resets the run (prestige): Run variables and resettable buffs return to initial state.</summary>
        void ResetRun();
    }
}
