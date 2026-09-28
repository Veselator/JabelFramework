using System;
using System.Collections.Generic;
using System.Globalization;
using Jabel.Numbers;

namespace Jabel.Scripting
{
    /// <summary>Result of compiling a formula string. Immutable and shared between identical sources.</summary>
    public sealed class CompiledFormula
    {
        public readonly string Source;
        public readonly FormulaNode Root;
        public readonly string Error;
        public readonly bool IsConstant;
        public readonly BigNumber ConstantValue;
        public readonly HashSet<string> Identifiers = new HashSet<string>();

        public bool IsValid => Error == null;
        public bool IsVolatile => Root != null && Root.IsVolatile;

        internal CompiledFormula(string source, FormulaNode root, string error)
        {
            Source = source;
            Root = root;
            Error = error;
            if (root != null)
            {
                root.CollectIdentifiers(Identifiers);
                if (root.IsConstant)
                {
                    IsConstant = true;
                    ConstantValue = root.Evaluate(null);
                }
            }
        }

        public BigNumber Evaluate(IFormulaContext context)
        {
            if (Root == null) return BigNumber.Zero;
            return IsConstant ? ConstantValue : Root.Evaluate(context);
        }
    }

    /// <summary>
    /// Recursive-descent compiler for Jabel formulas.
    /// Grammar (lowest to highest precedence):
    ///   ternary  : or ('?' ternary ':' ternary)?
    ///   or       : and (('||' | 'or') and)*
    ///   and      : equality (('&&' | 'and') equality)*
    ///   equality : compare (('==' | '!=') compare)*
    ///   compare  : additive (('&lt;' | '&gt;' | '&lt;=' | '&gt;=') additive)*
    ///   additive : term (('+' | '-') term)*
    ///   term     : unary (('*' | '/' | '%') unary)*
    ///   unary    : ('-' | '!' | 'not' | '+') unary | power
    ///   power    : primary ('^' unary)?          (right associative)
    ///   primary  : number[suffix] | 'string' | identifier | identifier '(' args ')' | '(' ternary ')'
    /// Numbers accept K/M/B/T suffixes and exponents: 1.5e30, 2K.
    /// </summary>
    public static class FormulaCompiler
    {
        private static readonly Dictionary<string, CompiledFormula> Cache = new Dictionary<string, CompiledFormula>();

        public static CompiledFormula Compile(string source)
        {
            source ??= string.Empty;
            if (Cache.TryGetValue(source, out var cached)) return cached;

            CompiledFormula result;
            if (string.IsNullOrWhiteSpace(source))
            {
                result = new CompiledFormula(source, new ConstantNode(BigNumber.Zero), null);
            }
            else
            {
                try
                {
                    var parser = new Parser(source);
                    var root = parser.ParseAll();
                    result = new CompiledFormula(source, root, null);
                }
                catch (FormulaException ex)
                {
                    result = new CompiledFormula(source, null, ex.Message);
                }
            }

            Cache[source] = result;
            return result;
        }

        /// <summary>Drops compiled formulas (e.g. after registering new functions in the editor).</summary>
        public static void ClearCache() => Cache.Clear();

        private sealed class FormulaException : Exception
        {
            public FormulaException(string message, int position) : base($"{message} (at {position + 1})") { }
        }

        private enum TokenType { Number, Identifier, String, Operator, LParen, RParen, Comma, Question, Colon, End }

        private struct Token
        {
            public TokenType Type;
            public string Text;
            public BigNumber Number;
            public int Position;
        }

        private sealed class Parser
        {
            private readonly List<Token> _tokens;
            private int _index;

            public Parser(string source)
            {
                _tokens = Tokenize(source);
            }

            private Token Current => _tokens[_index];

            private bool IsOperator(string op) => Current.Type == TokenType.Operator && Current.Text == op;

            private bool IsKeyword(string word) =>
                Current.Type == TokenType.Identifier && string.Equals(Current.Text, word, StringComparison.OrdinalIgnoreCase);

            private Token Advance() => _tokens[_index++];

            private void Expect(TokenType type, string what)
            {
                if (Current.Type != type) throw new FormulaException($"Expected {what}", Current.Position);
                _index++;
            }

            public FormulaNode ParseAll()
            {
                var node = ParseTernary();
                if (Current.Type != TokenType.End)
                    throw new FormulaException($"Unexpected '{Current.Text}'", Current.Position);
                return node;
            }

            private FormulaNode ParseTernary()
            {
                var condition = ParseOr();
                if (Current.Type != TokenType.Question) return condition;
                Advance();
                var then = ParseTernary();
                Expect(TokenType.Colon, "':'");
                var otherwise = ParseTernary();
                return new TernaryNode(condition, then, otherwise);
            }

            private FormulaNode ParseOr()
            {
                var left = ParseAnd();
                while (IsOperator("||") || IsKeyword("or"))
                {
                    Advance();
                    left = new BinaryNode(BinaryOp.Or, left, ParseAnd());
                }
                return left;
            }

            private FormulaNode ParseAnd()
            {
                var left = ParseEquality();
                while (IsOperator("&&") || IsKeyword("and"))
                {
                    Advance();
                    left = new BinaryNode(BinaryOp.And, left, ParseEquality());
                }
                return left;
            }

            private FormulaNode ParseEquality()
            {
                var left = ParseCompare();
                while (IsOperator("==") || IsOperator("!="))
                {
                    var op = Advance().Text == "==" ? BinaryOp.Eq : BinaryOp.NotEq;
                    left = new BinaryNode(op, left, ParseCompare());
                }
                return left;
            }

            private FormulaNode ParseCompare()
            {
                var left = ParseAdditive();
                while (IsOperator("<") || IsOperator(">") || IsOperator("<=") || IsOperator(">="))
                {
                    BinaryOp op;
                    switch (Advance().Text)
                    {
                        case "<": op = BinaryOp.Less; break;
                        case ">": op = BinaryOp.Greater; break;
                        case "<=": op = BinaryOp.LessEq; break;
                        default: op = BinaryOp.GreaterEq; break;
                    }
                    left = new BinaryNode(op, left, ParseAdditive());
                }
                return left;
            }

            private FormulaNode ParseAdditive()
            {
                var left = ParseTerm();
                while (IsOperator("+") || IsOperator("-"))
                {
                    var op = Advance().Text == "+" ? BinaryOp.Add : BinaryOp.Sub;
                    left = new BinaryNode(op, left, ParseTerm());
                }
                return left;
            }

            private FormulaNode ParseTerm()
            {
                var left = ParseUnary();
                while (IsOperator("*") || IsOperator("/") || IsOperator("%"))
                {
                    string text = Advance().Text;
                    var op = text == "*" ? BinaryOp.Mul : text == "/" ? BinaryOp.Div : BinaryOp.Mod;
                    left = new BinaryNode(op, left, ParseUnary());
                }
                return left;
            }

            private FormulaNode ParseUnary()
            {
                if (IsOperator("-"))
                {
                    Advance();
                    return new UnaryNode(UnaryOp.Negate, ParseUnary());
                }
                if (IsOperator("+"))
                {
                    Advance();
                    return ParseUnary();
                }
                if (IsOperator("!") || IsKeyword("not"))
                {
                    Advance();
                    return new UnaryNode(UnaryOp.Not, ParseUnary());
                }
                return ParsePower();
            }

            private FormulaNode ParsePower()
            {
                var left = ParsePrimary();
                if (IsOperator("^"))
                {
                    Advance();
                    // Right associative: 2^3^2 = 2^(3^2). Unary allowed in exponent: 2^-1.
                    return new BinaryNode(BinaryOp.Pow, left, ParseUnary());
                }
                return left;
            }

            private FormulaNode ParsePrimary()
            {
                var token = Current;
                switch (token.Type)
                {
                    case TokenType.Number:
                        Advance();
                        return new ConstantNode(token.Number);
                    case TokenType.String:
                        Advance();
                        return new StringNode(token.Text);
                    case TokenType.LParen:
                    {
                        Advance();
                        var inner = ParseTernary();
                        Expect(TokenType.RParen, "')'");
                        return inner;
                    }
                    case TokenType.Identifier:
                    {
                        Advance();
                        if (Current.Type == TokenType.LParen)
                        {
                            Advance();
                            var args = new List<FormulaNode>();
                            if (Current.Type != TokenType.RParen)
                            {
                                args.Add(ParseTernary());
                                while (Current.Type == TokenType.Comma)
                                {
                                    Advance();
                                    args.Add(ParseTernary());
                                }
                            }
                            Expect(TokenType.RParen, "')'");
                            return new CallNode(token.Text, args.ToArray());
                        }

                        switch (token.Text.ToLowerInvariant())
                        {
                            case "true": return new ConstantNode(BigNumber.One);
                            case "false": return new ConstantNode(BigNumber.Zero);
                            case "pi": return new ConstantNode(Math.PI);
                        }
                        return new IdentifierNode(token.Text);
                    }
                    case TokenType.End:
                        throw new FormulaException("Unexpected end of formula", token.Position);
                    default:
                        throw new FormulaException($"Unexpected '{token.Text}'", token.Position);
                }
            }

            // ------------------------------------------------------------ lexer

            private static List<Token> Tokenize(string s)
            {
                var tokens = new List<Token>();
                int i = 0;
                while (i < s.Length)
                {
                    char c = s[i];
                    if (char.IsWhiteSpace(c)) { i++; continue; }

                    int start = i;

                    if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
                    {
                        while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == '_')) i++;
                        // Exponent part: e10, e-5
                        if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
                        {
                            int save = i;
                            i++;
                            if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
                            if (i < s.Length && char.IsDigit(s[i]))
                                while (i < s.Length && char.IsDigit(s[i])) i++;
                            else i = save;
                        }
                        // Short-scale suffix: 5K, 2.5M (only when not followed by identifier characters).
                        if (i < s.Length && "KkMBT".IndexOf(s[i]) >= 0 && (i + 1 >= s.Length || !IsIdentChar(s[i + 1]))) i++;

                        string text = s.Substring(start, i - start);
                        if (!BigNumber.TryParse(text, out var number))
                            throw new FormulaException($"Invalid number '{text}'", start);
                        tokens.Add(new Token { Type = TokenType.Number, Text = text, Number = number, Position = start });
                        continue;
                    }

                    if (char.IsLetter(c) || c == '_' || c == '$' || c == '@')
                    {
                        i++;
                        while (i < s.Length && IsIdentChar(s[i])) i++;
                        tokens.Add(new Token { Type = TokenType.Identifier, Text = s.Substring(start, i - start), Position = start });
                        continue;
                    }

                    if (c == '\'' || c == '"')
                    {
                        i++;
                        while (i < s.Length && s[i] != c) i++;
                        if (i >= s.Length) throw new FormulaException("Unterminated string", start);
                        tokens.Add(new Token { Type = TokenType.String, Text = s.Substring(start + 1, i - start - 1), Position = start });
                        i++;
                        continue;
                    }

                    switch (c)
                    {
                        case '(': tokens.Add(new Token { Type = TokenType.LParen, Text = "(", Position = start }); i++; continue;
                        case ')': tokens.Add(new Token { Type = TokenType.RParen, Text = ")", Position = start }); i++; continue;
                        case ',': tokens.Add(new Token { Type = TokenType.Comma, Text = ",", Position = start }); i++; continue;
                        case '?': tokens.Add(new Token { Type = TokenType.Question, Text = "?", Position = start }); i++; continue;
                        case ':': tokens.Add(new Token { Type = TokenType.Colon, Text = ":", Position = start }); i++; continue;
                    }

                    string two = i + 1 < s.Length ? s.Substring(i, 2) : null;
                    if (two == "<=" || two == ">=" || two == "==" || two == "!=" || two == "&&" || two == "||")
                    {
                        tokens.Add(new Token { Type = TokenType.Operator, Text = two, Position = start });
                        i += 2;
                        continue;
                    }

                    if ("+-*/%^<>!".IndexOf(c) >= 0)
                    {
                        tokens.Add(new Token { Type = TokenType.Operator, Text = c.ToString(), Position = start });
                        i++;
                        continue;
                    }

                    if (c == '=')
                    {
                        // Designers often type a single '=' for comparison; accept it.
                        tokens.Add(new Token { Type = TokenType.Operator, Text = "==", Position = start });
                        i++;
                        continue;
                    }

                    throw new FormulaException($"Unexpected character '{c}'", start);
                }

                tokens.Add(new Token { Type = TokenType.End, Text = "<end>", Position = s.Length });
                return tokens;
            }

            private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '.';
        }
    }
}
