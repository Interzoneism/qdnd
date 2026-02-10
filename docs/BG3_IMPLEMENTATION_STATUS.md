# BG3 Implementation Status — The Single Source of Truth

> This document is the authoritative source of truth for the QDND project's Baldur's Gate 3 mechanics implementation. It is generated from a full codebase analysis and consolidates findings from all previous audits and progress reports. This document reflects the **current state of the game** after the latest fixes.

---

## Part 1: High-Level Class Implementation Scorecard

This table provides a quick-glance summary of the **current** implementation status for each class's core mechanics.

| Class | Overall Status | Best Implemented Mechanic | Worst Implementation Gap |
|---|---|---|---|
| **Barbarian** | ✅ **Functional** | Rage (Resistance + Damage Bonus) | Most subclass features still missing. |
| **Bard** | ⛔ **Inaccurate** | Reaction system for Cutting Words works. | **Bardic Inspiration/Cutting Words use flat values, not dice.** |
| **Cleric** | ⚠️ **Partial** | Destructive Wrath (maximizes dice). | Turn Undead hits all enemies, not just Undead. |
| **Druid** | ❌ **Critical Gaps** | Spells (Healing Word, Produce Flame). | **Wild Shape is completely missing.** |
| **Fighter** | ✅ **Functional** | Action Surge, Extra Attack, Battle Master Maneuvers. | Most subclass features (Arcane Archer, Eldritch Knight) are missing. |
| **Monk** | ⚠️ **Partial** | Ki system and Stunning Strike work. | Patient Defence (Dodge) is inaccurate; subclass features missing. |
| **Paladin** | ✅ **Functional** | Divine Smite (on-hit trigger), Aura of Protection. | Lay on Hands is a generic heal, not pool-based. |
| **Ranger** | ❌ **Not Implemented**| N/A | **The entire class is a shell with no functional mechanics.** |
| **Rogue** | ✅ **Functional** | Sneak Attack (fully implemented). | Uncanny Dodge / Evasion still missing. |
| **Sorcerer**| ⛔ **Inaccurate** | Sorcery Points resource system. | **Metamagic is not a generic system**, just hardcoded spell variants. |
| **Warlock** | ✅ **Functional** | Eldritch Blast (with Agonizing Blast), Hex (on-hit). | Pact boons and most invocations are missing. |

---

## Part 2: Detailed Mechanics Audit

This section details the ground truth of what is **actually in the game**, incorporating all recent fixes.

### Legend
- ✅ **REAL** — Working C# runtime code exists and is integrated, faithful to BG3.
- ⚠️ **PARTIAL** — Some functionality works, but significant gaps or inaccuracies remain.
- 🔧 **DATA-ONLY** — JSON definitions exist but no special C# runtime code handles the mechanic.
- ❌ **MISSING** — Neither code nor data exists.
- ⛔ **INACCURATE** — The implementation exists but is mechanically incorrect compared to BG3.
- ✨ **RECENTLY FIXED** - This mechanic was recently implemented or corrected.

### A. Core Combat Systems

| Mechanic | Verdict | Details |
|---|---|---|
| **Core Math** (Attack/Save/DC) | ✅ **REAL** | Full BG3 formula with stats, proficiency, mods, finesse, and class-aware casting. |
| **Extra Attack** | ✅ **REAL** <br> ✨ **RECENTLY FIXED** | The multi-attack loop is now implemented. Martial classes correctly perform multiple attacks per action starting at level 5. |
| **Death Saving Throws** | ✅ **REAL** <br> ✨ **RECENTLY FIXED** | Full system implemented: Downed state, 3 saves, Nat 1/20 rules. Revival via healing works. |
| **On-Hit Trigger System** | ✅ **REAL** <br> ✨ **RECENTLY FIXED** | A new `OnHitTriggerService` has been added, enabling proper on-hit mechanics for Divine Smite, Hex, and GWM bonus attacks. |
| **Dodge Action** | ✅ **REAL** <br> ✨ **RECENTLY FIXED** | The `dodging` status now correctly imposes disadvantage on attackers. |
| **Threatened Condition** | ✅ **REAL** <br> ✨ **RECENTLY FIXED** | Ranged/spell attacks correctly get disadvantage when in melee range of a hostile. |
| **AI Actions** | ✅ **REAL** <br> ✨ **RECENTLY FIXED** | The AI can now correctly generate and execute Dash and Disengage actions in combat. |
| **Cantrip Scaling** | ✅ **REAL** <br> ✨ **RECENTLY FIXED** | Cantrips like Fire Bolt and Eldritch Blast now correctly scale in damage/beams at levels 5 and 11. |
| **Save Repeat System** | ✅ **REAL** <br> ✨ **RECENTLY FIXED** | Key statuses like Frightened and Paralyzed now correctly allow a new saving throw at the end of the character's turn. |
| **Height Advantage** | ❌ **MISSING** | AI scores it for positioning, but the rules engine has **no mechanic** to grant the +2/-2 attack bonus. |
| **Stealth/Hiding** | ❌ **MISSING** | No stealth vs. perception system. "Hide" is a status that grants advantage but doesn't involve detection. |

### B. Class-by-Class Breakdown

#### Fighter
| Mechanic | Verdict | Detail |
|---|---|---|
| Action Surge | ✅ **REAL** | `GrantActionEffect` correctly grants an additional action. |
| Extra Attack | ✅ **REAL** <br> ✨ **RECENTLY FIXED** | Fighters now correctly gain a second attack at level 5 and a third at level 11. |
| Battle Master Maneuvers | ✅ **REAL** <br> ✨ **RECENTLY FIXED** | Trip Attack, Riposte, and Menacing Attack are implemented and consume `superiority_dice`. |

#### Paladin
| Mechanic | Verdict | Detail |
|---|---|---|
| Divine Smite | ✅ **REAL** <br> ✨ **RECENTLY FIXED** | Now functions correctly as a toggleable, on-hit trigger ability, consuming a spell slot after a hit is confirmed. |
| Aura of Protection | ✅ **REAL** <br> ✨ **RECENTLY FIXED** | Now correctly applies the Paladin's CHA modifier as a save bonus to all allies within 10ft. |
| Lay on Hands | 🔧 **DATA-ONLY** | Functions as a generic heal, not the BG3 pool-based distribution mechanic. |

#### Barbarian
| Mechanic | Verdict | Detail |
|---|---|---|
| Rage | ✅ **REAL** <br> ✨ **RECENTLY FIXED** | Resistance to physical damage is functional. The **+2 damage bonus is now correctly applied**. |
| Reckless Attack | ✅ **REAL** <br> ✨ **RECENTLY FIXED** | Now works correctly: grants advantage to self, but also **grants advantage to enemies**, making it a true trade-off. |

#### Warlock
| Mechanic | Verdict | Detail |
|---|---|---|
| Agonizing Blast | ✅ **REAL** <br> ✨ **RECENTLY FIXED** | The `warlock_invocations.json` data is now used to correctly add the CHA modifier to Eldritch Blast damage. |
| Hex | ✅ **REAL** <br> ✨ **RECENTLY FIXED** | No longer a simple DoT. Now correctly functions as an on-hit trigger, adding 1d6 Necrotic damage to each attack against the target. |

#### Other Classes
- **Bard**: ⛔ **INACCURATE**. Inspiration is a flat `+4`, not a `d6/d8/d10` die. Cutting words is a flat `-4`. The core mechanic remains incorrect.
- **Cleric**: ⚠️ **PARTIAL**. Destructive Wrath was fixed and now correctly maximizes damage dice. However, Turn Undead still affects all enemies, not just Undead.
- **Druid**: ❌ **MISSING**. **Wild Shape remains the single biggest missing feature in the game.** The class is non-functional.
- **Ranger**: ❌ **MISSING**. **The entire class is a shell with no functional mechanics.**
- **Rogue**: ✅ **REAL**. Sneak Attack is perfectly implemented. AI can use Cunning Actions.
- **Sorcerer**: ⛔ **INACCURATE**. Metamagic is not a generic system, just manually-created variants of specific spells. This is a major fidelity gap.

---

## Part 3: Remaining Major Gaps & Fidelity Issues

This is a curated list of what is **still missing or inaccurate** after the latest round of fixes.

### Critical Gameplay Gaps
1.  **Wild Shape is Missing**: The Druid class has no identity without its core transformation mechanic.
2.  **Ranger Class is Empty**: The Ranger has no implemented class features.
3.  **Inaccurate Core Class Mechanics**:
    - **Bard's Inspiration** uses a flat bonus, not a scaling die (d6/d8/d10), which is fundamental to the class feel.
    - **Sorcerer's Metamagic** is not a system; it's a handful of hardcoded spell variants.
4.  **Height Advantage is Not Implemented**: The defining tactical layer of BG3 combat is missing.
5.  **Stealth & Detection System is Missing**: Core to Rogue gameplay and surprise rounds.

### High-Impact Fidelity Gaps
- **Spells**: The spellbook is still very small. Key spells like `Bless`, `Hold Person`, `Spirit Guardians`, `Spike Growth`, etc., are missing.
- **Racial Traits**: Unique racial abilities (Dragonborn breath, Halfling Lucky, Githyanki Psionics, etc.) are not functional.
- **Equipment System**: There is no system for equipping weapons, armor, or magic items that modify character stats or grant abilities.
- **Feats**: Many feats are still `DATA-ONLY` (e.g., Sentinel, Polearm Master, Mobile, Tavern Brawler). Feat prerequisites are not checked.
- **Subclasses**: Beyond the first few features, most subclass progression at later levels is not implemented.

### Specific Inaccurate Mechanics
- **Turn Undead**: Still affects all enemies, not just Undead.
- **Lay on Hands**: Still a generic heal, not a spendable HP pool.
- **Toll the Dead**: Deals 1d12 always, instead of 1d8 base / 1d12 if the target is injured.
- **Sleep**: A simple AoE status, not the HP-pool based mechanic from BG3.

---

## Appendix A: Code-Level Implementation Guide

For developers looking to contribute, this section provides key entry points into the codebase for addressing major gaps.

| Feature | Primary File & Line | Key Method / Field |
|---|---|---|
| **Ability Execution** | `Combat/Abilities/EffectPipeline.cs` | `ExecuteAbility()` |
| **Attack Rolls** | `Combat/Rules/RulesEngine.cs` | `RollAttack()` |
| **Damage Application**| `Combat/Abilities/Effects/Effect.cs` | `DealDamageEffect.Execute()` |
| **Status Management** | `Combat/Statuses/StatusSystem.cs` | `ApplyStatus()` |
| **AI Decisions** | `Combat/AI/AIDecisionPipeline.cs` | `MakeDecision()` |
| **Class Definitions** | `Data/CharacterModel/ClassDefinition.cs` | `LevelProgression` |
| **Ability JSON Data**| `Data/Abilities/bg3_mechanics_abilities.json` | N/A |
| **Status JSON Data** | `Data/Statuses/bg3_mechanics_statuses.json`| N/A |
