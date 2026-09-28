using System;
using Jabel.Numbers;
using Jabel.Variables;
using UnityEngine;

namespace Jabel.Scripting.Blocks
{
    public enum VariableScope
    {
        /// <summary>Game-wide variable declared in ClickerConfig (saved).</summary>
        Global,
        /// <summary>Lives only during this script run.</summary>
        Local,
        /// <summary>Belongs to the active buff instance running the script (saved with the instance).</summary>
        Instance
    }

    /// <summary>Where a block reads/writes a value.</summary>
    [Serializable]
    public struct VariableTarget
    {
        public VariableScope scope;
        public string name;

        public VariableTarget(VariableScope scope, string name)
        {
            this.scope = scope;
            this.name = name;
        }

        public BigNumber Get(JabelContext context)
        {
            switch (scope)
            {
                case VariableScope.Local:
                    return context.GetLocal(name);
                case VariableScope.Instance:
                    return context.Instance != null ? context.Instance.GetVariable(name) : BigNumber.Zero;
                default:
                    return context.Runtime.Variables.Get(name);
            }
        }

        public void Set(JabelContext context, BigNumber value)
        {
            switch (scope)
            {
                case VariableScope.Local:
                    context.SetLocal(name, value);
                    break;
                case VariableScope.Instance:
                    if (context.Instance != null) context.Instance.SetVariable(name, value);
                    else Debug.LogWarning($"[Jabel] Instance variable '{name}' used outside of an instance script.");
                    break;
                default:
                    context.Runtime.Variables.Set(name, value);
                    break;
            }
        }

        public override string ToString()
        {
            switch (scope)
            {
                case VariableScope.Local: return "local " + name;
                case VariableScope.Instance: return "this." + name;
                default: return name;
            }
        }
    }

    [Serializable]
    [JabelBlock("Variables/Set", "#2E7D5B", Help = "Assigns the result of a formula to a variable.")]
    public class SetVariableBlock : JabelBlock
    {
        public VariableTarget target = new VariableTarget(VariableScope.Global, "money");
        public JabelFormula value = new JabelFormula("0");

        public SetVariableBlock() { }

        public SetVariableBlock(VariableScope scope, string name, string formula)
        {
            target = new VariableTarget(scope, name);
            value = new JabelFormula(formula);
        }

        public override BlockResult Execute(JabelContext context)
        {
            target.Set(context, value.Evaluate(context));
            return BlockResult.Continue;
        }

        public override string Describe() => $"{target} = {value}";
    }

    public enum ModifyOperation { Add, Subtract, Multiply, Divide, Power, Min, Max }

    [Serializable]
    [JabelBlock("Variables/Modify", "#2E7D5B",
        Help = "Changes a variable by an amount. Add/Subtract are multiplied by the batch size during offline catch-up.")]
    public class ModifyVariableBlock : JabelBlock
    {
        public VariableTarget target = new VariableTarget(VariableScope.Global, "money");
        public ModifyOperation operation = ModifyOperation.Add;
        public JabelFormula amount = new JabelFormula("1");
        [Tooltip("Add/Subtract: amount * batch. Multiply/Divide: amount ^ batch. Keep enabled for anything that " +
                 "represents production, so offline progress stays correct.")]
        public bool scaleWithBatch = true;

        public ModifyVariableBlock() { }

        public ModifyVariableBlock(VariableScope scope, string name, ModifyOperation op, string formula)
        {
            target = new VariableTarget(scope, name);
            operation = op;
            amount = new JabelFormula(formula);
        }

        public override BlockResult Execute(JabelContext context)
        {
            var current = target.Get(context);
            var value = amount.Evaluate(context);
            long batch = scaleWithBatch ? Math.Max(1, context.Batch) : 1;

            BigNumber result;
            switch (operation)
            {
                case ModifyOperation.Add: result = current + value * batch; break;
                case ModifyOperation.Subtract: result = current - value * batch; break;
                case ModifyOperation.Multiply: result = current * (batch == 1 ? value : BigNumber.Pow(value, batch)); break;
                case ModifyOperation.Divide: result = current / (batch == 1 ? value : BigNumber.Pow(value, batch)); break;
                case ModifyOperation.Power: result = BigNumber.Pow(current, value); break;
                case ModifyOperation.Min: result = BigNumber.Min(current, value); break;
                case ModifyOperation.Max: result = BigNumber.Max(current, value); break;
                default: result = current; break;
            }
            target.Set(context, result);
            return BlockResult.Continue;
        }

        public override string Describe()
        {
            string op;
            switch (operation)
            {
                case ModifyOperation.Add: op = "+="; break;
                case ModifyOperation.Subtract: op = "-="; break;
                case ModifyOperation.Multiply: op = "*="; break;
                case ModifyOperation.Divide: op = "/="; break;
                case ModifyOperation.Power: op = "^="; break;
                case ModifyOperation.Min: op = "min="; break;
                default: op = "max="; break;
            }
            return $"{target} {op} {amount}";
        }
    }

    [Serializable]
    [JabelBlock("Variables/Declare", "#2E7D5B",
        Help = "Creates a variable if it does not exist yet. Global variables declared here are saved like config ones.")]
    public class DeclareVariableBlock : JabelBlock
    {
        public VariableScope scope = VariableScope.Local;
        public string name = "temp";
        public JabelFormula initialValue = new JabelFormula("0");
        [Tooltip("Global scope only.")]
        public VariablePersistence persistence = VariablePersistence.Run;

        public override BlockResult Execute(JabelContext context)
        {
            switch (scope)
            {
                case VariableScope.Local:
                    if (!context.HasLocal(name)) context.SetLocal(name, initialValue.Evaluate(context));
                    break;
                case VariableScope.Instance:
                    if (context.Instance != null && !context.Instance.HasVariable(name))
                        context.Instance.SetVariable(name, initialValue.Evaluate(context));
                    break;
                default:
                    if (!context.Runtime.Variables.Exists(name))
                        context.Runtime.Variables.DeclareRuntime(name, initialValue.Evaluate(context), persistence);
                    break;
            }
            return BlockResult.Continue;
        }

        public override string Describe() => $"declare {scope.ToString().ToLowerInvariant()} {name} = {initialValue}";
    }
}
