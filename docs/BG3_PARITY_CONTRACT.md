# BG3 Parity Contract

> **Purpose**: Defines the exact version, scope, and enforcement rules for Baldur's Gate 3 combat parity in this project.  
> **Status**: Living document — must be updated when data extraction or scope changes.  
> **Last Updated**: 2026-02-15

---

## Version Lock

### Data Extraction Source

- **Source**: Baldur's Gate 3 `Shared.pak` (unpacked)
- **Target Version**: Undocumented  
  ⚠️ **Action Required**: Extraction date and BG3 patch version should be documented in future data refreshes
- **Cosmetic Stripping**: ~31,000 lines of visual/audio data removed (icons, animations, VFX, sounds, materials)
- **See**: [BG3_Data/README.md](../BG3_Data/README.md) for extraction methodology

### Wiki Reference Source

- **Primary Behavioral Source**: https://bg3.wiki
- **Policy**: When local `BG3_Data/` conflicts with wiki-documented BG3 behavior, wiki behavior is the target
- **Drift Resolution**: Local data serves as implementation reference; discrepancies must be documented as known limitations

---

## Scope Statement

### Character Progression

- **Classes**: 12 base classes
- **Subclasses**: Local extract contains 24 subclasses (wiki documents 58 — see Known Limitations)
- **Level Cap**: 12
- **Multiclassing**: Enabled with **no ability score prerequisites** (BG3-specific deviation from D&D 5e)
- **Difficulty Modes**: Explorer mode disables multiclassing by default unless custom difficulty settings override

### Spellcasting

- **Max Spell Level**: 6
- **Excluded Content**: No level 7-9 spells in player progression
- **Spell Slot Mechanics**: Full/half/third caster progression as per BG3 implementation
- **Spell Preparation**: Classes with `MustPrepareSpells=true` (Cleric, Druid, Paladin, Wizard) require preparation
- **Wizard Learning**: Scroll-based spell learning enabled for Wizard class

### Action Economy

- **Core Actions**: Action + Bonus Action + Movement + Reaction per turn
- **Off-Hand Attacks**: Bonus action (does NOT require taking Attack action first — BG3-specific)
- **Weapon Set Swapping**: Free action (unlimited per turn)
- **Initiative**: `1d4 + DEX modifier` with optional shared initiative for adjacent allies

### Combat Mechanics

#### High/Low Ground
- **Implementation**: Ranged attack hit modifiers (`+2` high ground, `-2` low ground)
- **NOT D&D 5e**: Does NOT use advantage/disadvantage for elevation

#### Cover
- **Implementation**: BG3 does NOT implement D&D 5e cover bonuses (`+2/+5 AC and DEX saves`)
- **Current Status**: Cover system is out of scope for initial parity milestone

#### Death and Survival
- **Player Characters**: Enter `Downed` state at 0 HP, roll death saving throws
- **NPCs/Enemies**: Most die immediately at 0 HP (no death saves)
- **Damage Overflow**: Player characters are NOT instantly killed by overflow damage in normal cases (Downed flow applies)
- **Known Exceptions**: Some Wild Shape transitions and specific status effects may bypass Downed

#### Tactical Environment
- **Difficult Terrain**: Halves movement speed (2x cost per meter)
  - Multiple difficult terrain sources do NOT stack
- **Surfaces**: Core combat mechanic (Wet + Lightning, Grease + Fire, etc.)
  - Surface interactions are deterministic and required for parity
- **Forced Movement**: Push/pull/knockback must use collision detection, surface triggering, and fall damage

### Weapon Systems

- **Weapon Actions**: BG3-specific weapon abilities tied to equipped weapon type and proficiency
- **Recharge Pattern**: Most weapon actions recharge on short rest
- **Unlock Mechanism**: Granted via weapon proficiency and equipment (see `Stats/Weapon.txt` `BoostsOnEquipMainHand`)

---

## Known Limitations

### Content Gaps

1. **Subclass Count Mismatch**:
   - Local extract: 24 subclasses
   - Wiki current state: 58 subclasses
   - **Resolution**: Parity scope is limited to extracted 24 subclasses until data refresh
   - **Affected Classes**: Some classes missing subclass variants documented on wiki

2. **Summon Actions**:
   - **Status**: Forbidden in canonical parity scenarios
   - **Reason**: Incomplete summoned-actor lifecycle and targeting implementation
   - **Enforcement**: CI parity gate + HUD/AI filtering in canonical mode

3. **Data Freshness**:
   - Extraction may not match latest BG3 game patches
   - Some wiki-documented mechanics may reflect post-extraction balance changes
   - **Mitigation**: Document discrepancies as temporary drift; plan data refresh cycles

### Known Data Discrepancies

- `XPData.txt` stops at level 5, while progression tables extend to level 12
  - **Impact**: XP thresholds above level 5 must be extrapolated or sourced from wiki
- Some optional rules (e.g., multiclass spell slot calculations) may need wiki cross-reference

---

## Enforcement

### CI Parity Gate

**Location**: `Tests/Unit/ParityValidationTests.cs`  
**Gate Script**: `./scripts/ci-parity-validate.sh`

**Validation Rules**:
1. **Duplicate ID Detection**: Fail if duplicate action/status/class/feat IDs exist across loaded data
2. **Granted Action Links**: Fail if race/class/feat grants reference actions not in union registry:
   - `Data/Actions/*.json` (canonical runtime)
   - BG3 `ActionRegistry` loaded from `BG3_Data/`
3. **Action/Status Link Integrity**: Fail if actions reference statuses that don't exist
4. **Effect Handler Coverage**: Fail if action effect types lack registered handlers in `EffectPipeline`
5. **Forbidden Content**: Fail if summon actions are granted/usable in canonical parity scenarios

**Allowlist Policy**:
- Known legacy gaps tracked in `Data/Validation/parity_allowlist.json`
- New gaps MUST fail CI unless explicitly justified and documented
- Resolved gaps MUST be removed from allowlist

### Gameplay Filtering

**HUD Enforcement**:
- Forbidden actions (summons) excluded from player action bar in canonical mode
- Toggle passives presented without targeting mode

**AI Enforcement**:
- Forbidden actions excluded from AI candidate lists in canonical mode
- AI action selection must respect action economy and resource costs

### Test Coverage Requirements

**Headless Test Suite**:
- `./scripts/run_headless_tests.sh` — Service/registry/scenario validation
- Must pass for all PR merges

**Auto-Battle Verification**:
- `./scripts/run_autobattle.sh` — Real CombatArena.tscn with AI players
- Stress-test with multiple seeds to expose state machine bugs
- See [AGENTS-AUTOBATTLE-DEBUG.md](AGENTS-AUTOBATTLE-DEBUG.md)

**Full-Fidelity Testing**:
- `./scripts/run_autobattle.sh --full-fidelity` — Complete game simulation with HUD
- UI-aware AI playing like a human player
- See [AGENTS-FULL-FIDELITY-TESTING.md](AGENTS-FULL-FIDELITY-TESTING.md)

---

## Parity Milestone Definition

Combat parity milestone is complete when ALL are true:

1. ✅ Core BG3 combat rules (action economy, death, initiative) implemented and test-covered
2. ✅ Class/progression behavior generated from validated data (not ad hoc code)
3. ✅ Spell/status execution has no silent unsupported paths
4. ✅ Forced movement uses service-backed collision/surface/fall handling in arena
5. ✅ Toggleable passives fully wired (data → runtime state → HUD → deterministic effects)
6. ✅ Tactical environment interactions (surfaces, difficult terrain) deterministic and regression-tested
7. ✅ Difficulty mode differences (Explorer/Balanced/Tactician/Honour) explicit and validated
8. ✅ CI parity gate blocks mismatched data/rules and forbidden summon actions in canonical mode

---

## Update Policy

**Trigger for updates**:
- BG3 data extraction refresh
- Scope expansion (new systems, classes, spells)
- Discovered behavioral discrepancies between implementation and wiki/live game

**Review Cadence**:
- Quarterly review of wiki parity delta
- Post-major-BG3-patch assessment

**Ownership**:
- Data integrity: Development team
- Wiki cross-reference: QA/Combat design
- CI gate maintenance: Infrastructure team

---

## References

- [BG3_Data/README.md](../BG3_Data/README.md) — Extraction methodology and file formats
- [docs/BG3_DATA_PROVENANCE.md](BG3_DATA_PROVENANCE.md) — Source tracking and integrity
- [docs/ROADMAP.md](ROADMAP.md) — Execution plan and measured baseline
- [docs/parity-validation.md](parity-validation.md) — CI gate implementation details
- https://bg3.wiki — Primary behavioral reference
