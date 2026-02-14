using System;
using System.Collections.Generic;
using System.Text;

namespace QDND.Combat.Rules.Conditions
{
    /// <summary>
    /// Token types produced by the condition tokenizer.
    /// </summary>
    public enum TokenType
    {
        /// <summary>An identifier: function name, enum value, keyword, etc.</summary>
        Identifier,
        /// <summary>An integer or floating-point literal.</summary>
        Number,
        /// <summary>A string literal enclosed in single quotes.</summary>
        StringLiteral,
        /// <summary>Opening parenthesis <c>(</c>.</summary>
        LeftParen,
        /// <summary>Closing parenthesis <c>)</c>.</summary>
        RightParen,
        /// <summary>Comma separator <c>,</c>.</summary>
        Comma,
        /// <summary>Dot accessor <c>.</c>.</summary>
        Dot,
        /// <summary>Boolean AND operator (<c>and</c> / <c>&amp;&amp;</c>).</summary>
        And,
        /// <summary>Boolean OR operator (<c>or</c> / <c>||</c>).</summary>
        Or,
        /// <summary>Boolean NOT operator (<c>not</c> / <c>!</c>).</summary>
        Not,
        /// <summary>Less-than operator <c>&lt;</c>.</summary>
        LessThan,
        /// <summary>Less-than-or-equal operator <c>&lt;=</c>.</summary>
        LessEqual,
        /// <summary>Greater-than operator <c>&gt;</c>.</summary>
        GreaterThan,
        /// <summary>Greater-than-or-equal operator <c>&gt;=</c>.</summary>
        GreaterEqual,
        /// <summary>Equality operator <c>==</c>.</summary>
        Equal,
        /// <summary>Inequality operator <c>!=</c>.</summary>
        NotEqual,
        /// <summary>End of input sentinel.</summary>
        EOF
    }

    /// <summary>
    /// A single token produced by <see cref="ConditionTokenizer"/>.
    /// </summary>
    public readonly struct Token
    {
        /// <summary>The kind of token.</summary>
        public readonly TokenType Type;

        /// <summary>The raw text of the token.</summary>
        public readonly string Value;

        /// <summary>Character offset where the token starts in the source string.</summary>
        public readonly int Position;

        /// <summary>Creates a new token.</summary>
        public Token(TokenType type, string value, int position)
        {
            Type = type;
            Value = value;
            Position = position;
        }

        /// <inheritdoc />
        public override string ToString() => $"{Type}({Value})@{Position}";
    }

    /// <summary>
    /// Tokenizes BG3 condition strings into a flat list of <see cref="Token"/>s
    /// that the <see cref="ConditionEvaluator"/> can parse.
    ///
    /// Handles:
    /// <list type="bullet">
    /// <item>Function calls and identifiers (IsMeleeAttack, context.Source, etc.)</item>
    /// <item>Parentheses, commas, dots</item>
    /// <item>Boolean keywords: <c>and</c>, <c>or</c>, <c>not</c></item>
    /// <item>Comparison operators: <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c>, <c>==</c>, <c>!=</c></item>
    /// <item>Numeric literals (integer and float)</item>
    /// <item>String literals in single quotes: <c>'RAGING'</c></item>
    /// </list>
    /// </summary>
    public static class ConditionTokenizer
    {
        /// <summary>
        /// Tokenizes the given condition string.
        /// </summary>
        /// <param name="condition">The BG3 condition expression string.</param>
        /// <returns>A list of tokens including a trailing <see cref="TokenType.EOF"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="condition"/> is null.</exception>
        public static List<Token> Tokenize(string condition)
        {
            if (condition == null)
                throw new ArgumentNullException(nameof(condition));

            var tokens = new List<Token>();
            int i = 0;
            int len = condition.Length;

            while (i < len)
            {
                char c = condition[i];

                // Skip whitespace
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                // Single-quoted string literal
                if (c == '\'')
                {
                    int start = i;
                    i++; // skip opening quote
                    var sb = new StringBuilder();
                    while (i < len && condition[i] != '\'')
                    {
                        sb.Append(condition[i]);
                        i++;
                    }
                    if (i < len) i++; // skip closing quote
                    tokens.Add(new Token(TokenType.StringLiteral, sb.ToString(), start));
                    continue;
                }

                // Numeric literal (integer or float, including negative sign if preceded by operator)
                if (char.IsDigit(c))
                {
                    int start = i;
                    while (i < len && (char.IsDigit(condition[i]) || condition[i] == '.'))
                        i++;
                    tokens.Add(new Token(TokenType.Number, condition.Substring(start, i - start), start));
                    continue;
                }

                // Identifiers and keywords
                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < len && (char.IsLetterOrDigit(condition[i]) || condition[i] == '_'))
                        i++;
                    string word = condition.Substring(start, i - start);

                    // Map keywords to their token types
                    switch (word.ToLowerInvariant())
                    {
                        case "and":
                            tokens.Add(new Token(TokenType.And, word, start));
                            break;
                        case "or":
                            tokens.Add(new Token(TokenType.Or, word, start));
                            break;
                        case "not":
                            tokens.Add(new Token(TokenType.Not, word, start));
                            break;
                        default:
                            tokens.Add(new Token(TokenType.Identifier, word, start));
                            break;
                    }
                    continue;
                }

                // Two-character operators
                if (i + 1 < len)
                {
                    string twoChar = condition.Substring(i, 2);
                    switch (twoChar)
                    {
                        case "<=":
                            tokens.Add(new Token(TokenType.LessEqual, twoChar, i));
                            i += 2;
                            continue;
                        case ">=":
                            tokens.Add(new Token(TokenType.GreaterEqual, twoChar, i));
                            i += 2;
                            continue;
                        case "==":
                            tokens.Add(new Token(TokenType.Equal, twoChar, i));
                            i += 2;
                            continue;
                        case "!=":
                            tokens.Add(new Token(TokenType.NotEqual, twoChar, i));
                            i += 2;
                            continue;
                        case "&&":
                            tokens.Add(new Token(TokenType.And, twoChar, i));
                            i += 2;
                            continue;
                        case "||":
                            tokens.Add(new Token(TokenType.Or, twoChar, i));
                            i += 2;
                            continue;
                    }
                }

                // Single-character tokens
                switch (c)
                {
                    case '(':
                        tokens.Add(new Token(TokenType.LeftParen, "(", i));
                        i++;
                        continue;
                    case ')':
                        tokens.Add(new Token(TokenType.RightParen, ")", i));
                        i++;
                        continue;
                    case ',':
                        tokens.Add(new Token(TokenType.Comma, ",", i));
                        i++;
                        continue;
                    case '.':
                        tokens.Add(new Token(TokenType.Dot, ".", i));
                        i++;
                        continue;
                    case '<':
                        tokens.Add(new Token(TokenType.LessThan, "<", i));
                        i++;
                        continue;
                    case '>':
                        tokens.Add(new Token(TokenType.GreaterThan, ">", i));
                        i++;
                        continue;
                    case '!':
                        tokens.Add(new Token(TokenType.Not, "!", i));
                        i++;
                        continue;
                    default:
                        // Skip unknown characters with a warning
                        Godot.GD.PushWarning($"[ConditionTokenizer] Unexpected character '{c}' at position {i} in: {condition}");
                        i++;
                        continue;
                }
            }

            tokens.Add(new Token(TokenType.EOF, "", len));
            return tokens;
        }
    }
}
