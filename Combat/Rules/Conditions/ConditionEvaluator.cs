using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Data.CharacterModel;

namespace QDND.Combat.Rules.Conditions
{
    /// <summary>
    /// Evaluates BG3 condition expression strings at runtime.
    ///
    /// The evaluator uses a recursive-descent parser over tokens produced by
    /// <see cref="ConditionTokenizer"/>.  It supports:
    /// <list type="bullet">
    /// <item>Boolean operators: <c>and</c>, <c>or</c>, <c>not</c></item>
    /// <item>Comparison operators: <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c>, <c>==</c>, <c>!=</c></item>
    /// <item>Parenthesised sub-expressions</item>
    /// <item>Built-in BG3 condition functions (see <see cref="EvaluateFunction"/>)</item>
    /// </list>
    ///
    /// Functions that are not yet implemented return <c>true</c> with a warning so
    /// boosts are not silently dropped.
    /// </summary>
    public class ConditionEvaluator
    {
        // ──────────────────────────────────────────────
        //  Singleton / caching
        // ──────────────────────────────────────────────

        /// <summary>Shared evaluator instance (thread-safety is not required in Godot).</summary>
        public static readonly ConditionEvaluator Instance = new ConditionEvaluator();

        /// <summary>
        /// Cache of already-warned unknown functions so we only warn once per function name
        /// to avoid flooding the log.
        /// </summary>
        private readonly HashSet<string> _warnedFunctions = new(StringComparer.OrdinalIgnoreCase);

        // ──────────────────────────────────────────────
        //  Parser state (per Evaluate call)
        // ──────────────────────────────────────────────

        private List<Token> _tokens;
        private int _pos;
        private ConditionContext _ctx;
        private string _rawCondition; // for error messages

        // ──────────────────────────────────────────────
        //  Public API
        // ──────────────────────────────────────────────

        /// <summary>
        /// Evaluates a BG3 condition string in the given context.
        /// </summary>
        /// <param name="condition">The condition expression (e.g. <c>"IsMeleeAttack() and not HasStatus('RAGING')"</c>).</param>
        /// <param name="context">The combat context providing source, target, attack metadata, etc.</param>
        /// <returns><c>true</c> if the condition is satisfied; <c>false</c> otherwise.
        /// Returns <c>true</c> for null/empty conditions (unconditional).</returns>
        public bool Evaluate(string condition, ConditionContext context)
        {
            if (string.IsNullOrWhiteSpace(condition))
                return true; // unconditional

            if (context == null)
                return false; // can't evaluate without context

            try
            {
                _rawCondition = condition;
                _tokens = ConditionTokenizer.Tokenize(condition);
                _pos = 0;
                _ctx = context;

                bool result = ParseExpression();

                // Ensure we consumed all meaningful tokens
                if (Current().Type != TokenType.EOF)
                {
                    GD.PushWarning($"[ConditionEvaluator] Unexpected token '{Current().Value}' at position {Current().Position} in: {condition}");
                }

                return result;
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[ConditionEvaluator] Error evaluating '{condition}': {ex.Message}");
                return true; // fail-open so boosts aren't silently dropped
            }
            finally
            {
                _tokens = null;
                _ctx = null;
                _rawCondition = null;
            }
        }

        // ──────────────────────────────────────────────
        //  Recursive-descent parser
        //  Grammar:
        //    expression  → or_expr
        //    or_expr     → and_expr ('or' and_expr)*
        //    and_expr    → not_expr ('and' not_expr)*
        //    not_expr    → 'not' not_expr | comparison
        //    comparison  → primary (('<' | '<=' | '>' | '>=' | '==' | '!=') primary)?
        //    primary     → '(' expression ')'
        //                | functionCall
        //                | qualifiedId        (e.g. Size.Medium, context.Source)
        //                | number
        //                | stringLiteral
        //    functionCall → identifier '(' argList? ')'
        //    argList     → expression (',' expression)*
        //    qualifiedId → identifier ('.' identifier)*
        // ──────────────────────────────────────────────

        private Token Current() => _pos < _tokens.Count ? _tokens[_pos] : _tokens[^1];

        private Token Advance()
        {
            var t = Current();
            if (_pos < _tokens.Count - 1) _pos++;
            return t;
        }

        private bool Match(TokenType type)
        {
            if (Current().Type == type)
            {
                Advance();
                return true;
            }
            return false;
        }

        private void Expect(TokenType type, string contextMsg = "")
        {
            if (!Match(type))
            {
                GD.PushWarning($"[ConditionEvaluator] Expected {type} but got {Current().Type}('{Current().Value}') {contextMsg} in: {_rawCondition}");
            }
        }

        // ── or_expr ──
        private bool ParseExpression() => ParseOr();

        private bool ParseOr()
        {
            bool left = ParseAnd();
            while (Current().Type == TokenType.Or)
            {
                Advance();
                bool right = ParseAnd();
                left = left || right;
            }
            return left;
        }

        // ── and_expr ──
        private bool ParseAnd()
        {
            bool left = ParseNot();
            while (Current().Type == TokenType.And)
            {
                Advance();
                bool right = ParseNot();
                left = left && right;
            }
            return left;
        }

        // ── not_expr ──
        private bool ParseNot()
        {
            if (Current().Type == TokenType.Not)
            {
                Advance();
                return !ParseNot();
            }
            return ParseComparison();
        }

        // ── comparison ──
        /// <summary>
        /// Parses a comparison: primary (op primary)?
        /// Returns a bool – comparisons resolve to true/false,
        /// plain primaries are returned via <see cref="ParsePrimaryValue"/> and then
        /// converted to bool (non-zero / non-null → true).
        /// </summary>
        private bool ParseComparison()
        {
            object left = ParsePrimaryValue();

            var op = Current().Type;
            if (op == TokenType.LessThan || op == TokenType.LessEqual ||
                op == TokenType.GreaterThan || op == TokenType.GreaterEqual ||
                op == TokenType.Equal || op == TokenType.NotEqual)
            {
                Advance();
                object right = ParsePrimaryValue();
                return CompareValues(left, right, op);
            }

            return ToBool(left);
        }

        // ── primary (returns boxed value) ──
        /// <summary>
        /// Parses a primary expression and returns its evaluated value.
        /// <list type="bullet">
        /// <item>Function call → bool (from <see cref="EvaluateFunction"/>)</item>
        /// <item>Parenthesised expression → bool</item>
        /// <item>Number → double</item>
        /// <item>String literal → string</item>
        /// <item>Qualified identifier → resolved enum/int value or string</item>
        /// </list>
        /// </summary>
        private object ParsePrimaryValue()
        {
            var tok = Current();

            // Parenthesised sub-expression
            if (tok.Type == TokenType.LeftParen)
            {
                Advance();
                bool inner = ParseExpression();
                Expect(TokenType.RightParen, "after parenthesised expression");
                return inner;
            }

            // Number literal
            if (tok.Type == TokenType.Number)
            {
                Advance();
                if (double.TryParse(tok.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
                    return num;
                return 0.0;
            }

            // String literal
            if (tok.Type == TokenType.StringLiteral)
            {
                Advance();
                return tok.Value;
            }

            // Identifier – may be function call or qualified name
            if (tok.Type == TokenType.Identifier)
            {
                // Build full qualified name (e.g. context.Source, Size.Medium, DamageFlags.Hit)
                string name = tok.Value;
                Advance();
                while (Current().Type == TokenType.Dot)
                {
                    Advance(); // skip dot
                    if (Current().Type == TokenType.Identifier)
                    {
                        name += "." + Current().Value;
                        Advance();
                    }
                }

                // Function call?
                if (Current().Type == TokenType.LeftParen)
                {
                    Advance(); // consume '('
                    var args = new List<object>();
                    if (Current().Type != TokenType.RightParen)
                    {
                        args.Add(ParseArgValue());
                        while (Current().Type == TokenType.Comma)
                        {
                            Advance(); // skip comma
                            args.Add(ParseArgValue());
                        }
                    }
                    Expect(TokenType.RightParen, $"after arguments of {name}()");
                    return EvaluateFunction(name, args);
                }

                // Bare identifier / qualified name → resolve to a known value
                return ResolveIdentifier(name);
            }

            // Fallback: skip and return true
            GD.PushWarning($"[ConditionEvaluator] Unexpected token '{tok.Value}' at position {tok.Position} in: {_rawCondition}");
            Advance();
            return true;
        }

        /// <summary>
        /// Parses a single function argument value (may be a full expression, string, or
        /// qualified identifier).
        /// </summary>
        private object ParseArgValue()
        {
            var tok = Current();

            // String literal argument
            if (tok.Type == TokenType.StringLiteral)
            {
                Advance();
                return tok.Value;
            }

            // Number argument
            if (tok.Type == TokenType.Number)
            {
                Advance();
                if (double.TryParse(tok.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
                    return num;
                return 0.0;
            }

            // Identifier (may be qualified: context.Source, DamageFlags.Hit, etc.)
            if (tok.Type == TokenType.Identifier)
            {
                string name = tok.Value;
                Advance();
                while (Current().Type == TokenType.Dot)
                {
                    Advance();
                    if (Current().Type == TokenType.Identifier)
                    {
                        name += "." + Current().Value;
                        Advance();
                    }
                }
                // Could be a nested function call
                if (Current().Type == TokenType.LeftParen)
                {
                    Advance();
                    var innerArgs = new List<object>();
                    if (Current().Type != TokenType.RightParen)
                    {
                        innerArgs.Add(ParseArgValue());
                        while (Current().Type == TokenType.Comma)
                        {
                            Advance();
                            innerArgs.Add(ParseArgValue());
                        }
                    }
                    Expect(TokenType.RightParen, $"after arguments of {name}()");
                    return EvaluateFunction(name, innerArgs);
                }
                return name; // bare identifier / qualified name
            }

            // Sub-expression in parens
            if (tok.Type == TokenType.LeftParen)
            {
                Advance();
                bool inner = ParseExpression();
                Expect(TokenType.RightParen, "after sub-expression arg");
                return inner;
            }

            // Fallback: return the token's text and advance
            Advance();
            return tok.Value;
        }

        // ──────────────────────────────────────────────
        //  Function evaluation
        // ──────────────────────────────────────────────

        /// <summary>
        /// Evaluates a BG3 condition function by name.
        /// Implements the most common ~15 functions; unrecognised functions return <c>true</c>
        /// with a warning (fail-open so boosts aren't silently lost).
        /// </summary>
        /// <param name="name">Function name (may be qualified, e.g. <c>"context.Source"</c>).</param>
        /// <param name="args">Evaluated argument values.</param>
        /// <returns>The function result, typically a <see cref="bool"/>.</returns>
        private object EvaluateFunction(string name, List<object> args)
        {
            // Normalise the function name for matching.
            // BG3 data uses varying casing: IsMeleeAttack, HasStatus, etc.
            string fn = name;

            // Strip "context.Source." or "context.Target." prefixes that some BG3 conditions use
            // to qualify which entity the function applies to.
            string targetQualifier = null;
            if (fn.StartsWith("context.Source.", StringComparison.OrdinalIgnoreCase))
            {
                targetQualifier = "Source";
                fn = fn.Substring("context.Source.".Length);
            }
            else if (fn.StartsWith("context.Target.", StringComparison.OrdinalIgnoreCase))
            {
                targetQualifier = "Target";
                fn = fn.Substring("context.Target.".Length);
            }

            // Choose the entity the function applies to
            Combatant subject = targetQualifier switch
            {
                "Target" => _ctx.Target,
                "Source" => _ctx.Source,
                _ => _ctx.Source   // unqualified defaults to source
            };

            switch (fn.ToLowerInvariant())
            {
                // ── Attack-type checks ──
                case "ismeleeattack":
                    return _ctx.IsMelee && (_ctx.IsWeaponAttack || _ctx.IsSpellAttack);

                case "israngedattack":
                    return _ctx.IsRanged;

                case "isweaponattack":
                    return _ctx.IsWeaponAttack;

                case "isspellattack":
                    return _ctx.IsSpellAttack;

                case "isspell":
                    return _ctx.IsSpell;

                case "ismeleeweaponattack":
                    return _ctx.IsMelee && _ctx.IsWeaponAttack;

                case "israngedweaponattack":
                    return _ctx.IsRanged && _ctx.IsWeaponAttack;

                case "ismeleespellattack":
                    return _ctx.IsMelee && _ctx.IsSpellAttack;

                case "israngedspellattack":
                    return _ctx.IsRanged && _ctx.IsSpellAttack;

                // ── Hit / critical checks ──
                case "iscriticalhit":
                case "iscritical":
                    return _ctx.IsCriticalHit;

                case "iscriticalmiss":
                    return _ctx.IsCriticalMiss;

                case "hasdamageeffectflag":
                {
                    // HasDamageEffectFlag(DamageFlags.Hit)
                    string flag = ArgString(args, 0);
                    if (flag.EndsWith(".Hit", StringComparison.OrdinalIgnoreCase) ||
                        flag.Equals("Hit", StringComparison.OrdinalIgnoreCase))
                        return _ctx.IsHit;
                    if (flag.EndsWith(".Miss", StringComparison.OrdinalIgnoreCase) ||
                        flag.Equals("Miss", StringComparison.OrdinalIgnoreCase))
                        return !_ctx.IsHit;
                    WarnUnknownArg(name, flag);
                    return true;
                }

                // ── Status checks ──
                case "hasstatus":
                {
                    // HasStatus('STATUS_ID') or HasStatus('STATUS_ID', context.Target)
                    string statusId = StripQuotes(ArgString(args, 0));
                    Combatant who = ResolveTargetArg(args, 1, subject);
                    return CheckHasStatus(who, statusId);
                }

                // ── Passive checks ──
                case "haspassive":
                {
                    // HasPassive('PassiveName', context.Source)
                    string passiveId = StripQuotes(ArgString(args, 0));
                    Combatant who = ResolveTargetArg(args, 1, subject);
                    return who?.PassiveManager?.HasPassive(passiveId) ?? false;
                }

                // ── Entity checks ──
                case "self":
                    // Self() — true when source == target (self-buff evaluation)
                    return _ctx.Source != null && _ctx.Target != null &&
                           ReferenceEquals(_ctx.Source, _ctx.Target);

                case "character":
                    // Character() — is the subject a player-type character (not a summon/object)?
                    // Approximate: has Faction Player or Hostile (i.e. a sentient combatant)
                    return subject != null && subject.Faction != Faction.Neutral;

                case "dead":
                    return subject?.LifeState == CombatantLifeState.Dead;

                case "isalive":
                    return subject?.LifeState == CombatantLifeState.Alive;

                // ── Distance checks ──
                case "distancetotargetgreaterthan":
                {
                    double threshold = ArgDouble(args, 0, 0);
                    double dist = GetDistance(_ctx.Source, _ctx.Target);
                    return dist > threshold;
                }

                case "distanceto":
                {
                    // DistanceTo(context.Target) — returns the numeric distance (used in comparisons)
                    return (object)GetDistance(_ctx.Source, _ctx.Target);
                }

                // ── Size check ──
                case "size":
                {
                    // Size(context.Target) → returns numeric size for comparison
                    // BG3 sizes: Tiny=0, Small=1, Medium=2, Large=3, Huge=4, Gargantuan=5
                    Combatant who = ResolveTargetArg(args, 0, subject);
                    return (object)GetCreatureSize(who);
                }

                // ── Weapon checks ──
                case "hasweaponproperty":
                {
                    // HasWeaponProperty(WeaponProperties.Heavy)
                    string propName = ArgString(args, 0);
                    return CheckWeaponProperty(propName);
                }

                case "isusingweapontype":
                {
                    string weaponType = ArgString(args, 0);
                    return _ctx.Weapon != null &&
                           _ctx.Weapon.WeaponType.ToString().Equals(weaponType, StringComparison.OrdinalIgnoreCase);
                }

                // ── Class level check ──
                // BG3 data uses several spellings; match them all.
                case "classlevelhigheroregualto":
                case "classlevelhighorequalto":
                case "classlevelhigheroreequalto":
                case "classlevelhigheroregualtoaliased":
                case "classlevelhigheroregualtos":
                case "classlevelhigheroregualto1":
                case "classlevelhighorequalto1":
                {
                    int level = (int)ArgDouble(args, 0, 1);
                    string className = ArgString(args, 1);
                    return CheckClassLevel(subject, level, className);
                }

                // ── Proficiency ──
                case "isproficientwith":
                case "isproficientwithweapon":
                {
                    // Stub: assume proficient for now
                    WarnStub(name);
                    return true;
                }

                // ── Spellcasting ability ──
                case "spellcastingabilityis":
                case "usingspellslot":
                case "usingactionresource":
                {
                    WarnStub(name);
                    return true;
                }

                // ── Condition helpers ──
                case "hasusedheavyarmor":
                case "isequippedwith":
                case "iswearingarmor":
                case "hasshieldequipped":
                {
                    if (fn.Equals("HasShieldEquipped", StringComparison.OrdinalIgnoreCase))
                        return subject?.HasShield ?? false;
                    WarnStub(name);
                    return true;
                }

                case "isconcentrating":
                {
                    // Stub — concentration tracking not yet wired
                    WarnStub(name);
                    return false;
                }

                case "tagged":
                case "hastag":
                {
                    string tag = StripQuotes(ArgString(args, 0));
                    return subject?.Tags?.Contains(tag) ?? false;
                }

                default:
                    WarnUnknownFunction(name);
                    return true; // fail-open
            }
        }

        // ──────────────────────────────────────────────
        //  Identifier resolution
        // ──────────────────────────────────────────────

        /// <summary>
        /// Resolves a bare or dotted identifier to a value.
        /// Handles BG3 enum values like <c>Size.Medium</c>, <c>DamageFlags.Hit</c>, etc.
        /// </summary>
        private object ResolveIdentifier(string name)
        {
            // Boolean literals
            if (name.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;

            // Known enums
            // Size.X → numeric value
            if (name.StartsWith("Size.", StringComparison.OrdinalIgnoreCase))
            {
                string sizeVal = name.Substring(5);
                if (Enum.TryParse<CreatureSize>(sizeVal, true, out var cs))
                    return (double)(int)cs;
            }

            // WeaponProperties.X → the property name (used by HasWeaponProperty comparison)
            if (name.StartsWith("WeaponProperties.", StringComparison.OrdinalIgnoreCase))
                return name; // keep as-is for the function to parse

            // DamageFlags.X
            if (name.StartsWith("DamageFlags.", StringComparison.OrdinalIgnoreCase))
                return name;

            // Numeric-looking identifiers
            if (double.TryParse(name, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
                return num;

            // Fallback: return as string
            return name;
        }

        // ──────────────────────────────────────────────
        //  Helpers
        // ──────────────────────────────────────────────

        /// <summary>Extracts a string from the args list at the given index.</summary>
        private static string ArgString(List<object> args, int index)
        {
            if (index < 0 || index >= args.Count) return "";
            return args[index]?.ToString() ?? "";
        }

        /// <summary>Extracts a double from the args list at the given index.</summary>
        private static double ArgDouble(List<object> args, int index, double fallback = 0)
        {
            if (index < 0 || index >= args.Count) return fallback;
            if (args[index] is double d) return d;
            if (args[index] is bool b) return b ? 1 : 0;
            if (double.TryParse(args[index]?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                return parsed;
            return fallback;
        }

        /// <summary>Strips surrounding single quotes from a string, if present.</summary>
        private static string StripQuotes(string s)
        {
            if (s != null && s.Length >= 2 && s[0] == '\'' && s[^1] == '\'')
                return s.Substring(1, s.Length - 2);
            return s ?? "";
        }

        /// <summary>
        /// Resolves a "target" argument that may be a combatant reference like <c>context.Source</c>
        /// or <c>context.Target</c>.
        /// </summary>
        private Combatant ResolveTargetArg(List<object> args, int index, Combatant fallback)
        {
            if (index < 0 || index >= args.Count) return fallback;
            string val = args[index]?.ToString() ?? "";
            if (val.Equals("context.Source", StringComparison.OrdinalIgnoreCase)) return _ctx.Source;
            if (val.Equals("context.Target", StringComparison.OrdinalIgnoreCase)) return _ctx.Target;
            return fallback;
        }

        /// <summary>Converts an arbitrary value to bool.</summary>
        private static bool ToBool(object value)
        {
            if (value is bool b) return b;
            if (value is double d) return d != 0;
            if (value is int i) return i != 0;
            if (value is string s) return !string.IsNullOrEmpty(s) && !s.Equals("false", StringComparison.OrdinalIgnoreCase);
            return value != null;
        }

        /// <summary>
        /// Compares two values using the given operator.
        /// Attempts numeric comparison first, then string comparison.
        /// </summary>
        private static bool CompareValues(object left, object right, TokenType op)
        {
            double? leftNum = ToDouble(left);
            double? rightNum = ToDouble(right);

            if (leftNum.HasValue && rightNum.HasValue)
            {
                double l = leftNum.Value;
                double r = rightNum.Value;
                return op switch
                {
                    TokenType.LessThan => l < r,
                    TokenType.LessEqual => l <= r,
                    TokenType.GreaterThan => l > r,
                    TokenType.GreaterEqual => l >= r,
                    TokenType.Equal => Math.Abs(l - r) < 0.0001,
                    TokenType.NotEqual => Math.Abs(l - r) >= 0.0001,
                    _ => false
                };
            }

            // String comparison
            string ls = left?.ToString() ?? "";
            string rs = right?.ToString() ?? "";
            int cmp = string.Compare(ls, rs, StringComparison.OrdinalIgnoreCase);
            return op switch
            {
                TokenType.Equal => cmp == 0,
                TokenType.NotEqual => cmp != 0,
                TokenType.LessThan => cmp < 0,
                TokenType.LessEqual => cmp <= 0,
                TokenType.GreaterThan => cmp > 0,
                TokenType.GreaterEqual => cmp >= 0,
                _ => false
            };
        }

        /// <summary>Tries to convert an object to double.</summary>
        private static double? ToDouble(object value)
        {
            if (value is double d) return d;
            if (value is int i) return i;
            if (value is bool b) return b ? 1 : 0;
            if (value is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                return parsed;
            return null;
        }

        // ──────────────────────────────────────────────
        //  Domain-specific helpers
        // ──────────────────────────────────────────────

        /// <summary>
        /// Computes the Euclidean distance between two combatants, in game units.
        /// Returns 0 if either combatant is null.
        /// </summary>
        private static double GetDistance(Combatant a, Combatant b)
        {
            if (a == null || b == null) return 0;
            return a.Position.DistanceTo(b.Position);
        }

        /// <summary>
        /// Returns the numeric size of a combatant (Tiny=0 .. Gargantuan=5).
        /// Falls back to <see cref="CreatureSize.Medium"/> (2) if unknown.
        /// </summary>
        private static double GetCreatureSize(Combatant c)
        {
            if (c?.ResolvedCharacter?.Sheet == null)
                return (double)(int)CreatureSize.Medium;

            // Currently CharacterSheet doesn't store size; default to Medium
            return (double)(int)CreatureSize.Medium;
        }

        /// <summary>
        /// Checks whether a combatant has the given status.
        /// Tries the <see cref="ConditionContext.StatusManager"/> first, then falls back
        /// to searching the boost container for a status-sourced boost.
        /// </summary>
        private bool CheckHasStatus(Combatant who, string statusId)
        {
            if (who == null || string.IsNullOrEmpty(statusId))
                return false;

            // Try StatusManager first (most accurate)
            if (_ctx.StatusManager != null)
                return _ctx.StatusManager.HasStatus(who.Id, statusId);

            // Fallback: check if there are boosts sourced from this status
            var fromStatus = who.Boosts.GetBoostsFromSource("Status", statusId);
            return fromStatus.Count > 0;
        }

        /// <summary>
        /// Checks whether the combatant's class level meets the threshold.
        /// </summary>
        private static bool CheckClassLevel(Combatant c, int requiredLevel, string className)
        {
            if (c?.ResolvedCharacter?.Sheet == null)
                return false;

            if (string.IsNullOrEmpty(className))
                return c.ResolvedCharacter.Sheet.TotalLevel >= requiredLevel;

            return c.ResolvedCharacter.Sheet.GetClassLevel(className) >= requiredLevel;
        }

        /// <summary>
        /// Checks if the current weapon has a specific property.
        /// </summary>
        private bool CheckWeaponProperty(string propName)
        {
            var weapon = _ctx.Weapon ?? _ctx.Source?.MainHandWeapon;
            if (weapon == null)
                return false;

            // Strip "WeaponProperties." prefix if present
            if (propName.StartsWith("WeaponProperties.", StringComparison.OrdinalIgnoreCase))
                propName = propName.Substring("WeaponProperties.".Length);

            if (Enum.TryParse<WeaponProperty>(propName, true, out var prop))
                return weapon.Properties.HasFlag(prop);

            return false;
        }

        // ──────────────────────────────────────────────
        //  Warning helpers
        // ──────────────────────────────────────────────

        private void WarnUnknownFunction(string name)
        {
            if (_warnedFunctions.Add(name))
                GD.PushWarning($"[ConditionEvaluator] Unknown function '{name}' — returning true (fail-open). Condition: {_rawCondition}");
        }

        private void WarnStub(string name)
        {
            if (_warnedFunctions.Add(name))
                GD.PushWarning($"[ConditionEvaluator] Stub function '{name}' — not fully implemented yet. Condition: {_rawCondition}");
        }

        private void WarnUnknownArg(string func, string arg)
        {
            string key = $"{func}:{arg}";
            if (_warnedFunctions.Add(key))
                GD.PushWarning($"[ConditionEvaluator] Unknown argument '{arg}' for {func}() — returning true. Condition: {_rawCondition}");
        }
    }
}
