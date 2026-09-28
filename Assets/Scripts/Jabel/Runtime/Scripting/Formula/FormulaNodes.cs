using System.Collections.Generic;
using Jabel.Numbers;
using UnityEngine;

namespace Jabel.Scripting
{
    /// <summary>Anything a formula can read values from: locals, instance data, globals, derived values.</summary>
    public interface IFormulaContext
    {
        IJabelRuntime Runtime { get; }
        bool TryResolve(string name, out BigNumber value);
    }

    /// <summary>Node of a compiled formula syntax tree.</summary>
    public abstract class FormulaNode
    {
        public abstract BigNumber Evaluate(IFormulaContext context);

        /// <summary>True when the node's value can change without any variable changing (random, time...).</summary>
        public virtual bool IsVolatile => false;

        /// <summary>True when the node never reads context (enables constant folding).</summary>
        public virtual bool IsConstant => false;

        public virtual void CollectIdentifiers(HashSet<string> into) { }
    }

    internal sealed class ConstantNode : FormulaNode
    {
        public readonly BigNumber Value;
        public ConstantNode(BigNumber value) { Value = value; }
        public override BigNumber Evaluate(IFormulaContext context) => Value;
        public override bool IsConstant => true;
    }

    /// <summary>String literal. Only meaningful as a function argument (e.g. count('monkey')).</summary>
    internal sealed class StringNode : FormulaNode
    {
        public readonly string Value;
        public StringNode(string value) { Value = value; }
        public override BigNumber Evaluate(IFormulaContext context) => BigNumber.Zero;
        public override bool IsConstant => true;
    }

    internal sealed class IdentifierNode : FormulaNode
    {
        public readonly string Name;
        private static readonly HashSet<string> WarnedMissing = new HashSet<string>();

        public IdentifierNode(string name) { Name = name; }

        public override BigNumber Evaluate(IFormulaContext context)
        {
            if (context != null && context.TryResolve(Name, out var value)) return value;
            if (WarnedMissing.Add(Name))
                Debug.LogWarning($"[Jabel] Formula identifier '{Name}' is not defined (variable, derived value or local). Using 0.");
            return BigNumber.Zero;
        }

        public override void CollectIdentifiers(HashSet<string> into) => into.Add(Name);
    }

    internal enum UnaryOp { Negate, Not }

    internal sealed class UnaryNode : FormulaNode
    {
        private readonly UnaryOp _op;
        private readonly FormulaNode _operand;

        public UnaryNode(UnaryOp op, FormulaNode operand) { _op = op; _operand = operand; }

        public override BigNumber Evaluate(IFormulaContext context)
        {
            var v = _operand.Evaluate(context);
            return _op == UnaryOp.Negate ? -v : (v.IsZero ? BigNumber.One : BigNumber.Zero);
        }

        public override bool IsVolatile => _operand.IsVolatile;
        public override bool IsConstant => _operand.IsConstant;
        public override void CollectIdentifiers(HashSet<string> into) => _operand.CollectIdentifiers(into);
    }

    internal enum BinaryOp { Add, Sub, Mul, Div, Mod, Pow, Less, Greater, LessEq, GreaterEq, Eq, NotEq, And, Or }

    internal sealed class BinaryNode : FormulaNode
    {
        private readonly BinaryOp _op;
        private readonly FormulaNode _left, _right;

        public BinaryNode(BinaryOp op, FormulaNode left, FormulaNode right) { _op = op; _left = left; _right = right; }

        private static BigNumber B(bool v) => v ? BigNumber.One : BigNumber.Zero;

        public override BigNumber Evaluate(IFormulaContext context)
        {
            // Logical operators short-circuit.
            if (_op == BinaryOp.And) return B(_left.Evaluate(context).ToBool() && _right.Evaluate(context).ToBool());
            if (_op == BinaryOp.Or) return B(_left.Evaluate(context).ToBool() || _right.Evaluate(context).ToBool());

            var a = _left.Evaluate(context);
            var b = _right.Evaluate(context);
            switch (_op)
            {
                case BinaryOp.Add: return a + b;
                case BinaryOp.Sub: return a - b;
                case BinaryOp.Mul: return a * b;
                case BinaryOp.Div: return a / b;
                case BinaryOp.Mod: return a % b;
                case BinaryOp.Pow: return BigNumber.Pow(a, b);
                case BinaryOp.Less: return B(a < b);
                case BinaryOp.Greater: return B(a > b);
                case BinaryOp.LessEq: return B(a <= b);
                case BinaryOp.GreaterEq: return B(a >= b);
                case BinaryOp.Eq: return B(a.ApproximatelyEquals(b));
                case BinaryOp.NotEq: return B(!a.ApproximatelyEquals(b));
            }
            return BigNumber.Zero;
        }

        public override bool IsVolatile => _left.IsVolatile || _right.IsVolatile;
        public override bool IsConstant => _left.IsConstant && _right.IsConstant;

        public override void CollectIdentifiers(HashSet<string> into)
        {
            _left.CollectIdentifiers(into);
            _right.CollectIdentifiers(into);
        }
    }

    internal sealed class TernaryNode : FormulaNode
    {
        private readonly FormulaNode _condition, _then, _else;

        public TernaryNode(FormulaNode condition, FormulaNode then, FormulaNode otherwise)
        {
            _condition = condition; _then = then; _else = otherwise;
        }

        public override BigNumber Evaluate(IFormulaContext context) =>
            _condition.Evaluate(context).ToBool() ? _then.Evaluate(context) : _else.Evaluate(context);

        public override bool IsVolatile => _condition.IsVolatile || _then.IsVolatile || _else.IsVolatile;
        public override bool IsConstant => _condition.IsConstant && _then.IsConstant && _else.IsConstant;

        public override void CollectIdentifiers(HashSet<string> into)
        {
            _condition.CollectIdentifiers(into);
            _then.CollectIdentifiers(into);
            _else.CollectIdentifiers(into);
        }
    }

    internal sealed class CallNode : FormulaNode
    {
        public readonly string Name;
        private readonly FormulaNode[] _args;
        private FormulaFunctions.Entry _entry;
        private int _registryVersion = -1;

        public CallNode(string name, FormulaNode[] args) { Name = name; _args = args; }

        private FormulaFunctions.Entry Resolve()
        {
            if (_registryVersion != FormulaFunctions.Version)
            {
                _entry = FormulaFunctions.Find(Name);
                _registryVersion = FormulaFunctions.Version;
            }
            return _entry;
        }

        public override BigNumber Evaluate(IFormulaContext context)
        {
            var entry = Resolve();
            if (entry == null)
            {
                Debug.LogWarning($"[Jabel] Unknown formula function '{Name}'.");
                return BigNumber.Zero;
            }
            return entry.Function(new FormulaCall(context, _args));
        }

        public override bool IsVolatile
        {
            get
            {
                var entry = Resolve();
                if (entry == null || entry.Volatile) return true;
                foreach (var a in _args) if (a.IsVolatile) return true;
                return false;
            }
        }

        // Functions may read runtime state (count('x')), so they are never folded unless pure.
        public override bool IsConstant
        {
            get
            {
                var entry = Resolve();
                if (entry == null || !entry.Pure) return false;
                foreach (var a in _args) if (!a.IsConstant) return false;
                return true;
            }
        }

        public override void CollectIdentifiers(HashSet<string> into)
        {
            foreach (var a in _args) a.CollectIdentifiers(into);
        }
    }

    /// <summary>Arguments passed to a formula function. Arguments are evaluated lazily.</summary>
    public readonly struct FormulaCall
    {
        public readonly IFormulaContext Context;
        private readonly FormulaNode[] _args;

        internal FormulaCall(IFormulaContext context, FormulaNode[] args)
        {
            Context = context;
            _args = args;
        }

        public int Count => _args.Length;

        public BigNumber Arg(int index, BigNumber fallback = default)
        {
            if (index < 0 || index >= _args.Length) return fallback;
            return _args[index].Evaluate(Context);
        }

        public double ArgDouble(int index, double fallback = 0) =>
            index < _args.Length ? _args[index].Evaluate(Context).ToDoubleClamped() : fallback;

        /// <summary>Reads a string literal ('id') or a bare identifier name as text.</summary>
        public string StringArg(int index)
        {
            if (index < 0 || index >= _args.Length) return null;
            switch (_args[index])
            {
                case StringNode s: return s.Value;
                case IdentifierNode id: return id.Name;
                default: return _args[index].Evaluate(Context).ToInvariantString();
            }
        }
    }
}
