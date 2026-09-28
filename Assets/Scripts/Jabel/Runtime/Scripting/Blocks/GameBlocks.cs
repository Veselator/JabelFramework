using System;
using System.Collections.Generic;
using System.Text;
using Jabel.Buffs;
using Jabel.Events;
using Jabel.Localization;
using Jabel.Numbers;
using UnityEngine;

namespace Jabel.Scripting.Blocks
{
    [Serializable]
    [JabelBlock("Buffs/Grant buff", "#2E6FB5", Help = "Gives buffs for free (rewards, starting kits). Runs their OnBought script.")]
    public class GrantBuffBlock : JabelBlock
    {
        public BaseBuff buff;
        public JabelFormula amount = new JabelFormula("1");

        public override BlockResult Execute(JabelContext context)
        {
            if (buff != null) context.Runtime.Buffs.Grant(buff, amount.Evaluate(context).ToInt());
            return BlockResult.Continue;
        }

        public override string Describe() => $"grant {amount} x {(buff != null ? buff.name : "<none>")}";
    }

    [Serializable]
    [JabelBlock("Buffs/Buy buff", "#2E6FB5", Help = "Attempts to buy buffs with the normal price. Result local 'bought' = amount bought.")]
    public class BuyBuffBlock : JabelBlock
    {
        public BaseBuff buff;
        public JabelFormula amount = new JabelFormula("1");

        public override BlockResult Execute(JabelContext context)
        {
            if (buff == null) return BlockResult.Continue;
            int bought = context.Runtime.Buffs.TryBuy(buff, amount.Evaluate(context).ToInt());
            context.SetLocal("bought", bought);
            return BlockResult.Continue;
        }

        public override string Describe() => $"buy {amount} x {(buff != null ? buff.name : "<none>")}";
    }

    [Serializable]
    [JabelBlock("Buffs/Unlock buff", "#2E6FB5", Help = "Unlocks a buff regardless of its requirements (story rewards, secrets).")]
    public class UnlockBuffBlock : JabelBlock
    {
        public BaseBuff buff;

        public override BlockResult Execute(JabelContext context)
        {
            if (buff != null) context.Runtime.Buffs.ForceUnlock(buff);
            return BlockResult.Continue;
        }

        public override string Describe() => $"unlock {(buff != null ? buff.name : "<none>")}";
    }

    [Serializable]
    [JabelBlock("Game/Reset run (prestige)", "#9E3A3A",
        Help = "Resets Run variables and resettable buffs to their initial state. Permanent variables survive.")]
    public class ResetRunBlock : JabelBlock
    {
        public override BlockResult Execute(JabelContext context)
        {
            context.Runtime.ResetRun();
            return BlockResult.Return;
        }

        public override string Describe() => "reset run";
    }

    [Serializable]
    [JabelBlock("Game/Save game", "#9E3A3A")]
    public class SaveGameBlock : JabelBlock
    {
        public override BlockResult Execute(JabelContext context)
        {
            context.Runtime.RequestSave();
            return BlockResult.Continue;
        }

        public override string Describe() => "save";
    }

    [Serializable]
    [JabelBlock("Feedback/Notify", "#C29B2E",
        Help = "Shows a toast. The text is a localization key; arguments are formulas formatted as numbers ({0}, {1}...).")]
    public class NotifyBlock : JabelBlock
    {
        public LocalizedString message = new LocalizedString("notify.example");
        public List<JabelFormula> arguments = new List<JabelFormula>();
        public NotificationStyle style = NotificationStyle.Info;
        public Sprite icon;
        [Tooltip("Do not show during offline catch-up.")]
        public bool skipWhenOffline = true;

        public NotifyBlock() { }

        public NotifyBlock(string key, NotificationStyle style, params string[] args)
        {
            message = new LocalizedString(key);
            this.style = style;
            foreach (var a in args) arguments.Add(new JabelFormula(a));
        }

        public override BlockResult Execute(JabelContext context)
        {
            if (skipWhenOffline && context.IsOffline) return BlockResult.Continue;

            object[] args = new object[arguments.Count];
            for (int i = 0; i < arguments.Count; i++) args[i] = NumberFormatter.Format(arguments[i].Evaluate(context));

            context.Runtime.Events.Publish(new NotificationEvent
            {
                Text = message.Resolve(args),
                Style = style,
                Icon = icon
            });
            return BlockResult.Continue;
        }

        public override string Describe() => $"notify \"{message.Key}\"";
    }

    [Serializable]
    [JabelBlock("Feedback/Play sound", "#C29B2E", Help = "Plays a Sound Cue (random variation and pitch) or a single clip.")]
    public class PlaySoundBlock : JabelBlock
    {
        public Jabel.Audio.SoundCue cue;
        [Tooltip("Used when no cue is set.")]
        public AudioClip clip;
        [Range(0, 1)] public float volume = 1;

        public override BlockResult Execute(JabelContext context)
        {
            if (context.IsOffline) return BlockResult.Continue;
            if (cue != null) Jabel.Audio.JabelAudio.Play(cue);
            else if (clip != null) context.Runtime.PlaySound(clip, volume);
            return BlockResult.Continue;
        }

        public override string Describe() => "play " + (cue != null ? cue.name : clip != null ? clip.name : "<none>");
    }

    [Serializable]
    [JabelBlock("Feedback/Log", "#3A3F44", Help = "Debug log. Formulas inside braces are evaluated: \"money is {money}\".")]
    public class LogBlock : JabelBlock
    {
        public string message = "money = {money}";

        [NonSerialized] private string _parsedFor;
        [NonSerialized] private List<object> _parts;

        public override BlockResult Execute(JabelContext context)
        {
            Debug.Log("[JabelScript] " + Interpolate(context));
            return BlockResult.Continue;
        }

        private string Interpolate(JabelContext context)
        {
            if (_parsedFor != message) Parse();
            var sb = new StringBuilder();
            foreach (var part in _parts)
            {
                if (part is JabelFormula f) sb.Append(NumberFormatter.Format(f.Evaluate(context)));
                else sb.Append(part);
            }
            return sb.ToString();
        }

        private void Parse()
        {
            _parsedFor = message;
            _parts = new List<object>();
            string text = message ?? string.Empty;
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('{', i);
                if (open < 0) { _parts.Add(text.Substring(i)); break; }
                int close = text.IndexOf('}', open);
                if (close < 0) { _parts.Add(text.Substring(i)); break; }
                if (open > i) _parts.Add(text.Substring(i, open - i));
                _parts.Add(new JabelFormula(text.Substring(open + 1, close - open - 1)));
                i = close + 1;
            }
        }

        public override string Describe() => "log \"" + message + "\"";
    }
}
