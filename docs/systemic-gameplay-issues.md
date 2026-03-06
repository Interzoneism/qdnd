Deep Spell Audit: 5 Complex BG3 Spells
1. HOLD PERSON
What it should do: L2 Enchantment, WIS save, Paralyzed (can't act, auto-fail STR/DEX saves, attackers have advantage, melee hits auto-crit within 3m), repeat WIS save at end of each turn. Concentration. Humanoid-only. Upcast: +1 target per level.

What works
WIS saving throw with correct DC formula (8 + prof + ability mod)
Repeat saves at end of target's turn with caster's actual spell DC
Concentration tracking — breaking concentration removes the status
BG3 data parsed correctly from Spell_Target.txt
Critical bugs
#	Bug	Impact
1	Humanoid restriction not enforced — Tagged('HUMANOID') stored in BG3TargetConditions but never evaluated at runtime	Hold Person can target undead, constructs, fiends
2	BG3-path status hold_person not recognized as incapacitating — ConditionEffects.IsIncapacitating("hold_person") returns false	Paralyzed targets still take normal turns
3	Actions not blocked — BG3 StatusType=INCAPACITATED isn't mapped to BlockedActions	Paralyzed target can still act and react
4	No attacker advantage — Advantage(AttackTarget) boost format not parsed by BG3StatusIntegration	Attacks against paralyzed targets don't get advantage
5	No melee auto-crit — CriticalHit(AttackTarget,Success,Always,3) boost not parsed	Melee hits within range aren't auto-crits
6	Target concentration not broken — hold_person not in ExplicitIncapacitatingStatusIds and has no hard_control tag	Paralyzed casters keep concentrating
7	Melee auto-crit range wrong — MeleeAutocritRangeMeters = 1.5f vs BG3's 3m	Even for JSON paralyzed path, range is half correct
8	BG3-path upcast broken — SpellUpcastRules["hold_person"] doesn't match "Target_HoldPerson"	No extra targets when upcasting
Root cause: Two parallel definitions exist — BG3-parsed hold_person (incomplete boost parsing) and JSON paralyzed (missing BlockDexFromAC). Neither is fully correct alone.

2. FIREBALL
What it should do: L3 Evocation, 8d6 fire, 4m radius AoE, DEX save for half, ignites surfaces, +1d6 per upcast level.

What works
AoE targeting with AoECircleMode, correct 4m radius from BG3 data
Per-target DEX saves with correct DC
Fire resistance/vulnerability/immunity via DamagePipeline
Evasion (Rogue/Monk) works correctly
Line of effect from explosion center to targets
Surface ignite/melt effects fire (grease → fire, etc.)
Critical bugs
#	Bug	Impact
1	Half-damage on save BROKEN — CleanDiceFormula discards /2 from (8d6)/2; SaveTakesHalf flag never triggers for on_save_success condition	Targets who succeed DEX save take FULL 8d6 instead of 4d6
2	Upcast scaling missing for BG3 entry — SpellUpcastRules["fireball"] doesn't match "Projectile_Fireball"	Casting at 4th-9th level adds zero extra damage
3	Surface change radius too small — default 2.5m instead of 4m explosion radius	Only inner half of explosion ignites surfaces
4	No fire surface created — ExplodeRadius field never generates a CreateSurface(fire) effect	No persistent fire hazard after Fireball
5	Sculpt Spells non-functional — BG3TargetConditions stored but never evaluated in ResolveAreaTargets	Evocation Wizard allies always take full AoE damage
Bug #1 is especially severe — it means every successful DEX save against Fireball results in full damage rather than half. This affects every AoE save spell that uses the same SpellFail = "DealDamage((XdY)/2,..." pattern.

3. SPIRIT GUARDIANS
What it should do: L3 Conjuration, Concentration, 4.5m aura around caster, enemies take 3d8 radiant/necrotic (WIS save for half) on entry and at start of their turn. Half movement speed in aura. +1d8 per upcast level.

What works
Concentration tracking — start/break handled correctly
JSON status definitions exist for both caster buff and enemy debuff
FATAL: The spell is a complete no-op
#	Bug	Severity
1	spirit_guardians status missing auraRadius/auraStatusId — AuraSystem filter s.Definition.AuraRadius > 0f silently drops it	FATAL — entire mechanic dead
2	No movement-entry detection — MovementService has zero aura mentions	Enemies walking into aura take no damage
3	AuraSystem processes at turn END, not turn START — BG3 damages at start of enemy's turn	Timing wrong even if wired
4	No necrotic variant — only radiant child status exists	No damage type choice
5	Wrong status ID applied by action — spirit_guardians instead of the aura emitter status	Even if aura data were fixed, wrong status applied
6	Upcast rule key mismatch — "spirit_guardians" vs action ID "spirit_guardians_radiant"	Upcast never scales damage
7	No double-damage prevention marker	Could be hit on entry AND turn start in same turn
What actually happens today: Cleric casts Spirit Guardians → status applied to caster → AuraSystem silently drops it → enemies take zero damage, have full movement speed. The spell does nothing except occupy the concentration slot.

4. COUNTERSPELL
What it should do: Reaction, L3 Abjuration, 18m range. Auto-cancels spells of level ≤ slot used. Higher-level spells: Intelligence check DC = 10 + target spell level.

What works
Reaction registration with SpellCastNearby trigger, 18m range
Spell slot requirement check (needs ≥ L3 slot)
Player gets a reaction prompt when enemy casts
CounterEffect correctly sets WasCancelled = true → target spell never executes
AI has score-based heuristic for when to counter
Concentration interaction correct (Counterspell isn't concentration, doesn't break existing)
Critical gaps
#	Bug	Impact
1	No spell level comparison — always auto-succeeds regardless of relative levels	Counterspell at L3 slot auto-cancels L9 spells
2	No ability check — TryCounterspellHigherLevel has no QDND equivalent	No roll when countering higher-level spells
3	Upcast threshold not tracked — counterspellerLevel hardcoded to 3	Upcasting Counterspell provides zero benefit
4	UI shows no spell level — player can't make informed upcast decisions	Missing information in reaction prompt
5	AI has no slot-level awareness — doesn't consider probability of success	AI wastes slots on uncounterable spells
The FunctorExecutor.Counterspell stub is harmless — it's a dead code path since the real execution goes through CounterEffect.

5. HASTE
What it should do: L3 Transmutation, Concentration, target ally. Grants: +2 AC, advantage on DEX saves, doubled movement, +1 action per turn. On end: Lethargy (can't move or act for 1 turn).

What works
+2 AC via modifier stack
Movement speed doubled (100% increase modifier)
DEX save advantage via conditional modifier
Concentration fully wired (start, break, damage saves)
Lethargy triggers correctly on Haste removal (dual-path: JSON triggerEffects + StatusInteractionRules)
Lethargy blocks all actions via BlockedActions
No upcast scaling (correct — Haste doesn't scale)
TargetFilter: "Self, Allies" correctly restricts targets
Critical bugs
#	Bug	Impact
1	Extra action NOT GRANTED — ActionBudget.ResetForTurn() hardcodes _actionCharges = 1, never reads ModifierTarget.ActionPoints	The core benefit of Haste (extra action) doesn't work
2	BG3 HASTE boost parse gaps — ActionResource(Movement/ActionPoint) and Advantage(SavingThrow,Dexterity) silently dropped	Potion of Speed path only gets +2 AC
3	Lethargic not in IsIncapacitating() — turn not auto-skipped	AI may freeze/loop during lethargic turn
4	Duplicate lethargy trigger — both JSON and C# paths fire, guarded only by HasStatus() check	Fragile
5	Status ID split — spell uses "hasted", Potion of Speed uses "haste"	Changes must be made in two places
Systemic Issues Revealed
The 5 spell audits reveal four deep structural problems that affect far more than just these spells:

1. BG3 ID / JSON ID Mismatch (affects ALL BG3-parsed spells)
SpellUpcastRules is keyed to short names ("fireball", "hold_person", "spirit_guardians") but BG3-parsed actions use prefixed IDs ("Projectile_Fireball", "Target_HoldPerson"). The lookup always fails for BG3 entries, falling back to a heuristic that often produces wrong scaling. This means upcast scaling is broken for most BG3-parsed spells — not just Fireball and Hold Person.

2. Save-for-Half Damage Broken System-Wide
The CleanDiceFormula function silently discards /2 divisors from BG3's SpellFail = "DealDamage((XdY)/2,...)" pattern. The SaveTakesHalf flag is set but the condition check (on_save_fail vs on_save_success) prevents it from ever activating. Every AoE save-for-half spell in the game likely deals full damage on successful saves. This affects Fireball, Lightning Bolt, Cone of Cold, Ice Storm, Shatter, and dozens more.

3. BG3StatusIntegration Boost Parser Is Incomplete
BG3StatusIntegration.ParseBoosts() is a separate, simpler parser than BoostParser.ParseBoostString(). It fails on:

Multi-param boosts: Advantage(SavingThrow, Dexterity) — two-arg format doesn't match one-arg regex
Advantage(AttackTarget) / CriticalHit(AttackTarget,...) — "attacktarget" not in ParseModifierTarget switch
ActionResource(Movement/ActionPoint,N,0) — no handler at all
This means any BG3 status that uses these boost formats has silently incomplete effects. Paralyzed, Haste, and likely many other conditions are affected.

4. BG3TargetConditions Never Evaluated at Runtime
action.BG3TargetConditions (containing creature type restrictions like Tagged('HUMANOID') and feature interactions like HasPassive('SculptSpells')) is stored but never read by any targeting or effect system. This means:

Hold Person can target non-humanoids
Sculpt Spells (Evocation Wizard) is non-functional for all AoE spells
Any spell with creature type restrictions (Hold Monster vs Hold Person, etc.) has no enforcement
Priority Ranking of Systemic Fixes
Priority	Systemic Issue	Spells Affected	Fix Scope
P0	Save-for-half damage broken	Every AoE save spell (~30+)	DealDamageEffect.cs condition check logic
P0	SpellUpcastRules ID mismatch	Every BG3-parsed spell with upcast	ID normalization in BG3ActionConverter or SpellUpcastRules
P1	BG3StatusIntegration boost parser gaps	Hold Person, Haste, and ~50+ BG3 statuses	Extend or replace ParseBoosts() with BoostParser
P1	BG3TargetConditions not evaluated	Hold Person, Sculpt Spells, creature-type spells	Wire ConditionEvaluator into TargetValidator
P2	Spirit Guardians completely non-functional	Spirit Guardians	Wire aura data, add movement entry detection
P2	Counterspell no level check	Counterspell	Add level comparison + ability check
P2	Haste extra action not granted	Haste	Wire ModifierTarget.ActionPoints into TurnLifecycleService
Completed: Synthesize findings & systemic issues (7/7)

Bottom line: Of the 5 spells audited, Spirit Guardians is completely non-functional, Haste is missing its core benefit (extra action), Fireball deals full damage to targets who succeed their save, Hold Person's BG3-path doesn't actually paralyze (turn still taken, actions not blocked), and Counterspell auto-succeeds against any spell level. The two most damaging systemic issues — save-for-half broken and upcast ID mismatch — likely affect 30-50+ spells beyond just these five.