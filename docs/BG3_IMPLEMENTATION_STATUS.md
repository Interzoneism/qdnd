# BG3 Implementation Status — The Single Source of Truth

> This document is the authoritative source of truth for the QDND project's Baldur's Gate 3 mechanics implementation. It is generated from a full codebase analysis and consolidates findings from all previous audits and progress reports. This document reflects the **current state of the game** after the latest fixes.

---

## Part 1: High-Level Class Implementation Scorecard

This table provides a quick-glance summary of the **current** implementation status for each class's core mechanics.

| Class | Overall Status | Best Implemented Mechanic | Worst Implementation Gap |
|---|---|---|---|
| **Barbarian** | ✅ **Functional** | Rage (Resistance + Damage Bonus) | Most subclass features still missing. |
| **Bard** | ✅ **Functional** <br> ✨ **RECENTLY FIXED** | Bardic Inspiration now uses proper dice rolls (d6/d8/d10). | Most subclass features still missing (Swords, Glamour, etc.). |
| **Cleric** | ✅ **Functional** | Turn Undead (targets only undead), Destructive Wrath. | Most subclass domain features still missing. |
| **Druid** | ✅ **Functional** <br> ✨ **RECENTLY FIXED** | Wild Shape transformation with multiple beast forms. | Elemental forms (Circle of Moon level 10) not implemented. |
| **Fighter** | ✅ **Functional** | Action Surge, Extra Attack, Battle Master Maneuvers. | Most subclass features (Arcane Archer, Eldritch Knight) are missing. |
| **Monk** | ⚠️ **Partial** | Ki system and Stunning Strike work. | Patient Defence (Dodge) is inaccurate; subclass features missing. |
| **Paladin** | ✅ **Functional** | Divine Smite (on-hit trigger), Aura of Protection. | Lay on Hands is a generic heal, not pool-based. |
| **Ranger** | ✅ **Functional** <br> ✨ **RECENTLY FIXED** | Hunter's Mark, Ensnaring Strike, Hail of Thorns, Extra Attack. | Beast Master companion AI not implemented. |
| **Rogue** | ✅ **Functional** | Sneak Attack (fully implemented). | Uncanny Dodge / Evasion still missing. |
| **Sorcerer**| ✅ **Functional** <br> ✨ **RECENTLY FIXED** | Generic Metamagic system via AbilityVariants (Quickened, Twinned). | Some advanced metamagic options (Subtle, Heightened) not yet added. |
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
| **Dice Modifiers** | ✅ **REAL** <br> ✨ **RECENTLY FIXED** | A new `ModifierType.Dice` system allows modifiers to roll dice when applied (e.g., Bardic Inspiration adds 1d8). Modifiers can be marked as `consume-on-use` to remove them after first application. |
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
- **Bard**: ✅ **FUNCTIONAL** <br> ✨ **RECENTLY FIXED**. Inspiration now uses proper dice rolls (`d6/d8/d10`) instead of flat bonuses. Cutting Words similarly uses negative dice. The core mechanic is now accurate to D&D 5e/BG3.
- **Cleric**: ✅ **FUNCTIONAL**. Destructive Wrath correctly maximizes damage dice. Turn Undead now correctly targets only undead creatures using the new tag-based filtering system.
- **Druid**: ✅ **FUNCTIONAL** <br> ✨ **RECENTLY FIXED**. Wild Shape now fully implemented with 8 beast forms (Wolf, Bear, Dire Wolf, Giant Spider, Panther, Badger, Polar Bear, Sabre-Toothed Tiger). Transformation correctly swaps physical stats, grants temp HP, and provides beast abilities.
- **Ranger**: ✅ **FUNCTIONAL** <br> ✨ **RECENTLY FIXED**. Hunter's Mark, Ensnaring Strike, Hail of Thorns, and Colossus Slayer are implemented. Extra Attack at level 5. Favoured Enemy and Natural Explorer provide passive bonuses.
- **Rogue**: ✅ **REAL**. Sneak Attack is perfectly implemented. AI can use Cunning Actions.
- **Sorcerer**: ✅ **FUNCTIONAL** <br> ✨ **RECENTLY FIXED**. Metamagic now uses the generic `AbilityVariant` system. Quickened Spell and Twinned Spell are implemented as spell variants that modify action cost and targeting.

---

## Part 3: Remaining Major Gaps & Lower Priority Items

This is a curated list of what is **still missing** after the latest round of fixes. Most critical gaps have been addressed.

### Remaining High-Impact Items
1. **Height Advantage**: The +2/-2 attack bonus from elevation is not implemented in the rules engine.
2. **Full Stealth/Detection System**: Hide status grants advantage but no stealth vs. perception contest.
3. **Equipment System**: No system for equipping weapons, armor, or magic items that modify stats.

### Medium Priority Items
- **Lay on Hands**: Still a generic heal, not the pool-based mechanic.
- **More Metamagic Options**: Subtle Spell, Empowered Spell, Heightened Spell not yet added.
- **Subclass Progression**: Most subclass features beyond level 3-6 are not implemented.
- **More Feats**: Many feats have data but the runtime mechanics are tags without code hooks.

### Lower Priority / Nice-to-Have
- **Racial Traits**: Most are tagged but Dragonborn breath weapons are now functional. Halfling Lucky reroll is implemented.
- **More Spells**: Core spells like Bless, Hold Person, Spirit Guardians, Spike Growth, Sleep are now implemented. More spells can always be added.
- **Feat Prerequisites**: The system doesn't check prerequisites before allowing feat selection.

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
