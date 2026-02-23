using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Environment;
using QDND.Combat.Rules.Boosts;
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

        /// <summary>Optional registry for data-driven lookups (e.g. spellcasting ability).</summary>
        public QDND.Data.CharacterModel.CharacterDataRegistry Registry { get; set; }

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
                return false; // fail-closed so broken conditions don't silently grant boosts
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
                    // Character() — is a real character (not a summon/object)
                    return subject != null && subject.Faction != Faction.Neutral && string.IsNullOrEmpty(subject.OwnerId);

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
                    string profName = StripQuotes(ArgString(args, 0));
                    Combatant who = ResolveTargetArg(args, 1, subject);
                    return CheckProficiency(who, profName);
                }

                // ── Spellcasting ability ──
                case "spellcastingabilityis":
                {
                    string ability = StripQuotes(ArgString(args, 0));
                    return CheckSpellcastingAbility(subject, ability);
                }

                case "usingspellslot":
                {
                    // True if casting a leveled spell (not a cantrip or at-will ability)
                    if (_ctx.SpellLevel >= 0) return _ctx.SpellLevel > 0;
                    return _ctx.IsSpell;
                }

                case "usingactionresource":
                {
                    string resource = StripQuotes(ArgString(args, 0));
                    return subject?.ActionResources?.HasResource(resource) ?? true;
                }

                // ── Condition helpers ──
                case "hasusedheavyarmor":
                {
                    Combatant armorWho = ResolveTargetArg(args, 0, subject);
                    return CheckWearingArmor(armorWho, "Heavy");
                }

                case "iswearingarmor":
                {
                    string armorType = StripQuotes(ArgString(args, 0));
                    Combatant armorSubject = ResolveTargetArg(args, 1, subject);
                    return CheckWearingArmor(armorSubject, armorType);
                }

                case "isequippedwith":
                {
                    string itemType = StripQuotes(ArgString(args, 0));
                    Combatant equipWho = ResolveTargetArg(args, 1, subject);
                    return CheckEquippedWith(equipWho, itemType);
                }

                case "hasshieldequipped":
                {
                    return subject?.HasShield ?? false;
                }

                case "isconcentrating":
                {
                    Combatant concWho = ResolveTargetArg(args, 0, subject);
                    return CheckIsConcentrating(concWho);
                }

                case "tagged":
                case "hastag":
                {
                    string tag = StripQuotes(ArgString(args, 0));
                    return subject?.Tags?.Contains(tag) ?? false;
                }

                // ── New BG3 condition functions ──

                case "iscantrip":
                {
                    // A cantrip is a level 0 spell
                    if (_ctx.SpellLevel >= 0) return _ctx.SpellLevel == 0;
                    // Fallback: approximation if SpellLevel not set
                    return _ctx.IsSpell && !_ctx.IsWeaponAttack;
                }

                case "isunarmedattack":
                {
                    return _ctx.IsWeaponAttack && (_ctx.Weapon == null ||
                        (_ctx.Weapon.Name != null && _ctx.Weapon.Name.Contains("Unarmed", StringComparison.OrdinalIgnoreCase)));
                }

                case "imissed":
                case "ismiss":
                {
                    return !_ctx.IsHit;
                }

                case "hasanystatus":
                {
                    foreach (var arg in args)
                    {
                        string statusId = StripQuotes(arg?.ToString() ?? "");
                        if (!string.IsNullOrEmpty(statusId) && CheckHasStatus(subject, statusId))
                            return true;
                    }
                    return false;
                }

                case "isimmunetostatus":
                {
                    string immuneStatusId = StripQuotes(ArgString(args, 0));
                    var immunities = BoostEvaluator.GetStatusImmunities(subject);
                    return immunities.Contains(immuneStatusId);
                }

                case "turnbased":
                {
                    // Always true in combat (our game is always turn-based)
                    return true;
                }

                case "spellid":
                {
                    string spellId = StripQuotes(ArgString(args, 0));
                    return _ctx.SpellId != null && _ctx.SpellId.Equals(spellId, StringComparison.OrdinalIgnoreCase);
                }

                case "hasanytags":
                case "hasanytag":
                {
                    foreach (var arg in args)
                    {
                        string tagVal = StripQuotes(arg?.ToString() ?? "");
                        if (!string.IsNullOrEmpty(tagVal) && (subject?.Tags?.Contains(tagVal) ?? false))
                            return true;
                    }
                    return false;
                }

                // ── Damage type checks ──
                case "isdamagetypefire":
                    return CheckDamageType("Fire");
                case "isdamagetypeacid":
                    return CheckDamageType("Acid");
                case "isdamagetypecold":
                    return CheckDamageType("Cold");
                case "isdamagetypelightning":
                    return CheckDamageType("Lightning");
                case "isdamagetypethunder":
                    return CheckDamageType("Thunder");
                case "isdamagetypepoison":
                    return CheckDamageType("Poison");
                case "isdamagetypenecrotic":
                    return CheckDamageType("Necrotic");
                case "isdamagetyperadiant":
                    return CheckDamageType("Radiant");
                case "isdamagetypeforce":
                    return CheckDamageType("Force");
                case "isdamagetypepsychic":
                    return CheckDamageType("Psychic");
                case "isdamagetypebludgeoning":
                    return CheckDamageType("Bludgeoning");
                case "isdamagetypepiercing":
                    return CheckDamageType("Piercing");
                case "isdamagetypeslashing":
                    return CheckDamageType("Slashing");

                // ── Heavy armor check (critical for BG3 Rage conditions) ──
                case "hasheavyarmor":
                {
                    Combatant armorWho = ResolveTargetArg(args, 0, subject);
                    return CheckWearingArmor(armorWho, "Heavy");
                }

                // ── Entity / Faction checks ──
                case "ally":
                {
                    if (_ctx.Source == null || _ctx.Target == null) return false;
                    Combatant who = ResolveTargetArg(args, 0, _ctx.Target);
                    return who.Faction == _ctx.Source.Faction;
                }

                case "enemy":
                {
                    if (_ctx.Source == null || _ctx.Target == null) return false;
                    Combatant who = ResolveTargetArg(args, 0, _ctx.Target);
                    return who.Faction != _ctx.Source.Faction && who.Faction != Faction.Neutral;
                }

                case "player":
                {
                    return subject?.IsPlayerControlled ?? false;
                }

                case "party":
                {
                    return subject?.IsPlayerControlled ?? false;
                }

                case "item":
                {
                    return false;
                }

                case "summon":
                {
                    return !string.IsNullOrEmpty(subject?.OwnerId);
                }

                case "summonowner":
                case "getsummoner":
                {
                    return (object)(subject?.OwnerId ?? "");
                }

                // ── Attack type checks ──
                case "isattack":
                {
                    return _ctx.IsWeaponAttack || _ctx.IsSpellAttack;
                }

                case "isattacktype":
                {
                    string atkType = ArgString(args, 0).ToLowerInvariant().Replace("attacktype.", "");
                    return atkType switch
                    {
                        "meleeweaponattack" => _ctx.IsMelee && _ctx.IsWeaponAttack,
                        "rangedweaponattack" => _ctx.IsRanged && _ctx.IsWeaponAttack,
                        "meleespellattack" => _ctx.IsMelee && _ctx.IsSpellAttack,
                        "rangedspellattack" => _ctx.IsRanged && _ctx.IsSpellAttack,
                        _ => false
                    };
                }

                // ── Combat state checks ──
                case "combat":
                {
                    return true;
                }

                case "isdowned":
                {
                    Combatant who = ResolveTargetArg(args, 0, subject);
                    return who?.LifeState == CombatantLifeState.Downed;
                }

                case "iskillingblow":
                {
                    if (_ctx.Target == null) return false;
                    return _ctx.DamageDealt > 0 && _ctx.Target.Resources.CurrentHP <= 0;
                }

                case "iscrowdcontrolled":
                {
                    Combatant who = ResolveTargetArg(args, 0, subject);
                    if (who == null) return false;
                    string[] ccStatuses = { "STUNNED", "PARALYZED", "INCAPACITATED", "PETRIFIED", "UNCONSCIOUS", "SLEEP", "PRONE" };
                    foreach (var cc in ccStatuses)
                        if (CheckHasStatus(who, cc)) return true;
                    return false;
                }

                case "immobilized":
                {
                    Combatant who = ResolveTargetArg(args, 0, subject);
                    if (who == null) return false;
                    string[] immobStatuses = { "RESTRAINED", "PARALYZED", "STUNNED", "GRAPPLED", "PETRIFIED", "ENTANGLED", "PRONE" };
                    foreach (var s in immobStatuses)
                        if (CheckHasStatus(who, s)) return true;
                    return false;
                }

                // ── HP checks ──
                case "hasmaxhp":
                {
                    Combatant who = ResolveTargetArg(args, 0, subject);
                    if (who == null) return false;
                    return who.Resources.CurrentHP >= who.Resources.MaxHP;
                }

                case "hashplessthan":
                {
                    double threshold = ArgDouble(args, 0, 0);
                    Combatant who = ResolveTargetArg(args, 1, subject);
                    return (who?.Resources.CurrentHP ?? 0) < threshold;
                }

                case "hashpmorethan":
                {
                    double threshold = ArgDouble(args, 0, 0);
                    Combatant who = ResolveTargetArg(args, 1, subject);
                    return (who?.Resources.CurrentHP ?? 0) > threshold;
                }

                case "hashppercentagelessthan":
                {
                    double pct = ArgDouble(args, 0, 50);
                    Combatant who = ResolveTargetArg(args, 1, subject);
                    if (who == null || who.Resources.MaxHP <= 0) return false;
                    double ratio = (double)who.Resources.CurrentHP / who.Resources.MaxHP * 100.0;
                    return ratio < pct;
                }

                // ── Resource checks ──
                case "hasactionresource":
                {
                    string resource = StripQuotes(ArgString(args, 0));
                    int amount = (int)ArgDouble(args, 1, 1);
                    int level = (int)ArgDouble(args, 2, 0);
                    Combatant who = ResolveTargetArg(args, 3, subject);
                    if (who?.ActionResources == null) return true; // fail-open
                    if (!who.ActionResources.HasResource(resource)) return false;
                    return who.ActionResources.Has(resource, amount, level);
                }

                case "hasactiontype":
                {
                    return true;
                }

                // ── Weapon / Equipment checks ──
                case "isproficientwithequippedweapon":
                {
                    Combatant who = ResolveTargetArg(args, 0, subject);
                    if (who?.MainHandWeapon == null) return true; // unarmed = proficient
                    return CheckProficiency(who, who.MainHandWeapon.WeaponType.ToString());
                }

                case "hasweaponinmainhand":
                {
                    Combatant who = ResolveTargetArg(args, 0, subject);
                    return who?.MainHandWeapon != null;
                }

                case "dualwielder":
                {
                    Combatant who = ResolveTargetArg(args, 0, subject);
                    return who?.MainHandWeapon != null && who?.OffHandWeapon != null;
                }

                case "unarmed":
                {
                    Combatant who = ResolveTargetArg(args, 0, subject);
                    return who?.MainHandWeapon == null;
                }

                case "hasmetalarmorinanyhand":
                case "hasmetalweaponinanyhand":
                {
                    Combatant who = ResolveTargetArg(args, 0, subject);
                    return who?.MainHandWeapon != null || who?.OffHandWeapon != null;
                }

                case "hasmetalarmor":
                case "ismetalcharacter":
                {
                    Combatant who = ResolveTargetArg(args, 0, subject);
                    if (who?.EquippedArmor == null) return false;
                    return who.EquippedArmor.Category == ArmorCategory.Heavy || who.EquippedArmor.Category == ArmorCategory.Medium;
                }

                // ── Spell property checks ──
                case "hasspellflag":
                {
                    string flagName = StripQuotes(ArgString(args, 0));
                    if (string.IsNullOrEmpty(flagName)) return false;
                    return _ctx.SpellFlags?.Contains(flagName) ?? false;
                }

                case "isspellschool":
                {
                    string schoolStr = ArgString(args, 0);
                    if (schoolStr.StartsWith("SpellSchool.", StringComparison.OrdinalIgnoreCase))
                        schoolStr = schoolStr.Substring("SpellSchool.".Length);
                    if (Enum.TryParse<QDND.Combat.Actions.SpellSchool>(schoolStr, true, out var school))
                        return _ctx.SpellSchool == school;
                    return false;
                }

                case "spelltypeis":
                {
                    string spellType = StripQuotes(ArgString(args, 0));
                    return _ctx.SpellType != null && _ctx.SpellType.Equals(spellType, StringComparison.OrdinalIgnoreCase);
                }

                case "statusid":
                {
                    string statusId = StripQuotes(ArgString(args, 0));
                    return _ctx.StatusTriggerId != null && _ctx.StatusTriggerId.Equals(statusId, StringComparison.OrdinalIgnoreCase);
                }

                // ── Size checks ──
                case "sizeequalorgreater":
                case "sizeequalorgreater1":
                {
                    double threshold = ArgDouble(args, 0, (double)(int)CreatureSize.Medium);
                    Combatant who = ResolveTargetArg(args, 1, subject);
                    return (double)(int)(who?.CreatureSize ?? CreatureSize.Medium) >= threshold;
                }

                case "targetsizeequalorsmaller":
                {
                    double threshold = ArgDouble(args, 0, (double)(int)CreatureSize.Medium);
                    Combatant who = _ctx.Target;
                    return (double)(int)(who?.CreatureSize ?? CreatureSize.Medium) <= threshold;
                }

                // ── Damage amount checks ──
                case "totaldamagedonegreaterthan":
                {
                    double threshold = ArgDouble(args, 0, 0);
                    return _ctx.DamageDealt > threshold;
                }

                case "totalattackdamagedonegreaterthan":
                {
                    double threshold = ArgDouble(args, 0, 0);
                    return _ctx.DamageDealt > threshold;
                }

                case "hasdamagedonefortype":
                {
                    string typeStr = ArgString(args, 0);
                    if (typeStr.StartsWith("DamageType.", StringComparison.OrdinalIgnoreCase))
                        typeStr = typeStr.Substring("DamageType.".Length);
                    if (_ctx.DamageByType != null && Enum.TryParse<DamageType>(typeStr, true, out var dmgType))
                        return _ctx.DamageByType.ContainsKey(dmgType) && _ctx.DamageByType[dmgType] > 0;
                    return CheckDamageType(typeStr);
                }

                case "healdonegreaterthan":
                {
                    double threshold = ArgDouble(args, 0, 0);
                    return _ctx.HealAmount > threshold;
                }

                // ── Distance / Spatial checks ──
                case "distancetotargetlessthan":
                {
                    double threshold = ArgDouble(args, 0, 0);
                    double dist = GetDistance(_ctx.Source, _ctx.Target);
                    return dist < threshold;
                }

                case "inmeleerange":
                {
                    double meleeRange = 1.5;
                    if (_ctx.Weapon != null && _ctx.Weapon.HasReach)
                        meleeRange = 3.0; // 10ft reach
                    double dist = GetDistance(_ctx.Source, _ctx.Target);
                    return dist <= meleeRange;
                }

                case "hasallywithinrange":
                {
                    double range = ArgDouble(args, 0, 1.5);
                    Combatant who = ResolveTargetArg(args, 1, subject);
                    if (who == null || _ctx.AllCombatants == null) return true; // fail-open
                    foreach (var c in _ctx.AllCombatants)
                    {
                        if (c == who || c.LifeState != CombatantLifeState.Alive) continue;
                        if (c.Faction == who.Faction && GetDistance(who, c) <= range)
                            return true;
                    }
                    return false;
                }

                case "ydistancetotargetgreaterorequal":
                {
                    double threshold = ArgDouble(args, 0, 0);
                    if (_ctx.Source == null || _ctx.Target == null) return false;
                    double yDist = Math.Abs(_ctx.Source.Position.Y - _ctx.Target.Position.Y);
                    return yDist >= threshold;
                }

                // ── Advantage / disadvantage checks ──
                case "hasadvantage":
                {
                    return _ctx.HasAdvantageOnRoll;
                }

                case "hasdisadvantage":
                {
                    return _ctx.HasDisadvantageOnRoll;
                }

                // ── Movement / state checks ──
                case "grounded":
                {
                    return true;
                }

                case "ismovable":
                {
                    Combatant who = ResolveTargetArg(args, 0, subject);
                    if (who == null) return false;
                    if (who.LifeState == CombatantLifeState.Dead) return false;
                    if (CheckHasStatus(who, "PETRIFIED")) return false;
                    return true;
                }

                case "canstand":
                {
                    Combatant who = ResolveTargetArg(args, 0, subject);
                    if (who == null) return false;
                    return who.LifeState == CombatantLifeState.Alive && !CheckHasStatus(who, "PARALYZED") && !CheckHasStatus(who, "PETRIFIED");
                }

                case "isonfirealiased":
                case "isonfire":
                {
                    Combatant who = ResolveTargetArg(args, 0, subject);
                    return who != null && CheckHasStatus(who, "BURNING");
                }

                // ── Context flag checks ──
                case "hascontextflag":
                case "context.hascontextflag":
                {
                    string flag = StripQuotes(ArgString(args, 0));
                    if (flag.StartsWith("StatsFunctorContext.", StringComparison.OrdinalIgnoreCase))
                        flag = flag.Substring("StatsFunctorContext.".Length);
                    return _ctx.FunctorContext != null && _ctx.FunctorContext.Equals(flag, StringComparison.OrdinalIgnoreCase);
                }

                // ── Misc / Niche checks ──
                case "hasproficiency":
                {
                    string profId = StripQuotes(ArgString(args, 0));
                    Combatant who = ResolveTargetArg(args, 1, subject);
                    return CheckProficiency(who, profId);
                }

                case "hasusecosts":
                {
                    return true;
                }

                case "extraattackspellcheck":
                {
                    return _ctx.IsWeaponAttack && (subject?.ExtraAttacks ?? 0) > 0;
                }

                case "canenlarge":
                {
                    Combatant who = ResolveTargetArg(args, 0, subject);
                    if (who == null) return false;
                    var size = who.CreatureSize;
                    return (size == CreatureSize.Small || size == CreatureSize.Medium) && !CheckHasStatus(who, "ENLARGED");
                }

                case "canshoveweight":
                {
                    return true;
                }

                case "attackedwithpassivesourceweapon":
                {
                    return _ctx.IsWeaponAttack;
                }

                case "hashexstatus":
                {
                    Combatant who = ResolveTargetArg(args, 0, subject);
                    return who != null && (CheckHasStatus(who, "HEX") || CheckHasStatus(who, "YOURSLEVEL_HEX"));
                }

                case "hasinstrumentequipped":
                {
                    return true;
                }

                case "hasthrownweaponininventory":
                {
                    Combatant who = ResolveTargetArg(args, 0, subject);
                    if (who?.MainHandWeapon?.IsThrown ?? false) return true;
                    if (who?.OffHandWeapon?.IsThrown ?? false) return true;
                    return false;
                }

                case "surface":
                case "insurface":
                {
                    Combatant surfWho = ResolveTargetArg(args, 0, subject);
                    if (surfWho == null || _ctx.SurfaceManager == null)
                        return true; // fail-open
                    var surfaces = _ctx.SurfaceManager.GetSurfacesForCombatant(surfWho);
                    return surfaces.Count > 0;
                }

                case "isdippablesurface":
                {
                    Combatant dipWho = ResolveTargetArg(args, 0, subject);
                    if (dipWho == null || _ctx.SurfaceManager == null)
                        return true; // fail-open
                    var dipSurfaces = _ctx.SurfaceManager.GetSurfacesForCombatant(dipWho);
                    foreach (var s in dipSurfaces)
                    {
                        var st = s.Definition.Type;
                        if (st == SurfaceType.Fire ||
                            st == SurfaceType.Poison ||
                            st == SurfaceType.Acid)
                            return true;
                    }
                    return false;
                }

                case "iswaterbasedsurface":
                {
                    Combatant waterWho = ResolveTargetArg(args, 0, subject);
                    if (waterWho == null || _ctx.SurfaceManager == null)
                        return true; // fail-open
                    var waterSurfaces = _ctx.SurfaceManager.GetSurfacesForCombatant(waterWho);
                    foreach (var s in waterSurfaces)
                    {
                        if (s.Definition.Type == SurfaceType.Water)
                            return true;
                    }
                    return false;
                }

                case "isinsunlight":
                {
                    return true;
                }

                case "freshcorpse":
                case "istargetablecorpse":
                {
                    Combatant who = ResolveTargetArg(args, 0, subject);
                    return who?.LifeState == CombatantLifeState.Dead;
                }

                case "locked":
                {
                    return false;
                }

                case "wildmagicspell":
                {
                    return subject?.ResolvedCharacter?.Sheet?.GetClassLevel("Sorcerer") > 0 &&
                           _ctx.IsSpell;
                }

                case "spellactivations":
                {
                    return true;
                }

                case "hasheatmetalactive":
                case "hasheatmetalactivehigherlevels":
                {
                    Combatant who = ResolveTargetArg(args, 0, subject);
                    return who != null && CheckHasStatus(who, "HEAT_METAL");
                }

                case "hasverbalcomponentblocked":
                {
                    Combatant who = ResolveTargetArg(args, 0, subject);
                    return who != null && CheckHasStatus(who, "SILENCED");
                }

                case "hashelpablecondition":
                {
                    Combatant who = ResolveTargetArg(args, 0, subject);
                    if (who == null) return false;
                    return who.LifeState == CombatantLifeState.Downed ||
                           CheckHasStatus(who, "PRONE") ||
                           CheckHasStatus(who, "FRIGHTENED");
                }

                case "hasattribute":
                {
                    return true;
                }

                case "intelligencegreaterthan":
                {
                    double threshold = ArgDouble(args, 0, 0);
                    Combatant who = ResolveTargetArg(args, 1, subject);
                    int intel = who?.GetAbilityScore(AbilityType.Intelligence) ?? 10;
                    return intel > threshold;
                }

                case "characterlevelgreaterthan":
                {
                    int level = (int)ArgDouble(args, 0, 0);
                    return (subject?.ResolvedCharacter?.Sheet?.TotalLevel ?? 1) > level;
                }

                case "getactiveweapon":
                {
                    return (object)(subject?.MainHandWeapon?.WeaponType.ToString() ?? "Unarmed");
                }

                // ── Passive / Feat pseudo-functions ──

                case "fightingstyle_dueling":
                {
                    var target = ResolveTargetArg(args, 0, subject);
                    return target?.PassiveManager?.HasPassive("FightingStyle_Dueling") ?? false;
                }

                case "greatweaponmaster":
                {
                    var target = ResolveTargetArg(args, 0, subject);
                    return target?.PassiveManager?.IsToggled("GreatWeaponMaster_BonusDamage") ?? false;
                }

                case "sharpshooter":
                {
                    var target = ResolveTargetArg(args, 0, subject);
                    return target?.PassiveManager?.IsToggled("Sharpshooter_AllIn") ?? false;
                }

                default:
                    WarnUnknownFunction(name);
                    return false; // fail-closed
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
            if (c == null)
                return (double)(int)CreatureSize.Medium;
            return (double)(int)c.CreatureSize;
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
        /// Checks whether the combatant is proficient with a weapon or armor type.
        /// Checks CharacterSheet proficiencies first, then boost-granted proficiencies.
        /// </summary>
        private bool CheckProficiency(Combatant who, string profName)
        {
            if (who == null || string.IsNullOrEmpty(profName))
                return true; // fail-open

            var profs = who.ResolvedCharacter?.Proficiencies;
            if (profs != null)
            {
                // Check weapon type proficiency
                if (Enum.TryParse<WeaponType>(profName, true, out var wt) && profs.IsProficientWithWeapon(wt))
                    return true;

                // Check weapon category proficiency
                if (Enum.TryParse<WeaponCategory>(profName, true, out var wc) && profs.IsProficientWithWeaponCategory(wc))
                    return true;

                // Check armor proficiency
                if (Enum.TryParse<ArmorCategory>(profName, true, out var ac) && profs.IsProficientWithArmor(ac))
                    return true;
            }

            // Check boost-granted proficiencies
            if (BoostEvaluator.HasProficiency(who, "Weapon", profName))
                return true;
            if (BoostEvaluator.HasProficiency(who, "Armor", profName))
                return true;

            // If no proficiency data is available at all, fail-open
            if (profs == null)
                return true;

            return false;
        }

        /// <summary>
        /// Checks if the combatant is concentrating on a spell.
        /// Falls back to checking for a CONCENTRATING tag or status.
        /// </summary>
        private bool CheckIsConcentrating(Combatant who)
        {
            if (who == null)
                return false;

            // Check for concentration status via StatusManager
            if (_ctx.StatusManager != null && _ctx.StatusManager.HasStatus(who.Id, "CONCENTRATING"))
                return true;

            // Fallback: check Tags
            if (who.Tags?.Contains("CONCENTRATING") ?? false)
                return true;

            // Fallback: check boosts sourced from CONCENTRATING status
            var fromStatus = who.Boosts.GetBoostsFromSource("Status", "CONCENTRATING");
            return fromStatus.Count > 0;
        }

        /// <summary>
        /// Checks if the combatant is wearing a specific armor category.
        /// </summary>
        private static bool CheckWearingArmor(Combatant who, string armorType)
        {
            if (who == null || string.IsNullOrEmpty(armorType))
                return false;

            var armor = who.EquippedArmor;
            if (armor != null)
            {
                return armor.Category.ToString().Equals(armorType, StringComparison.OrdinalIgnoreCase);
            }

            // Fallback: check tags
            string tagName = armorType.ToUpperInvariant() + "_ARMOR";
            return who.Tags?.Contains(tagName) ?? false;
        }

        /// <summary>
        /// Checks if the combatant has a specific type of equipment equipped.
        /// </summary>
        private static bool CheckEquippedWith(Combatant who, string itemType)
        {
            if (who == null || string.IsNullOrEmpty(itemType))
                return false;

            // Check shield
            if (itemType.Equals("Shield", StringComparison.OrdinalIgnoreCase))
                return who.HasShield;

            // Check main hand weapon type
            if (who.MainHandWeapon != null)
            {
                if (who.MainHandWeapon.WeaponType.ToString().Equals(itemType, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (who.MainHandWeapon.Name != null && who.MainHandWeapon.Name.Equals(itemType, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Check off hand weapon type
            if (who.OffHandWeapon != null)
            {
                if (who.OffHandWeapon.WeaponType.ToString().Equals(itemType, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (who.OffHandWeapon.Name != null && who.OffHandWeapon.Name.Equals(itemType, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Check armor
            if (who.EquippedArmor != null)
            {
                if (who.EquippedArmor.Category.ToString().Equals(itemType, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (who.EquippedArmor.Name != null && who.EquippedArmor.Name.Equals(itemType, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if the combatant's spellcasting ability matches the given ability.
        /// </summary>
        private bool CheckSpellcastingAbility(Combatant who, string ability)
        {
            if (who == null || string.IsNullOrEmpty(ability))
                return true; // fail-open

            var classLevels = who.ResolvedCharacter?.Sheet?.ClassLevels;
            if (classLevels == null || classLevels.Count == 0)
                return true; // no class data, fail-open

            // Data-driven lookup via registry
            if (Registry != null)
            {
                foreach (var cl in classLevels)
                {
                    var classDef = Registry.GetClass(cl.ClassId);
                    if (!string.IsNullOrEmpty(classDef?.SpellcastingAbility))
                        return classDef.SpellcastingAbility.Equals(ability, StringComparison.OrdinalIgnoreCase);
                }
                return true; // non-caster, fail-open
            }

            // Fallback if registry unavailable
            string classId = classLevels[^1].ClassId?.ToLowerInvariant();
            string spellcastingAbility = classId switch
            {
                "wizard" => "Intelligence",
                "cleric" or "druid" or "ranger" or "monk" => "Wisdom",
                "bard" or "sorcerer" or "warlock" or "paladin" => "Charisma",
                _ => null
            };

            if (spellcastingAbility == null)
                return true; // non-caster, fail-open

            return spellcastingAbility.Equals(ability, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the current damage type matches the expected type.
        /// </summary>
        private bool CheckDamageType(string expectedType)
        {
            return _ctx.DamageType.HasValue &&
                   _ctx.DamageType.Value.ToString().Equals(expectedType, StringComparison.OrdinalIgnoreCase);
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
                GD.PushWarning($"[ConditionEvaluator] Unknown function '{name}' — returning false (fail-closed). Condition: {_rawCondition}");
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
