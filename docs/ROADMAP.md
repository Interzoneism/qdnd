# BG3 Combat Parity Roadmap (Source-Backed)

> Last updated: 2026-02-15
> Goal: full Baldur's Gate 3 combat parity first, then expand outward  
> Source policy: `bg3.wiki` behavior first, `BG3_Data/` as local implementation reference

---

## 1. Verified BG3 Ground Truth (Target Behavior)

This section is the contract. If implementation conflicts with this, implementation changes.

### 1.1 Turn flow and action economy

- Initiative uses a `1d4 + DEX modifier` roll.
- Initiative can be shared for adjacent allies, enabling flexible ordering inside that shared block.
- Core economy is `Action + Bonus Action + Movement + Reaction`.
- Off-hand attacks are bonus actions and do not require first taking the Attack action.
- Weapon set swapping is free (not a once-per-turn consumable action).
- BG3 does not implement D&D 5e's numeric cover bonus model (`+2/+5 AC and DEX saves`).
- High/low ground for ranged attacks uses hit modifiers (`+2` from high ground, `-2` from low ground), not generic advantage/disadvantage.

### 1.2 Death, downed, and survival rules

- At 0 HP, player-controlled characters enter `Downed` and roll death saving throws.
- Most non-player enemies die at 0 HP instead of entering full death-save flow.
- Player characters are not instantly killed by damage overflow in normal cases; downed flow applies (with known exceptions, e.g. some Wild Shape transitions).

### 1.3 Classing, progression, and spell ceiling

- BG3 has 12 classes.
- Current wiki state lists 58 subclasses total.
- Multiclassing has no D&D-style ability score prerequisites.
- Explorer disables multiclassing unless enabled in custom difficulty.
- Level cap is 12.
- Spellcasting cap is level 6 spells (no level 7-9 spells for player progression).

### 1.4 Tactical environment

- Difficult terrain halves movement speed (equivalent to 2x movement cost per meter).
- Difficult terrain from multiple sources does not stack.
- Surface interactions are core combat mechanics (e.g., Wet + Lightning, Wet + Cold, Grease + Fire, etc.).
- Weapon actions are a major BG3-specific layer and are tied to equipped weapon types/proficiency, with short-rest recharge patterns.

---

## 2. Current Repository Baseline (Measured, Not Assumed)

Values below were re-counted from local files in this repository on 2026-02-15.

### 2.1 `BG3_Data/` snapshot

- Spell entries across `BG3_Data/Spells/*.txt`: `1467`
- Status entries across `BG3_Data/Statuses/*.txt`: `1082`
- `ClassDescriptions.lsx`: `12` base classes + `24` subclasses (`36` class descriptions total)
- `FeatDescriptions.lsx`: `21` feat descriptions
- `Backgrounds.lsx`: `11` backgrounds
- `Stats/Weapon.txt`: `227` entries
- `Stats/Armor.txt`: `352` entries
- `Stats/Object.txt`: `926` entries
- `Stats/Passive.txt`: `418` entries
- Weapon unlock spell refs found in `Weapon.txt`: `64` refs, `22` unique spell IDs
- `Rulesets.lsx` includes Explorer/Balanced/Tactician difficulty entries and `RULESET_HONOUR`

### 2.2 Project-curated runtime data

- `Data/Actions/*.json`: `195` listed actions, `194` unique IDs
- `Data/Statuses/*.json`: `114` listed statuses, `108` unique IDs

### 2.3 Critical data drift callout

- Wiki parity target (58 subclasses) and local extract snapshot (`24` subclasses) are mismatched.
- Roadmap execution must treat this as a first-order risk: either update extraction data to current BG3 content or explicitly scope parity to a fixed game version.

---

## 3. Corrections Applied vs Prior Roadmap

- Removed D&D 5e cover bonus assumptions as default BG3 combat behavior.
- Corrected high/low ground handling from advantage/disadvantage to ranged hit modifiers.
- Corrected death handling assumptions around instant death for player characters.
- Replaced hand-written class feature checklists (high error risk) with data-driven parity tasks.
- Replaced guessed content volume numbers with measured local counts + explicit wiki/live parity delta.

---

## 4. Execution Plan
  
Order is dependency-driven: validation first, then gameplay/runtime wiring, then coverage hardening.

### Stream 1: Parity Gates First (CI and data integrity)

Goal: fail fast on invalid action links and forbidden gameplay scope.

- Expand parity validation to use union action IDs:
  - `Data/Actions` canonical IDs
  - BG3 ActionRegistry-loaded IDs from `BG3_Data`
- Enforce "no summon actions" policy for canonical parity scenario packs.
- Make duplicate action IDs fail CI unless explicitly and temporarily allowlisted.

Exit criteria:

- CI parity gate catches missing granted abilities against union registry.
- Canonical content fails if summon actions are granted/usable.

### Stream 2: BG3-Exact Build Inputs and Deterministic Replica Scenarios

Goal: make "exact BG3 replica" scenarios practical and testable.

- Add optional BG3 character-template reference in scenario units (from `BG3_Data/Stats/Character.txt`).
- Wire StatsRegistry/loader flow so template stats and passives can seed spawned combatants.
- Maintain deterministic "replica" scenario(s) with explicit actions/equipment/class levels.

Exit criteria:

- Replica scenarios load headless and produce deterministic combatant builds.

### Stream 3: Forced Movement Correctness

Goal: route push/pull/knockback through the real forced movement service (collision, surfaces, fall damage).

- Ensure `ForcedMovementService` is always registered in arena service wiring.
- Pass service through action execution context.
- Refactor forced-move effects to delegate to service (fallback only for isolated tests).

Exit criteria:

- Arena execution no longer uses teleport-style forced movement for shove/push effects.

### Stream 4: Toggleable Passives End-to-End

Goal: make toggle passives data-driven and visible in gameplay UX.

- Add runtime toggle-state tracking in `PassiveManager`.
- Execute toggle on/off functors when state changes.
- Surface toggle passives in action bar model/UI with clear on/off state.
- Handle passive toggle clicks without entering targeting mode.

Exit criteria:

- Toggle passives are visible, clickable, deterministic, and immediately reflected in behavior/UI.

### Stream 5: Shove and AoE UX Parity

Goal: remove conflicting shove definitions and tighten AoE legality feedback.

- Deduplicate shove action definitions across JSON packs.
- Ensure shove paths use forced movement and (if enabled) contested checks deterministically.
- Tighten AoE preview legality:
  - no target highlight when cast point is illegal/out-of-range.

Exit criteria:

- Single canonical shove behavior.
- AoE preview does not imply illegal casts are valid.

### Stream 6: Coverage and Runtime Policy Hardening

Goal: close the loop between data grants, runtime usability, and test coverage.

- Filter forbidden actions (summons) out of HUD/AI candidate lists for canonical mode.
- Emit coverage inventory in parity output:
  - granted actions present in Data/Actions,
  - granted actions present only via BG3 ActionRegistry,
  - missing from both.
- Keep both headless and full-fidelity autobattle suites as release gates.

Exit criteria:

- Coverage gaps are actionable from CI output.
- Forbidden actions are blocked at gameplay surfaces (HUD + AI).

### Cross-Cutting Foundational Tasks

- Keep provenance/version locking tasks active:
  - `docs/BG3_PARITY_CONTRACT.md`
  - `docs/BG3_DATA_PROVENANCE.md`
- Resolve local-vs-wiki content drift explicitly:
  - either refresh extraction to target version,
  - or scope parity to declared extracted version with documented exclusions.

---

## 5. Definition of Done (Combat Parity Milestone)

Combat parity milestone is reached when all are true:

1. Core BG3 combat rules in Section 1 are implemented and test-covered.
2. Class/progression behavior is generated from validated data, not ad hoc code.
3. Spell/status execution has no silent unsupported paths.
4. Forced movement uses service-backed collision/surface/fall handling in arena execution.
5. Toggleable passives are fully wired (data -> runtime state -> HUD -> deterministic effect changes).
6. Tactical environment interactions are deterministic and regression-tested.
7. Difficulty mode differences (including Honour-specific behavior) are explicit and validated.
8. CI parity gate blocks mismatched data/rules behavior and forbidden summon-action usage in canonical parity mode.

---

## 6. Sources

- https://bg3.wiki/wiki/Gameplay_mechanics
- https://bg3.wiki/wiki/Initiative
- https://bg3.wiki/wiki/Death_saving_throw
- https://bg3.wiki/wiki/Downed_(Condition)
- https://bg3.wiki/wiki/Classes
- https://bg3.wiki/wiki/Weapon_actions
- https://bg3.wiki/wiki/Difficult_Terrain
- https://bg3.wiki/wiki/Surfaces
- https://bg3.wiki/wiki/Difficulty
- https://bg3.wiki/wiki/Guide:D%26D_5e_rule_changes
