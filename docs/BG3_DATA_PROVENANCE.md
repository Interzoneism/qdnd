# BG3 Data Provenance and Integrity Tracking

> **Purpose**: Documents the source, format, and integrity status of all BG3 reference data in this repository.  
> **Audience**: Developers, data curators, CI maintainers.  
> **Last Updated**: 2026-02-15

---

## Executive Summary

This project uses unpacked BG3 game data as authoritative reference for D&D 5e combat mechanics. All source files are **read-only** and live in `BG3_Data/`. Runtime-curated data (simplified for gameplay) lives in `Data/`.

**Critical Gap**: Extraction date and BG3 patch version are currently undocumented. Future data refreshes MUST track provenance metadata.

---

## Data Source Table

### BG3_Data/ — Raw Game Extraction

All files unpacked from Baldur's Gate 3 `Shared.pak`.

| Path | Format | Count/Size | Content | Cosmetic Stripping |
|------|--------|------------|---------|-------------------|
| `BG3_Data/Spells/` | TXT (Larian Stats) | 1467 spell entries<br>8 files | All spell types: Target, Projectile, Shout, Zone, Rush, Teleportation, Throw, ProjectileStrike | Icons, animations, VFX, sounds removed |
| `BG3_Data/Statuses/` | TXT (Larian Stats) | 1082 status entries<br>11 files | BOOST, INCAPACITATED, POLYMORPHED, EFFECT, KNOCKED_DOWN, FEAR, DOWNED, HEAL, INVISIBLE, SNEAKING, DEACTIVATED | Visual/audio effects stripped |
| `BG3_Data/Stats/Character.txt` | TXT | 4416 lines | Base stat blocks: heroes, NPCs, monsters, racial/gender templates | Material references removed |
| `BG3_Data/Stats/Weapon.txt` | TXT | 227 weapon entries<br>1655 lines | All weapons with D&D damage, properties, proficiency, unlocked spells | Visual templates removed |
| `BG3_Data/Stats/Armor.txt` | TXT | 352 armor entries<br>1990 lines | AC, type, proficiency, ability mod caps, boosts | Material/skin data removed |
| `BG3_Data/Stats/Object.txt` | TXT | 926 object entries<br>6490 lines | Consumables, scrolls, poisons, grenades, camp supplies | Visual data removed |
| `BG3_Data/Stats/Passive.txt` | TXT | 418 passive entries<br>3042 lines | Class features, racial traits, feats, toggleable passives | Icons removed |
| `BG3_Data/Stats/Interrupt.txt` | TXT | 452 lines | Reaction system: Counterspell, AoO, Bardic Inspiration, etc. | Animations removed |
| `BG3_Data/Stats/Modifiers.txt` | TXT | 646 lines | SCHEMA: field definitions for all stat types | N/A (data schema) |
| `BG3_Data/Stats/Equipment.txt` | TXT | 3811 lines | Starting equipment sets for character creation | Cosmetics removed |
| `BG3_Data/Stats/ItemTypes.txt` | TXT | 28 lines | Item category mappings | N/A |
| `BG3_Data/Stats/Data.txt` | TXT | 648 lines | Level scaling tables, formula lookup values | N/A |
| `BG3_Data/Stats/XPData.txt` | TXT | 12 lines | XP thresholds (stops at level 5) | N/A |
| `BG3_Data/Stats/CriticalHitTypes.txt` | TXT | 16 lines | Critical hit type definitions | N/A |
| `BG3_Data/ClassDescriptions.lsx` | LSX (Larian XML) | 601 lines | 12 base classes + 24 subclasses (36 total) | Character creation pose data removed (~88 attributes) |
| `BG3_Data/Progressions.lsx` | LSX | 2506 lines | Per-level progression tables (boosts, passives, selectors) | N/A |
| `BG3_Data/ProgressionDescriptions.lsx` | LSX | 2028 lines | Display text for progressions | TranslatedString handles (localization keys) |
| `BG3_Data/ActionResourceDefinitions.lsx` | LSX | 277 lines | Resource types: ActionPoint, SpellSlot, Rage, Ki, etc. | N/A |
| `BG3_Data/ActionResourceGroupDefinitions.lsx` | LSX | 15 lines | Resource groupings (SpellSlotsGroup, etc.) | N/A |
| `BG3_Data/Backgrounds.lsx` | LSX | 131 lines | 11 character backgrounds | N/A |
| `BG3_Data/Races.lsx` | LSX | 5619 lines<br>(originally 30,219) | Race/subrace hierarchy, tags | ~24,600 lines removed: skin/eye/hair colors, palettes, excluded gods, race equipment, sound switches |
| `BG3_Data/FeatDescriptions.lsx` | LSX | 157 lines | 21 feat descriptions | N/A |
| `BG3_Data/DifficultyClasses.lsx` | LSX | 160 lines | Named DC values (Easy=7, Medium=10, etc.) | N/A |
| `BG3_Data/Rulesets.lsx` | LSX | 62 lines | Difficulty presets: Explorer, Balanced, Tactician, Honour | N/A |
| `BG3_Data/RulesetModifiers.lsx` | LSX | 248 lines | Adjustable difficulty parameters | N/A |
| `BG3_Data/RulesetValues.lsx` | LSX | 244 lines | Concrete values per difficulty preset | N/A |

**Total Cosmetic Data Stripped**: ~31,000 lines (icons, animations, VFX, sounds, materials, character creation cosmetics)

**Stripped Fields** (from TXT files):
- Icons: `Icon`
- Animations: `SpellAnimation`, `DualWieldingSpellAnimation`, `HitAnimationType`, `CastAnimation`, `StatusAnimation`, etc.
- VFX: `PrepareEffect`, `CastEffect`, `TargetEffect`, `HitEffect`, `StatusEffect`, etc.
- Audio: `PrepareSound`, `CastSound`, `SoundLoop`, etc.
- Models: `RootTemplate`, `Projectile`, `StatusMaterial`, `Material`

**Stripped Nodes** (from LSX files):
- `Races.lsx`: Color palette nodes (`SkinColors`, `EyeColors`, `HairColors`, etc.), `ExcludedGods`, `RaceEquipment`
- `ClassDescriptions.lsx`: `CharacterCreationPose`, `SoundClassType`, UI hotbar column specs

**See**: [BG3_Data/README.md](../BG3_Data/README.md) for full extraction documentation and format specifications.

---

### Data/ — Runtime-Curated Data

Simplified, gameplay-focused data derived from `BG3_Data/` or hand-authored for specific scenarios.

| Path | Format | Count | Content | Derivation |
|------|--------|-------|---------|-----------|
| `Data/Actions/*.json` | JSON | 195 files, 194 unique IDs | Canonical runtime actions | Curated subset + custom actions |
| `Data/Statuses/*.json` | JSON | 114 files, 108 unique IDs | Canonical runtime statuses | Curated subset from BG3 statuses |
| `Data/Scenarios/*.json` | JSON | Multiple | Pre-built combat scenarios | Hand-authored for testing/gameplay |
| `Data/Races/*.json` | JSON | (count TBD) | Racial trait data | Simplified from Progressions + Races.lsx |
| `Data/Classes/*.json` | JSON | (count TBD) | Class progression data | Simplified from ClassDescriptions + Progressions |
| `Data/Feats/*.json` | JSON | (count TBD) | Feat definitions | Simplified from FeatDescriptions + Passive.txt |
| `Data/Spells/*.json` | JSON | (count TBD) | Spell definitions | Simplified from BG3_Data/Spells/*.txt |
| `Data/Passives/*.json` | JSON | (count TBD) | Passive ability data | Simplified from Passive.txt |
| `Data/Validation/parity_allowlist.json` | JSON | N/A | Known parity gaps (temporary) | Hand-maintained, reviewed in CI |

**Integrity Policy**:
- Runtime data MUST NOT contradict BG3_Data unless explicitly documented in parity allowlist
- Divergence for gameplay simplification is allowed but must be justified

---

## Extraction Methodology

### Source

- **Game**: Baldur's Gate 3
- **Archive**: `Shared.pak` (unpacked using community tools)
- **Extraction Date**: ⚠️ **Undocumented** — recommend tracking in future refreshes
- **Target Patch**: ⚠️ **Undocumented** — recommend tagging extraction with BG3 version

### Cleaning Process

1. **Cosmetic Stripping**:
   - Removed ~31,000 lines of visual/audio data
   - Each cleaned file includes header comment listing removed fields
   - Cleaned files retain 100% mechanical gameplay data

2. **Format Preservation**:
   - LSX: Larian's typed XML format (GUIDs, enums, cross-references preserved)
   - TXT: Larian's stats format with inheritance (`using "ParentName"`)

3. **Validation**:
   - Cross-references verified (UUIDs, spell names, status names)
   - Inheritance chains validated (no broken `using` links)

### Exclusion Policy

**Deliberately Omitted**:
- Character creation cosmetics (skin tones, hairstyles, etc.)
- Visual effect templates and animation references
- Sound event data
- Material/shader references
- UI icon paths
- Localization string contents (TranslatedString handles retained for ID tracking)

**Rationale**: Focus on mechanical combat data only; visual/audio data can be sourced separately if needed for full game recreation.

---

## Known Data Drift

### Local vs. Live Game

| Category | Local Snapshot | Wiki/Live Game | Status |
|----------|---------------|----------------|--------|
| Subclasses | 24 | 58 | ⚠️ **Significant gap** — extraction predates some subclass additions |
| Spell Count | 1467 | Unknown | Unknown — requires wiki enumeration |
| Status Count | 1082 | Unknown | Unknown — requires wiki enumeration |
| XP Table | Level 1-5 | Level 1-12 | ⚠️ **Incomplete** — levels 6-12 missing in XPData.txt |
| Weapon Actions | 64 refs, 22 unique IDs | Unknown | Unknown — wiki weapon action enumeration needed |

### Local vs. Wiki Behavior

| Mechanic | Local Data | Wiki Documented | Resolution |
|----------|-----------|-----------------|------------|
| High/low ground | Hit modifiers (+2/-2) | Same | ✅ Aligned |
| Cover bonuses | Not implemented | Not in BG3 | ✅ Correctly excluded |
| Death at 0 HP | Downed for PCs, death for NPCs | Same | ✅ Aligned |
| Multiclass prerequisites | None | None in BG3 | ✅ Aligned |
| Off-hand attack timing | Bonus action, no Attack action required | Same | ✅ Aligned |

**Action Required**:
- Compare local spell/status/action counts against wiki enumeration
- Document any missing content from wiki that exists in live game but not in extract
- Plan data refresh cycle to target specific BG3 patch versions

---

## Integrity Checks

### Automated Validation (CI)

**Location**: `Tests/Unit/ParityValidationTests.cs`  
**Script**: `./scripts/ci-parity-validate.sh`

**Checks Performed**:
1. **Duplicate IDs**: No duplicate action/status/class/feat/race IDs across all loaded data
2. **Cross-Reference Integrity**:
   - Granted actions exist in union registry (Data/Actions + BG3 ActionRegistry)
   - Applied statuses exist in union registry (Data/Statuses + BG3 StatusRegistry)
   - Passive unlock refs target valid spells/interrupts
3. **Effect Handler Coverage**: All action effect types registered in `EffectPipeline`
4. **JSON Schema Compliance**: All JSON data deserializes to typed models without errors
5. **Forbidden Content**: Summon actions are NOT granted in canonical parity scenarios

### Manual Verification Checklist

- [ ] Extraction date and BG3 patch version documented
- [ ] Cosmetic stripping header comments present in all cleaned files
- [ ] Cross-reference UUIDs validated between LSX files
- [ ] Inheritance chains tested (no missing `using` targets)
- [ ] Wiki behavioral cross-check for new/changed mechanics
- [ ] Subclass count drift assessed and documented
- [ ] XP table extended or extrapolated for levels 6-12

---

## Data Refresh Policy

### Triggers for Refresh

1. **Major BG3 Patches**: When Larian releases balance/content patches affecting combat
2. **Discovered Drift**: When wiki or live game behavior contradicts local data
3. **Scope Expansion**: When implementing new systems that require additional BG3 data
4. **Quarterly Review**: Scheduled assessment of wiki parity delta

### Refresh Procedure

1. **Pre-Refresh**:
   - Document current extraction metadata (date, patch, commit hash)
   - Archive current `BG3_Data/` directory with version tag
   - Run CI parity gate and record baseline metrics

2. **Extraction**:
   - Unpack target `Shared.pak` from specific BG3 patch version
   - Apply cosmetic stripping using documented field list
   - Add/update header comments with removal details

3. **Validation**:
   - Run automated integrity checks
   - Compare counts (classes, spells, statuses) against previous baseline
   - Document new/changed/removed entries

4. **Integration**:
   - Update `BG3_Data/README.md` with new extraction metadata
   - Update this provenance document with new counts
   - Re-run CI parity gate; address new failures
   - Update parity allowlist if necessary (with justification)

5. **Documentation**:
   - Update `BG3_PARITY_CONTRACT.md` with scope changes
   - Update `ROADMAP.md` with new measured counts
   - Tag repository with extraction version (e.g., `bg3-data-patch-6`)

---

## Derived Data Integrity

### ActionRegistry Population

**Source Hierarchy**:
1. `Data/Actions/*.json` (curated runtime — highest priority)
2. `BG3_Data/Spells/*.txt` parsed via `BG3DataLoader` (reference)
3. `BG3_Data/Stats/Weapon.txt` unlock refs (weapon actions)

**Duplicate Resolution**: Runtime (Data/Actions) overrides BG3 parsed data if ID collision occurs.

### StatusRegistry Population

**Source Hierarchy**:
1. `Data/Statuses/*.json` (curated runtime — highest priority)
2. `BG3_Data/Statuses/*.txt` parsed via `BG3DataLoader` (reference)

**Duplicate Resolution**: Runtime (Data/Statuses) overrides BG3 parsed data if ID collision occurs.

### ClassRegistry Population

**Source**:
- `BG3_Data/ClassDescriptions.lsx` + `BG3_Data/Progressions.lsx`
- Linked via `ProgressionTableUUID` ↔ `TableUUID`

**Integrity Requirements**:
- All `ProgressionTableUUID` refs must resolve
- All `PassivesAdded` refs must exist in Passive.txt or Passives registry
- All `Boosts` action resource refs must exist in ActionResourceDefinitions.lsx

---

## Recommendations for Future Work

### Immediate (High Priority)

1. **Document Extraction Metadata**:
   - Add extraction date to `BG3_Data/README.md`
   - Tag extraction with BG3 patch version (e.g., "Patch 6", "Hotfix 7")
   - Record extraction tool name/version if applicable

2. **Resolve Subclass Drift**:
   - Either refresh data to capture 58 wiki-documented subclasses
   - OR explicitly scope parity to 24-subclass snapshot with documented exclusions

3. **Extend XP Data**:
   - Source levels 6-12 XP thresholds from wiki or live game
   - Add to `BG3_Data/Stats/XPData.txt` or `Data/XPData.json`

### Medium Term

4. **Wiki Content Enumeration**:
   - Count total spells, statuses, actions on bg3.wiki
   - Compare against local snapshot for gap analysis

5. **Automate Integrity Checks**:
   - Add pre-commit hook for Data/ JSON schema validation
   - Add CI check for BG3_Data/ cross-reference integrity

6. **Version Control for Extractions**:
   - Use Git LFS or separate data repo for BG3_Data/ versioning
   - Tag each extraction with BG3 patch metadata

### Long Term

7. **Differential Data Updates**:
   - Implement tooling to diff two BG3_Data/ snapshots
   - Generate changelog of mechanical changes between patches

8. **Parity Coverage Dashboard**:
   - Visualize % coverage for classes, spells, statuses, weapon actions
   - Track wiki drift over time

---

## References

- [BG3_Data/README.md](../BG3_Data/README.md) — Extraction details and file format specifications
- [docs/BG3_PARITY_CONTRACT.md](BG3_PARITY_CONTRACT.md) — Scope and enforcement rules
- [docs/ROADMAP.md](ROADMAP.md) — Measured baseline and execution plan
- [docs/parity-validation.md](parity-validation.md) — CI gate implementation
- https://bg3.wiki — Primary behavioral reference and content enumeration
