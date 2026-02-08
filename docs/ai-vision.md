# AI Vision Document: BG3-Quality Tactical Combat AI

## Executive Summary

This document defines the target standard for QDND's combat AI: an intelligent, role-aware, environmentally-savvy decision-making system that produces behavior indistinguishable from a skilled human player in an isometric turn-based RPG. The benchmark is Baldur's Gate 3's Honour Mode AI — enemies that use terrain, exploit weaknesses, coordinate as teams, manage resources, and adapt to player strategies.

---

## 1. Core Decision Architecture

### 1.1 Current State
The AI uses a **Utility Scoring** pipeline: Generate candidates → Filter → Score → Select best. This is the right foundation — utility AI is the industry standard for tactical RPGs (used by XCOM 2, BG3, Divinity: OS2).

### 1.2 Target Architecture
Enhance the utility system with:

- **Real outcome prediction** — Replace placeholder damage/hit calculations with actual `RulesEngine` and `EffectPipeline.PreviewAbility()` calls
- **Multi-action planning** — Evaluate turn sequences (move + attack, buff + attack) rather than single actions
- **Team-level coordination** — Cross-combatant awareness for focus fire, combo setups, and role fulfillment
- **Adaptive re-evaluation** — Re-score after each action within a turn based on updated state

---

## 2. Required AI Capabilities

### 2.1 Ability-Aware Decision Making
**Current gap**: `GenerateAbilityCandidates()` is a placeholder stub. AI cannot use spells, special attacks, or class features.

**Target**:
- Enumerate all abilities the combatant possesses via `DataRegistry` / combatant loadout
- Filter by `EffectPipeline.CanUseAbility()` (action economy, cooldowns, resources)
- For each valid ability, generate target combinations using `TargetValidator.GetValidTargets()`
- Score using `EffectPipeline.PreviewAbility()` for expected outcomes
- Differentiate between:
  - **Damage abilities** (direct damage, AoE, DoT)
  - **Healing abilities** (single-target, AoE heals)
  - **Buff/debuff abilities** (status application, removal)
  - **Crowd control** (stun, paralyze, incapacitate)
  - **Utility** (teleport, summon, terrain creation)

### 2.2 Accurate Outcome Prediction
**Current gap**: `CalculateExpectedDamage()` and `CalculateHitChance()` return hardcoded placeholder values (10f and 0.65f).

**Target**:
- **Hit chance**: Use `RulesEngine.CalculateHitChance()` with proper inputs (attacker stats, target AC, advantage/disadvantage from height, cover, flanking, statuses)
- **Expected damage**: Use `EffectPipeline.PreviewAbility()` which returns `(Min, Max, Avg)` damage per effect
- **Expected healing**: Same pipeline for heal-type abilities
- **Save-based abilities**: Factor in target save bonuses vs. spell save DC
- **Status effect value**: Estimate tactical value of CC/debuff duration and impact

### 2.3 Environmental Awareness
**Current gap**: `LOSService` is passed as `null` to `AIScorer`; surface/hazard data not used in scoring.

**Target**:
- **Line of Sight**: Wire `LOSService` into scorer. Use `GetCover()` and `HasLineOfSight()` for attack scoring. Avoid attacks against targets in full cover.
- **Cover**: Seek positions that provide cover from enemies. Use `LOSService.GetCover()` to evaluate candidate move positions.
- **Height advantage**: Fully utilize `HeightService.GetHeightAdvantage()` and `GetAttackModifier()` in hit chance calculations.
- **Surface awareness**: Query `SurfaceManager.GetSurfacesAt()` to avoid moving into hazards. Score abilities that create favorable surfaces (grease under enemies, fire on choke points).
- **Hazard creation**: Evaluate surface-creating abilities for area denial and combo potential.
- **Interactive objects**: Detect and use explosive barrels, breakable terrain, and other environment interactables.

### 2.4 Movement Intelligence
**Current gap**: Movement candidate generation is a simple grid sweep. No pathfinding, no opportunity attack awareness.

**Target**:
- **Opportunity attack avoidance**: Use `MovementService.DetectOpportunityAttacks()` to penalize paths that provoke OAs (unless the benefit outweighs the risk)
- **Path cost awareness**: Use `MovementService.GetMovementCostMultiplier()` for terrain that slows movement (difficult terrain, surfaces)
- **Position preview**: Use `MovementService.GetPathPreview()` to evaluate actual movement results including surface transitions
- **Disengage action**: When surrounded, evaluate `Disengage` to safely reposition without OAs
- **Dash action**: Evaluate when extra movement is more valuable than attacking this turn
- **Chokepoint awareness**: Identify narrow passages and position to block enemy movement / force AoE efficiency
- **Retreat intelligence**: Know when to fall back (low HP, outnumbered in melee)

### 2.5 Target Prioritization
**Current gap**: `AITargetEvaluator` exists but uses placeholder threat calculations and isn't wired into the main pipeline.

**Target**:
- Wire `AITargetEvaluator` into `AIDecisionPipeline` for all attack/ability targeting
- **Role detection**: Identify enemy roles from abilities (healer = has heal spells, controller = has CC, etc.)
- **Kill priority**: Focus fire on targets close to death (action economy — removing a combatant removes all their future turns)
- **Threat assessment**: Factor in actual ability loadout, remaining resources, and position
- **CC priority**: Prefer crowd-controlling high-threat targets that haven't acted yet this round
- **Concentration tracking**: Target enemies maintaining concentration spells to break them
- **Triage**: Support roles should heal the most impactful ally first, not the most damaged

### 2.6 Resource Management
**Current gap**: No awareness of spell slots, cooldowns, limited-use abilities, or long-term resource planning.

**Target**:
- **Spell slot conservation**: Don't use high-level abilities on low-threat targets
- **Cooldown awareness**: Track cooldown timers when scoring abilities. Available-but-on-cooldown abilities get filtered.
- **Resource cost weighting**: Factor `AbilityCost.ResourceCosts` into scoring. More expensive = needs higher expected value to justify.
- **Emergency reserves**: Support archetypes should maintain healing reserves for clutch moments
- **Consumable usage**: Know when to use potions, scrolls, and other single-use items

### 2.7 Multi-Action Turn Planning
**Current gap**: AI evaluates one action at a time, doesn't plan sequences.

**Target**:
- **Action sequences**: Evaluate common patterns:
  - Move → Attack (melee close then strike)
  - Bonus Action → Action (buff self then attack)
  - Attack → Move (hit and retreat / kite)
  - Action → Bonus Action (heal then bonus attack)
- **Movement budgeting**: Reserve movement for post-attack repositioning
- **Action economy optimization**: Use all available actions per turn (action + bonus action + movement + free object interaction)
- **Two-phase turns**: Plan a "main objective" (attack/heal/CC) and an "auxiliary objective" (position/buff/item)

### 2.8 Team Coordination
**Current gap**: Each AI evaluates independently with no cross-combatant awareness.

**Target**:
- **Focus fire coordination**: Multiple allies should target the same enemy, not spread damage
- **Flanking setups**: Move to create flanking positions with allies for advantage
- **Combo awareness**: Set up combos (one ally creates surface, another ignites it; one ally Shoves to prone, melee allies attack at advantage)
- **Role fulfillment**: Tanks should position to absorb hits; supports should stay protected; controllers should CC priority targets
- **Turn order awareness**: Know which allies act later this round; set up for their turns
- **Avoid redundancy**: Don't CC an already-CC'd target; don't heal a full-HP ally; don't all attack different targets

### 2.9 Reaction Management
**Current gap**: `AIReactionPolicy` exists with good structure but uses placeholder damage/hit values and isn't connected to runtime `ReactionSystem`.

**Target**:
- Wire `AIReactionPolicy` into `ReactionSystem` event hooks for AI combatants
- **Opportunity attacks**: Take them unless saving reaction for something better
- **Defensive reactions**: Use Shield/Parry when incoming damage is significant
- **Counterspell decisions**: Counter high-value spells (heals, AoE damage, powerful buffs)
- **Reaction budgeting**: Consider whether it's worth spending reaction now vs potentially needing it later this round
- **Sentinel / special reactions**: Support feats that grant special reaction triggers

### 2.10 Difficulty Scaling (Without Stat Inflation)
**Current gap**: Difficulty only adjusts randomness and focus-fire toggle.

**Target**:
- **Easy**: Intentional suboptimal play. Don't focus fire. Use basic attacks more often. Delay using powerful abilities. Ignore positioning advantages.
- **Normal**: Good play but not perfect. Occasional suboptimal positioning. Use abilities appropriately. Focus wounded targets.
- **Hard**: Near-optimal play. Strong focus fire. Exploit weaknesses (low saves, no reaction). Team coordination. Environmental exploitation.
- **Nightmare**: Perfect play. Maximum exploitation. Predictive play based on player patterns. Full combo awareness. Counter-play player strategies.

### 2.11 Adaptive Behavior
**Target**:
- **HP-responsive**: Switch from aggressive to defensive when health drops
- **Numerical advantage**: Play more aggressively when outnumbering the player; more carefully when outnumbered
- **Ability-responsive**: If a key ally (healer) goes down, adapt strategy
- **Positional adaptation**: If player consistently uses choke points, AI should adapt pathfinding
- **Status-responsive**: Factor in current buffs/debuffs when scoring actions

---

## 3. Role-Specific Behavior Profiles

### 3.1 Melee DPS (Berserker/Aggressive)
- Close distance to weakest/most damaged enemy
- Use power attacks on wounded targets for kills
- Accept opportunity attacks if the trade is favorable
- Use mobility abilities to bypass frontline
- Flank with allies for advantage

### 3.2 Ranged DPS
- Maintain optimal range (~30ft)
- Prefer targets without cover
- Reposition to break cover / gain elevation
- Disengage if forced into melee
- Focus fire coordinator (highest per-target contribution)

### 3.3 Tank (Defensive)
- Position between enemies and allies
- Use AoE taunt / threat abilities
- Take opportunity attacks on passing enemies
- Hold chokepoints
- Self-heal / use defensive abilities under pressure

### 3.4 Healer (Support)
- Prioritize healing critically wounded allies
- Buffer cast: buff allies before combat reaches them
- Conservative resource usage early, save for emergencies
- Position behind frontline but within heal range
- Dispel negative statuses on key allies

### 3.5 Controller
- CC highest-threat enemies at start of combat
- Place AoE effects on clusters
- Create difficult terrain / hazard surfaces on chokepoints
- Avoid redundant CC on controlled targets
- Break enemy concentration with targeted attacks

---

## 4. Technical Integration Points

### 4.1 Service Wiring (Currently Missing)
These existing services need to be connected to the AI pipeline:

| Service | Currently Wired | Needed For |
|---------|----------------|------------|
| `LOSService` | ❌ (null) | Cover, LOS checking, target visibility |
| `RulesEngine` | ❌ | Real hit chance, save DC, damage calc |
| `EffectPipeline` | ❌ | Ability preview, canUse validation |
| `TargetValidator` | ❌ | Valid target enumeration |
| `SurfaceManager` | ❌ | Hazard awareness, surface combo scoring |
| `MovementService` | ❌ | OA detection, path cost |
| `ForcedMovementService` | ❌ | Shove/push prediction |
| `HeightService` | ✅ (partial) | Attack mods, fall damage |
| `SpecialMovementService` | ✅ (partial) | Jump/climb candidates |
| `DataRegistry` | ❌ | Ability loadout enumeration |

### 4.2 Combatant Data Needed
The AI needs access to per-combatant ability loadouts. Currently `Combatant` doesn't expose a list of known/equipped abilities. Need:
- `Combatant.Abilities` — list of ability IDs the combatant can use
- `Combatant.Tags` — already exists but needs consistent population

### 4.3 Performance Constraints
- Decision budget: 500ms per combatant (configurable)
- Candidate explosion: With full ability × target enumeration, candidate count grows fast
- Mitigation: Prune candidates early (filter by `CanUseAbility`, `GetValidTargets`, range check)
- Lazy evaluation: Only score abilities that pass basic validity checks
- Caching: Cache ThreatMap, LOS results, and ability previews within a single decision

---

## 5. Success Criteria

### 5.1 Qualitative
- [ ] AI uses context-appropriate abilities (healing when allies are hurt, CC on dangerous targets, AoE on clusters)
- [ ] AI positions intelligently (cover, high ground, flanking, range maintenance)
- [ ] AI manages resources (doesn't waste powerful spells on near-dead enemies)
- [ ] AI coordinates as a team (focus fire, flanking, role fulfillment)
- [ ] AI adapts to situation (aggression changes with HP, numbers, positioning)
- [ ] AI feels different at each difficulty level
- [ ] AI feels "fair" — doesn't use information a player couldn't have

### 5.2 Quantitative
- [ ] Average decision time < 200ms on Normal difficulty
- [ ] All combatants use their full ability kit (not just basic attacks)
- [ ] Focus fire causes 60%+ of damage to go to the same target (Hard+)
- [ ] AI win rate scales with difficulty: Easy (~30%), Normal (~50%), Hard (~70%), Nightmare (~85%)
- [ ] Zero "wasted turns" — every turn includes at least one meaningful action
- [ ] Auto-battle stress tests pass with 50+ seeds without freeze or infinite loop
