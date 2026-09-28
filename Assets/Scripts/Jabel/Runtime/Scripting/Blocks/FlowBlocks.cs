using System;
using UnityEngine;

namespace Jabel.Scripting.Blocks
{
    [Serializable]
    [JabelBlock("Flow/If", "#8E5BB5", Help = "Runs 'then' when the condition is non-zero, otherwise 'else'.")]
    public class IfBlock : JabelBlock
    {
        public JabelFormula condition = new JabelFormula("money >= 10");
        public JabelScript then = new JabelScript();
        public JabelScript otherwise = new JabelScript();

        public IfBlock() { }

        public IfBlock(string condition, JabelScript then, JabelScript otherwise = null)
        {
            this.condition = new JabelFormula(condition);
            this.then = then ?? new JabelScript();
            this.otherwise = otherwise ?? new JabelScript();
        }

        public override BlockResult Execute(JabelContext context) =>
            condition.Check(context) ? then.Run(context) : otherwise.Run(context);

        public override string Describe() => $"if {condition}";
    }

    [Serializable]
    [JabelBlock("Flow/Chance", "#8E5BB5", Help = "Random branch: 'then' runs with the given probability (0..1). Perfect for critical clicks.")]
    public class ChanceBlock : JabelBlock
    {
        public JabelFormula probability = new JabelFormula("0.1");
        public JabelScript then = new JabelScript();
        public JabelScript otherwise = new JabelScript();

        public ChanceBlock() { }

        public ChanceBlock(string probability, JabelScript then, JabelScript otherwise = null)
        {
            this.probability = new JabelFormula(probability);
            this.then = then ?? new JabelScript();
            this.otherwise = otherwise ?? new JabelScript();
        }

        public override BlockResult Execute(JabelContext context)
        {
            double p = probability.EvaluateDouble(context);
            return UnityEngine.Random.value < p ? then.Run(context) : otherwise.Run(context);
        }

        public override string Describe() => $"with chance {probability}";
    }

    [Serializable]
    [JabelBlock("Flow/Repeat", "#8E5BB5", Help = "Runs the body N times. The loop index is stored in a local variable.")]
    public class RepeatBlock : JabelBlock
    {
        public const int Limit = 10000;

        public JabelFormula times = new JabelFormula("3");
        public string indexLocal = "i";
        public JabelScript body = new JabelScript();

        public override BlockResult Execute(JabelContext context)
        {
            long count = Math.Min(Limit, times.Evaluate(context).ToLong());
            for (long i = 0; i < count; i++)
            {
                context.SetLocal(indexLocal, i);
                var result = body.Run(context);
                if (result == BlockResult.Break) break;
                if (result == BlockResult.Return) return result;
            }
            return BlockResult.Continue;
        }

        public override string Describe() => $"repeat {times} times";
    }

    [Serializable]
    [JabelBlock("Flow/While", "#8E5BB5", Help = "Runs the body while the condition holds (max 10000 iterations).")]
    public class WhileBlock : JabelBlock
    {
        public JabelFormula condition = new JabelFormula("false");
        public JabelScript body = new JabelScript();

        public WhileBlock() { }

        public WhileBlock(string condition, JabelScript body)
        {
            this.condition = new JabelFormula(condition);
            this.body = body;
        }

        public override BlockResult Execute(JabelContext context)
        {
            int guard = 0;
            while (condition.Check(context, false))
            {
                if (++guard > RepeatBlock.Limit)
                {
                    Debug.LogWarning($"[Jabel] While loop '{condition}' hit the iteration limit.");
                    break;
                }
                var result = body.Run(context);
                if (result == BlockResult.Break) break;
                if (result == BlockResult.Return) return result;
            }
            return BlockResult.Continue;
        }

        public override string Describe() => $"while {condition}";
    }

    [Serializable]
    [JabelBlock("Flow/Break loop", "#8E5BB5")]
    public class BreakBlock : JabelBlock
    {
        public override BlockResult Execute(JabelContext context) => BlockResult.Break;
        public override string Describe() => "break";
    }

    [Serializable]
    [JabelBlock("Flow/Stop script", "#8E5BB5")]
    public class StopBlock : JabelBlock
    {
        public override BlockResult Execute(JabelContext context) => BlockResult.Return;
        public override string Describe() => "stop";
    }

    [Serializable]
    [JabelBlock("Flow/Comment", "#3A3F44", Help = "Does nothing. Leave notes for your team.")]
    public class CommentBlock : JabelBlock
    {
        [TextArea(1, 5)] public string text = "Note";
        public override BlockResult Execute(JabelContext context) => BlockResult.Continue;
        public override string Describe() => "// " + text;
    }
}
