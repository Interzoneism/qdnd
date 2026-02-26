# BG3 Combat Log — What It Writes Out

Research findings from the unpacked BG3 game data, specifically the XAML UI definitions and stat files.

---

## Combat Log Entry Types (Parameter Types)

These are the **typed parameter categories** that appear in combat log text, each with distinct color coding. Found in [Tooltips.xaml](file:///c:/Users/Martin/Downloads/bg3-modders-multitool/UnpackedMods/Game/Mods/MainUI/GUI/Library/Tooltips.xaml#L48-L286):

| Type | Sub-Type | Color Resource | Purpose |
|---|---|---|---|
| `Character` | `Enemy` | `RelationType.Enemy` | Names of hostile creatures |
| `Character` | `Ally` | `RelationType.Ally` | Names of friendly characters |
| `Damage` | `None` | `DamageType.None` | Typeless damage |
| `Damage` | `Slashing` | `DamageType.Physical` | Physical damage types |
| `Damage` | `Piercing` | `DamageType.Physical` | Physical damage types |
| `Damage` | `Bludgeoning` | `DamageType.Physical` | Physical damage types |
| `Damage` | `Force` | `DamageType.Force` | Force damage |
| `Damage` | `Psychic` | `DamageType.Psychic` | Psychic damage |
| `Damage` | `Thunder` | `DamageType.Thunder` | Thunder damage |
| `Damage` | `Lightning` | `DamageType.Lightning` | Lightning damage |
| `Damage` | `Cold` | `DamageType.Cold` | Cold damage |
| `Damage` | `Necrotic` | `DamageType.Necrotic` | Necrotic damage |
| `Damage` | `Poison` | `DamageType.Poison` | Poison damage |
| `Damage` | `Acid` | `DamageType.Acid` | Acid damage |
| `Damage` | `Radiant` | `DamageType.Radiant` | Radiant damage |
| `Damage` | `Fire` | `DamageType.Fire` | Fire damage |
| `HealAmount` | — | `DamageType.HealAmount` | HP healed |
| `Spell` | — | `CombatLog.Spell` | Spell/ability names |
| `Status` | — | `CombatLog.Status` | Status effects applied/removed |
| `Passive` | — | `CombatLog.Status` | Passive ability triggers |
| `Surface` | — | (has Surface tooltip) | Surface interactions (fire, ice, etc.) |
| `Experience` | — | `LS_ExperienceTextColor` | XP gained |
| `ExperienceOverflow` | — | `LS_ExperienceOverflowTextColor` | XP at max level |
| `Round` | — | `LS_accent00TxtColor` | "Round X" markers |
| `Distance` | — | (unit-converted) | Distance values (e.g. movement, range) |
| `Weight` | — | (unit-converted) | Weight values |

---

## Combat Log View-Models (Entry Types)

Each of these is a distinct **type of log entry** with its own data template and localized string. Found in [Tooltips.xaml](file:///c:/Users/Martin/Downloads/bg3-modders-multitool/UnpackedMods/Game/Mods/MainUI/GUI/Library/Tooltips.xaml#L323-L415):

### Generic Entry (`CombatLogGenericTemplate`)
The fallback for any entry that has a `CtxTransText` (contextual translated string). Used for **most combat messages** including:
- Attack rolls (hit/miss)
- Damage dealt
- Saving throws (success/failure)
- Concentration saves
- Spell failures / immunities
- Death saves

### Equipment Status Entry (`VMEquipStatusEntry`)
Logs when a **status effect is applied via equipment**. Parameters:
- `StatusParam` — the status/condition
- `ItemParam` — the item that granted it

### Ping Entry (`VMPingEntry`)
Logs when a player **pings** a location or target in multiplayer. Parameters:
- `PingSourceParam` — who pinged
- `PingTargetParam` — what was pinged (optional)

### Dialog Start Entry (`VMDialogStartEntry`)
Logs when a **conversation begins**. Parameters:
- `DialogSpeakerParam` — who initiated the conversation
- `DialogTargetParam` — who they're talking to (optional)

### Stealth Spotted Entry (`VMStealthSpottedEntry`)
Logs when a stealthy character is **spotted**. Parameters:
- `SpottedParam` — who was spotted
- `SpotterParam` — who spotted them
- `StatusParam` or `PassiveParam` — by what means (e.g. perception passive)
- `ObscuredState` — `LightlyObscured` variant changes the message

---

## Tooltip Drill-Down Detail

When you hover over a combat log entry, it can show **expanded tooltip detail**:

### Regular Tooltips (`CombatLog.Tooltip`)
Shows a list of `CombatLogTooltipEntry` items, each with:
- **Type** `Dice` → shows a dice icon
- **Type** `ArmorClass` → shows an AC shield icon
- **Type** `DiceDescription` → styled in secondary color (e.g. "1d20")
- **Type** `ExpressionVariable` — styled in secondary color

### Rolls Tooltips (`CombatLog.RollsTooltip`)
Shows individual dice rolls with:
- `DiceTypeSet.DiceType` — the die used (d20, d8, etc.)
- `RolledNumber` — the actual number rolled
- `RerollType` — `Old` (struck through) or `New` (reroll icon) for things like Lucky or Halfling rerolls

### Damage Tooltips (`VMContextTransStringDamageParam`)
Shows damage breakdowns in the tooltip with a list of `CombatLogTooltipEntry` items (e.g. base damage, modifiers, critical hit bonus, resistances).

---

## Data Model Summary

The core data model for the combat log:

```
DCCombatLog
├── EntryGroups : Collection<CombatLogGroupedEntries>     (newest first in feed)
├── EntryGroupsReversed : Collection<CombatLogGroupedEntries> (oldest first for journal)
└── CombatFeedMessagesPredicate : filter for feed vs full log

CombatLogGroupedEntries
├── Entries[0]         — the primary entry (view-model above)
├── CtxTransText       — contextual translated text (for generic entries)
├── TooltipEntries     — expanded detail on hover
├── ParamTooltips      — parameter-level tooltips
├── Rolls              — dice roll data
└── ChildDepth         — indentation level (0 = top-level, >0 = sub-entry)
```

---

## Status Property Flag: `DisableCombatlog`

Statuses can opt out of the combat log entirely via the flag `DisableCombatlog` in their stat definition. This is used for internal/cosmetic statuses that shouldn't clutter the log (e.g. visual effects, internal bookkeeping buffs). Found across many status types: `BOOST`, `EFFECT`, `POLYMORPHED`, `INVISIBLE`, `INCAPACITATED`, `DOWNED`.

Other related flags: `DisableOverhead` (no floating text), `DisablePortraitIndicator` (no portrait icon).

---

## UI Behavior

| Feature | Detail |
|---|---|
| **Feed mode** | Right-aligned floating text, entries fade out after ~5 seconds |
| **Expanded log** | Scrollable panel, 3 sizes: Small (200px), Expanded (400px), SuperExpanded (800px) |
| **Journal view** | Full-page view in the journal, entries shown in reverse chronological order |
| **Auto-scroll** | Locks to bottom by default, unlocks when user scrolls up or hovers |
| **Splitscreen** | Shows one log per player, hides during radial menu, special handling for shared initiative |
| **Entry grouping** | Related entries are grouped (e.g. attack + damage as parent/child via `ChildDepth`) |
| **Colored text** | Characters, damage types, spells, statuses, surfaces — all color-coded |
| **Tooltips** | Hoverable entries show dice breakdowns, AC values, damage calculations |
| **Pinnable tooltips** | Rolls tooltips can be pinned for reference |

---

## What to Implement for Your Game

Based on the BG3 system, a combat log should output these categories of messages:

1. **Attack rolls** — "[Attacker] attacks [Target]: Roll (d20 + modifiers) vs AC → Hit/Miss"
2. **Damage dealt** — "[X] damage (type)" with color-coded damage type
3. **Healing** — "[X] HP healed" in healing color
4. **Saving throws** — "[Target] makes [Ability] save: Roll vs DC → Success/Failure"
5. **Concentration saves** — Specific saving throw variant
6. **Status effects** — "[Target] gains/loses [Status]" (applied, removed, expired)
7. **Spell/ability use** — "[Caster] casts [Spell]" with spell name highlighted
8. **Passive triggers** — "[Character]: [Passive] triggered"
9. **Equipment status** — "[Status] from [Item]"
10. **Stealth/perception** — "[Spotter] spotted [Target]" with passive/status attribution
11. **Combat round markers** — "Round X" header
12. **XP gains** — "[X] XP gained"
13. **Dialog starts** — "[Speaker] talks to [Target]"
14. **Multiplayer pings** — "[Player] pinged [Target/Location]"
15. **Surface interactions** — "[Surface] affects [Target]"
16. **Immunities/resistances** — implicit via damage tooltips showing reduced/blocked damage
