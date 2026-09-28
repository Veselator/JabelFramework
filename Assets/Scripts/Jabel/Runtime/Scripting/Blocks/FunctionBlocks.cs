using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jabel.Scripting.Blocks
{
    [Serializable]
    public class FunctionArgument
    {
        public string name = "amount";
        public JabelFormula value = new JabelFormula("1");
        [Tooltip("Multiply by the batch size during offline/lag catch-up (use for produced amounts).")]
        public bool scaleWithBatch;

        public FunctionArgument() { }

        public FunctionArgument(string name, string formula, bool scaleWithBatch = false)
        {
            this.name = name;
            value = new JabelFormula(formula);
            this.scaleWithBatch = scaleWithBatch;
        }
    }

    [Serializable]
    [JabelBlock("Functions/Call function", "#B5732E",
        Help = "Calls a named function. C# systems register handlers for it, scene objects can react with a " +
               "FunctionListener, and everything else can subscribe to FunctionCalledEvent on the event bus.")]
    public class CallFunctionBlock : JabelBlock
    {
        public string function = "MyFunction";
        public List<FunctionArgument> arguments = new List<FunctionArgument>();
        [Tooltip("Optional: local variable that receives the handler's return value.")]
        public string resultLocal;

        public CallFunctionBlock() { }

        public CallFunctionBlock(string function, params FunctionArgument[] args)
        {
            this.function = function;
            arguments = new List<FunctionArgument>(args);
        }

        public override BlockResult Execute(JabelContext context)
        {
            var args = FunctionArgs.Rent();
            try
            {
                foreach (var arg in arguments)
                {
                    var value = arg.value.Evaluate(context);
                    if (arg.scaleWithBatch && context.Batch > 1) value *= context.Batch;
                    args.Set(arg.name, value);
                }

                context.Runtime.Functions.Call(function, context, args);

                if (!string.IsNullOrEmpty(resultLocal) && args.HasResult) context.SetLocal(resultLocal, args.Result);
            }
            finally
            {
                args.Release();
            }
            return BlockResult.Continue;
        }

        public override string Describe()
        {
            var parts = new List<string>();
            foreach (var a in arguments) parts.Add($"{a.name}: {a.value}");
            return $"{function}({string.Join(", ", parts)})";
        }
    }

    [Serializable]
    [JabelBlock("Functions/Run script asset", "#B5732E",
        Help = "Runs a reusable JabelScriptAsset (a subroutine). Arguments become its local variables.")]
    public class RunScriptAssetBlock : JabelBlock
    {
        public JabelScriptAsset script;
        public List<FunctionArgument> arguments = new List<FunctionArgument>();

        public override BlockResult Execute(JabelContext context)
        {
            if (script == null) return BlockResult.Continue;
            var child = context.CreateChild();
            try
            {
                foreach (var arg in arguments)
                {
                    var value = arg.value.Evaluate(context);
                    if (arg.scaleWithBatch && context.Batch > 1) value *= context.Batch;
                    child.SetLocal(arg.name, value);
                }
                script.Script.Run(child);
            }
            finally
            {
                child.Release();
            }
            return BlockResult.Continue;
        }

        public override string Describe() => "run " + (script != null ? script.name : "<none>");
    }
}
