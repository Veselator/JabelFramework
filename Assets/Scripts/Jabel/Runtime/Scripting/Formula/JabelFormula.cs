using System;
using Jabel.Numbers;
using UnityEngine;

namespace Jabel.Scripting
{
    /// <summary>
    /// A designer-editable expression, e.g. "10 * 1.15 ^ count('monkey')" or "playerLevel >= 5".
    /// Compiled once and cached; evaluation allocates nothing.
    /// Formulas are the "value blocks" of JabelScript: every numeric field that designers may
    /// want to make dynamic is a formula, so there is no separate expression-block zoo to click through.
    /// </summary>
    [Serializable]
    public class JabelFormula
    {
        [SerializeField] private string expression;

        [NonSerialized] private CompiledFormula _compiled;

        public JabelFormula() { expression = string.Empty; }
        public JabelFormula(string expression) { this.expression = expression; }

        public static implicit operator JabelFormula(string expression) => new JabelFormula(expression);

        public string Expression
        {
            get => expression;
            set { expression = value; _compiled = null; }
        }

        public bool IsEmpty => string.IsNullOrWhiteSpace(expression);

        public CompiledFormula Compiled
        {
            get
            {
                // Re-compile when the source was edited in the inspector at runtime.
                if (_compiled == null || _compiled.Source != (expression ?? string.Empty))
                {
                    _compiled = FormulaCompiler.Compile(expression);
                    if (!_compiled.IsValid)
                        Debug.LogError($"[Jabel] Formula error in \"{expression}\": {_compiled.Error}");
                }
                return _compiled;
            }
        }

        public bool IsValid => Compiled.IsValid;

        /// <summary>Evaluates the formula. Empty formulas return <paramref name="whenEmpty"/>.</summary>
        public BigNumber Evaluate(IFormulaContext context, BigNumber whenEmpty = default)
        {
            if (IsEmpty) return whenEmpty;
            return Compiled.Evaluate(context);
        }

        /// <summary>Evaluates as a condition. Empty conditions are true by default.</summary>
        public bool Check(IFormulaContext context, bool whenEmpty = true)
        {
            if (IsEmpty) return whenEmpty;
            return Compiled.Evaluate(context).ToBool();
        }

        public double EvaluateDouble(IFormulaContext context, double whenEmpty = 0) =>
            IsEmpty ? whenEmpty : Compiled.Evaluate(context).ToDoubleClamped();

        public override string ToString() => expression;
    }
}
