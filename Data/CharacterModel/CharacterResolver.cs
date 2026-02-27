using System;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Data.CharacterModel
{
    /// <summary>
    /// Resolves a CharacterSheet into final combat-ready stats.
    /// Follows the deterministic build resolution order from the BG3 guide:
    /// 1. Base ability scores (point buy)
    /// 2. Race/subrace grants
    /// 3. Background grants
    /// 4. Class level progression (including subclass)
    /// 5. Feat grants
    /// 6. Compute derived stats
    /// 7. Multiclass spell slot merging (if applicable)
    /// </summary>
    public class CharacterResolver
    {
        private readonly CharacterDataRegistry _registry;
        private QDND.Combat.Actions.ActionRegistry _actionRegistry;
        
        /// <summary>
        /// 5e/BG3 multiclass spell slot table indexed by caster level (1-12).
        /// Each entry is an array of slot counts for spell levels 1-6.
        /// </summary>
        private static readonly int[][] MulticlassSlotTable = new[]
        {
            // CL  1st 2nd 3rd 4th 5th 6th
            new[] { 2, 0, 0, 0, 0, 0 },  // Caster level 1
            new[] { 3, 0, 0, 0, 0, 0 },  // Caster level 2
            new[] { 4, 2, 0, 0, 0, 0 },  // Caster level 3
            new[] { 4, 3, 0, 0, 0, 0 },  // Caster level 4
            new[] { 4, 3, 2, 0, 0, 0 },  // Caster level 5
            new[] { 4, 3, 3, 0, 0, 0 },  // Caster level 6
            new[] { 4, 3, 3, 1, 0, 0 },  // Caster level 7
            new[] { 4, 3, 3, 2, 0, 0 },  // Caster level 8
            new[] { 4, 3, 3, 3, 1, 0 },  // Caster level 9
            new[] { 4, 3, 3, 3, 2, 0 },  // Caster level 10
            new[] { 4, 3, 3, 3, 2, 1 },  // Caster level 11
            new[] { 4, 3, 3, 3, 2, 1 },  // Caster level 12
        };
        
        public CharacterResolver(CharacterDataRegistry registry)
        {
            _registry = registry;
        }

        /// <summary>
        /// Set the ActionRegistry for ability validation.
        /// </summary>
        public void SetActionRegistry(QDND.Combat.Actions.ActionRegistry actionRegistry)
        {
            _actionRegistry = actionRegistry;
        }

        /// <summary>
        /// Optional seed for spell selection randomization. When set, this overrides
        /// the default name-based hash for spell shuffling, allowing different scenario
        /// seeds to produce different spell loadouts for the same character.
        /// </summary>
        public int? SpellSelectionSeed { get; set; }
        
        /// <summary>
        /// Resolve a character sheet into a fully computed character.
        /// </summary>
        public ResolvedCharacter Resolve(CharacterSheet sheet)
        {
            var resolved = new ResolvedCharacter();
            resolved.Name = sheet.Name;
            resolved.Sheet = sheet;
            
            // Step 1: Collect all features from all sources
            var allFeatures = new List<Feature>();
            
            // Step 2: Race and subrace features
            var race = _registry.GetRace(sheet.RaceId);
            if (race != null)
            {
                resolved.Speed = race.Speed;
                resolved.DarkvisionRange = race.DarkvisionRange;
                
                // Add race features that are at or below character level
                foreach (var f in race.Features)
                {
                    if (f.GrantedAtLevel <= sheet.TotalLevel)
                        allFeatures.Add(f);
                }
                
                // Subrace
                if (!string.IsNullOrEmpty(sheet.SubraceId))
                {
                    var subrace = race.Subraces?.FirstOrDefault(s => s.Id == sheet.SubraceId);
                    if (subrace != null)
                    {
                        if (subrace.DarkvisionOverride.HasValue)
                            resolved.DarkvisionRange = subrace.DarkvisionOverride.Value;
                        if (subrace.SpeedOverride.HasValue)
                            resolved.Speed = subrace.SpeedOverride.Value;
                        
                        foreach (var f in subrace.Features)
                        {
                            if (f.GrantedAtLevel <= sheet.TotalLevel)
                                allFeatures.Add(f);
                        }
                    }
                }
            }
            
            // Step 3: Background skill proficiencies
            if (sheet.BackgroundSkills != null)
            {
                foreach (var skillName in sheet.BackgroundSkills)
                {
                    if (Enum.TryParse<Skill>(skillName, true, out var skill))
                        resolved.Proficiencies.Skills.Add(skill);
                }
            }
            
            // Step 4: Class progression
            // Starting class gets full proficiencies + save proficiencies
            // Additional classes get multiclass proficiencies only
            var classLevelCounts = new Dictionary<string, int>();
            bool isFirstClass = true;
            int maxExtraAttacks = 0; // Track highest extra attacks value
            var subclassSpells = new List<string>();
            
            foreach (var cl in sheet.ClassLevels)
            {
                if (!classLevelCounts.ContainsKey(cl.ClassId))
                    classLevelCounts[cl.ClassId] = 0;
                classLevelCounts[cl.ClassId]++;
                int currentClassLevel = classLevelCounts[cl.ClassId];
                
                var classDef = _registry.GetClass(cl.ClassId);
                if (classDef == null) continue;
                
                // First level of first class: full starting proficiencies + saves
                if (isFirstClass && currentClassLevel == 1)
                {
                    // Saving throw proficiencies (only from starting class)
                    foreach (var saveName in classDef.SavingThrowProficiencies)
                    {
                        if (Enum.TryParse<AbilityType>(saveName, true, out var ability))
                            resolved.Proficiencies.SavingThrows.Add(ability);
                    }
                    
                    ApplyProficiencyGrant(classDef.StartingProficiencies, resolved.Proficiencies);
                    isFirstClass = false;
                }
                else if (currentClassLevel == 1)
                {
                    // Multiclass into new class: limited proficiencies
                    ApplyProficiencyGrant(classDef.MulticlassProficiencies, resolved.Proficiencies);
                }
                
                // Level table features
                if (classDef.LevelTable.TryGetValue(currentClassLevel.ToString(), out var progression))
                {
                    ApplyLevelProgressionResources(progression, classDef.Id, resolved);

                    if (progression.Features != null)
                        allFeatures.AddRange(progression.Features);
                    
                    // Track the highest ExtraAttacks value from any class
                    if (progression.ExtraAttacks.HasValue && progression.ExtraAttacks.Value > maxExtraAttacks)
                        maxExtraAttacks = progression.ExtraAttacks.Value;
                }
                
                // Subclass features
                if (!string.IsNullOrEmpty(cl.SubclassId))
                {
                    var subclass = classDef.Subclasses?.FirstOrDefault(s => s.Id == cl.SubclassId);
                    if (subclass?.LevelTable != null && 
                        subclass.LevelTable.TryGetValue(currentClassLevel.ToString(), out var subProgression))
                    {
                        ApplyLevelProgressionResources(subProgression, classDef.Id, resolved);

                        if (subProgression.Features != null)
                            allFeatures.AddRange(subProgression.Features);
                    }
                    
                    if (subclass?.AlwaysPreparedSpells != null &&
                        subclass.AlwaysPreparedSpells.TryGetValue(currentClassLevel.ToString(), out var alwaysPrepared))
                    {
                        subclassSpells.AddRange(alwaysPrepared);
                    }
                }
            }
            
            // Step 5: Feats
            foreach (var featId in sheet.FeatIds)
            {
                var feat = _registry.GetFeat(featId);
                if (feat?.Features != null)
                    allFeatures.AddRange(feat.Features);

                // Grant BG3 passive IDs for feats that are implemented as toggleable passives
                if (featId.Equals("GreatWeaponMaster", StringComparison.OrdinalIgnoreCase))
                {
                    allFeatures.Add(new Feature { Id = "GreatWeaponMaster_BonusAttack", IsPassive = true });
                    allFeatures.Add(new Feature { Id = "GreatWeaponMaster_BonusDamage", IsPassive = true });
                }
                else if (featId.Equals("Sharpshooter", StringComparison.OrdinalIgnoreCase))
                {
                    allFeatures.Add(new Feature { Id = "Sharpshooter_AllIn", IsPassive = true });
                    allFeatures.Add(new Feature { Id = "Sharpshooter_Bonuses", IsPassive = true });
                }
            }
            
            // Step 5b: Add selected metamagic options as passive features
            if (sheet.MetamagicIds != null)
            {
                foreach (var mmId in sheet.MetamagicIds)
                {
                    allFeatures.Add(new Feature
                    {
                        Id = $"metamagic_{mmId}",
                        Name = $"Metamagic: {mmId}",
                        IsPassive = true
                    });
                }
            }

            ApplyWarlockInvocations(sheet, allFeatures, resolved);

            // Step 6: Apply all collected features and extra attacks
            resolved.Features = allFeatures;
            resolved.ExtraAttacks = maxExtraAttacks;
            foreach (var feature in allFeatures)
            {
                ApplyFeature(feature, resolved);
            }
            
            // Step 7: Compute final ability scores
            foreach (AbilityType ability in Enum.GetValues(typeof(AbilityType)))
            {
                resolved.AbilityScores[ability] = sheet.GetAbilityScore(ability, allFeatures);
            }

            // Step 7b: Apply explicit ASIs from the sheet (e.g., manual point-buy overrides)
            if (sheet.AbilityScoreImprovements != null)
            {
                foreach (var (abilityName, bonus) in sheet.AbilityScoreImprovements)
                {
                    if (Enum.TryParse<AbilityType>(abilityName, true, out var ability))
                        resolved.AbilityScores[ability] = Math.Min(20, resolved.AbilityScores[ability] + bonus);
                }
            }
            
            // Step 8: Compute HP
            resolved.MaxHP = ComputeMaxHP(sheet, resolved);
            
            // Step 9: Compute AC base (10 + DEX mod)
            // Note: Actual equipment-based AC (armor + shield) will be computed at combatant spawn time
            // when equipment is resolved in ScenarioLoader.ResolveEquipment()
            int dexMod = CharacterSheet.GetModifier(resolved.AbilityScores[AbilityType.Dexterity]);
            resolved.BaseAC = 10 + dexMod;
            
            // Collect all granted abilities from features (before spell limit caps).
            var rawAbilityPool = allFeatures
                .Where(f => f.GrantedAbilities != null)
                .SelectMany(f => f.GrantedAbilities)
                .Distinct()
                .ToList();

            // Apply per-class spell limits. Subclass spells (AlwaysPreparedSpells) are always
            // included outside the cap. Non-spell features are never filtered.
            resolved.AllAbilities = ApplySpellLimits(rawAbilityPool, sheet, subclassSpells, resolved);

            // For prepared-spell casters, manual PreparedSpellIds always apply unconditionally.
            if (sheet.PreparedSpellIds?.Count > 0)
            {
                resolved.AllAbilities = resolved.AllAbilities
                    .Concat(sheet.PreparedSpellIds)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            
            // Apply dynamic feat choices (ability selections, spell grants, etc.)
            foreach (var featId in sheet.FeatIds)
                if (sheet.FeatChoices.TryGetValue(featId, out var choices))
                    ApplyFeatChoices(featId, choices, resolved);

            // Step 10: Multiclass spell slot merging
            // If character has multiple spellcasting classes (excluding Warlock Pact Magic),
            // replace per-class spell slots with the unified multiclass table.
            MergeMulticlassSpellSlots(sheet, resolved);
            
            // Validate action IDs (log warnings for missing abilities)
            resolved.AllAbilities = ValidateActionIds(resolved.AllAbilities, sheet.Name);
            
            return resolved;
        }
        
        /// <summary>
        /// Cap spells in the raw ability pool according to BG3/5e per-class rules.
        /// Prepared-spell classes (Wizard, Cleric) cap leveled spells at classLevel + abilityMod (min 1).
        /// Known-spell classes (Sorcerer, Warlock) cap leveled spells at SpellsKnown from the level
        /// table, or classLevel + 1 if the table entry is absent.
        /// All caster classes cap cantrips at CantripsKnown from the level table.
        /// Subclass spells (AlwaysPreparedSpells) are always included outside the cap.
        /// Non-spell class features are never filtered.
        /// </summary>
        /// <summary>
        /// Stable string hash that does not change across process restarts.
        /// .NET 6+ randomizes string.GetHashCode() by default.
        /// </summary>
        private static int StableStringHash(string s)
        {
            if (s == null) return 0;
            int h = 17;
            foreach (char c in s)
                h = unchecked(h * 31 + c);
            return h;
        }

        private List<string> ApplySpellLimits(List<string> rawAbilityIds, CharacterSheet sheet,
            List<string> subclassSpellIds, ResolvedCharacter resolved)
        {
            if (_actionRegistry == null)
            {
                // Without a registry we cannot classify spells — pass through unchanged.
                return rawAbilityIds.Concat(subclassSpellIds).Distinct().ToList();
            }

            // Classify abilities by type using the action registry.
            var nonSpells = new List<string>();      // Class features (rage, second_wind, etc.)
            var cantrips = new List<string>();       // SpellLevel == 0, has a magic school
            var leveledSpells = new List<string>();  // SpellLevel > 0

            foreach (var id in rawAbilityIds)
            {
                var action = _actionRegistry.GetAction(id);
                if (action == null)
                {
                    // Unknown ID — treat as a non-spell; ValidateActionIds will handle it later.
                    nonSpells.Add(id);
                    continue;
                }

                if (action.SpellLevel > 0)
                    leveledSpells.Add(id);
                else if (action.School != QDND.Combat.Actions.SpellSchool.None)
                    cantrips.Add(id);
                else
                    nonSpells.Add(id);
            }

            // If no spells present, skip cap logic entirely.
            if (cantrips.Count == 0 && leveledSpells.Count == 0)
            {
                var allNoSpells = new List<string>(nonSpells);
                allNoSpells.AddRange(subclassSpellIds);
                return allNoSpells.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

            // Accumulate per-class caps.
            var classLevelCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var cl in sheet.ClassLevels)
            {
                classLevelCounts.TryGetValue(cl.ClassId, out int cur);
                classLevelCounts[cl.ClassId] = cur + 1;
            }

            int totalCantripsAllowed = 0;
            int totalLeveledAllowed = 0;
            bool anySpellcaster = false;

            foreach (var (classId, classLevel) in classLevelCounts)
            {
                var classDef = _registry.GetClass(classId);
                if (classDef == null || string.IsNullOrEmpty(classDef.SpellcastingAbility))
                    continue;

                anySpellcaster = true;

                // Cantrips: walk backwards through the level table to find the most recent CantripsKnown.
                int cantripsKnown = 0;
                for (int lvl = classLevel; lvl >= 1; lvl--)
                {
                    if (classDef.LevelTable.TryGetValue(lvl.ToString(), out var prog) &&
                        prog.CantripsKnown.HasValue)
                    {
                        cantripsKnown = prog.CantripsKnown.Value;
                        break;
                    }
                }
                totalCantripsAllowed += cantripsKnown;

                // Leveled spells.
                if (classDef.UsesPreparedSpells)
                {
                    // Prepared-spell casters: cap = classLevel + spellcasting ability modifier (min 1).
                    if (Enum.TryParse<AbilityType>(classDef.SpellcastingAbility, true, out var spellAbility) &&
                        resolved.AbilityScores.TryGetValue(spellAbility, out int score))
                    {
                        int abilityMod = (int)Math.Floor((score - 10) / 2.0);
                        totalLeveledAllowed += Math.Max(1, classLevel + abilityMod);
                    }
                    else
                    {
                        totalLeveledAllowed += Math.Max(1, classLevel);
                    }
                }
                else
                {
                    // Known-spell casters: walk backwards; fall back to classLevel + 1
                    // (standard 5e Sorcerer/Warlock/Bard/Ranger progression).
                    int spellsKnown = classLevel + 1;
                    for (int lvl = classLevel; lvl >= 1; lvl--)
                    {
                        if (classDef.LevelTable.TryGetValue(lvl.ToString(), out var prog) &&
                            prog.SpellsKnown.HasValue)
                        {
                            spellsKnown = prog.SpellsKnown.Value;
                            break;
                        }
                    }
                    totalLeveledAllowed += spellsKnown;
                }
            }

            // No spellcasting class — return everything unfiltered.
            if (!anySpellcaster)
            {
                var allNoCaster = new List<string>(nonSpells);
                allNoCaster.AddRange(cantrips);
                allNoCaster.AddRange(leveledSpells);
                allNoCaster.AddRange(subclassSpellIds);
                return allNoCaster.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

            // Seeded random keyed to character name: same build always gets the same subset.
            // If SpellSelectionSeed is provided, XOR it with the name hash so different scenario
            // seeds produce different spell loadouts while still differentiating characters.
            int nameHash = StableStringHash(sheet.Name);
            var rng = SpellSelectionSeed.HasValue
                ? new Random(SpellSelectionSeed.Value ^ nameHash)
                : new Random(nameHash != 0 ? nameHash : 42);

            // Subclass (domain/oath) spells are always-prepared — exclude them from the cap pool.
            var subclassSpellSet = new HashSet<string>(subclassSpellIds, StringComparer.OrdinalIgnoreCase);
            var cappableLeveled = leveledSpells.Where(s => !subclassSpellSet.Contains(s)).ToList();

            var selectedCantrips = cantrips
                .OrderBy(_ => rng.Next())
                .Take(Math.Max(0, totalCantripsAllowed))
                .ToList();

            var selectedLeveled = cappableLeveled
                .OrderBy(_ => rng.Next())
                .Take(Math.Max(0, totalLeveledAllowed))
                .ToList();

            var result = new List<string>(nonSpells);
            result.AddRange(subclassSpellIds);
            result.AddRange(selectedCantrips);
            result.AddRange(selectedLeveled);
            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Validate that action IDs exist in the ActionRegistry.
        /// Returns a validation report with resolved vs unresolved abilities.
        /// </summary>
        private List<string> ValidateActionIds(List<string> abilityIds, string characterName)
        {
            var validAbilities = new List<string>();
            var missingAbilities = new List<string>();
            
            foreach (var abilityId in abilityIds)
            {
                // Check if ActionRegistry is available and if the ability exists
                if (_actionRegistry != null)
                {
                    var action = _actionRegistry.GetAction(abilityId);
                    if (action != null)
                    {
                        validAbilities.Add(abilityId);
                    }
                    else
                    {
                        missingAbilities.Add(abilityId);
                    }
                }
                else
                {
                    // No registry available - assume all abilities are valid
                    validAbilities.Add(abilityId);
                }
            }
            
            if (missingAbilities.Count > 0 && _actionRegistry != null)
            {
                Console.WriteLine($"[CharacterResolver] Warning: Character '{characterName}' has {missingAbilities.Count} unregistered abilities:");
                foreach (var missing in missingAbilities.Take(10))
                {
                    Console.WriteLine($"  - {missing}");
                }
                if (missingAbilities.Count > 10)
                {
                    Console.WriteLine($"  ... and {missingAbilities.Count - 10} more");
                }
            }
            
            return validAbilities;
        }

        /// <summary>
        /// Get a validation report for a character's abilities.
        /// Returns total abilities, resolved count, and unresolved count.
        /// </summary>
        public (int Total, int Resolved, int Unresolved, List<string> Missing) GetValidationReport(ResolvedCharacter character)
        {
            if (_actionRegistry == null)
            {
                return (character.AllAbilities.Count, character.AllAbilities.Count, 0, new List<string>());
            }

            var missing = new List<string>();
            int resolved = 0;

            foreach (var abilityId in character.AllAbilities)
            {
                var action = _actionRegistry.GetAction(abilityId);
                if (action != null)
                {
                    resolved++;
                }
                else
                {
                    missing.Add(abilityId);
                }
            }

            return (character.AllAbilities.Count, resolved, character.AllAbilities.Count - resolved, missing);
        }

        private void ApplyWarlockInvocations(CharacterSheet sheet, List<Feature> allFeatures, ResolvedCharacter resolved)
        {
            if (sheet?.InvocationIds == null || sheet.InvocationIds.Count == 0)
                return;

            int allowed = resolved.Resources.TryGetValue("invocations_known", out var known)
                ? Math.Max(0, known)
                : 0;
            if (allowed <= 0)
                return;

            foreach (var invocationId in sheet.InvocationIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(allowed))
            {
                var invocation = _registry.GetFeat(invocationId);
                if (invocation?.Features != null)
                    allFeatures.AddRange(invocation.Features);
            }
        }
        
        private void ApplyFeatChoices(string featId, Dictionary<string, string> choices, ResolvedCharacter resolved)
        {
            switch (featId.ToLowerInvariant())
            {
                case "resilient":
                    if (choices.TryGetValue("ability", out var resilientAbility) &&
                        Enum.TryParse<AbilityType>(resilientAbility, true, out var resiAbil))
                    {
                        resolved.Proficiencies.SavingThrows.Add(resiAbil);
                        resolved.AbilityScores[resiAbil] = Math.Min(20, resolved.AbilityScores[resiAbil] + 1);
                    }
                    break;
                case "elemental_adept":
                    if (choices.TryGetValue("damageType", out var elemType))
                        resolved.ElementalAdeptTypes.Add(elemType.ToLowerInvariant());
                    break;
                case "skilled":
                    foreach (var (_, skillName) in choices)
                        if (Enum.TryParse<Skill>(skillName, true, out var sk))
                            resolved.Proficiencies.Skills.Add(sk);
                    break;
                case "athlete":
                case "lightly_armoured":
                case "moderately_armoured":
                case "tavern_brawler":
                case "weapon_master":
                    if (choices.TryGetValue("ability", out var chosenAbil) &&
                        Enum.TryParse<AbilityType>(chosenAbil, true, out var abilEnum))
                        resolved.AbilityScores[abilEnum] = Math.Min(20, resolved.AbilityScores[abilEnum] + 1);
                    break;
            }
            // Magic Initiate variants: grant chosen spells
            if (featId.StartsWith("magic_initiate", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var (_, spellId) in choices)
                    if (!string.IsNullOrEmpty(spellId) && !resolved.AllAbilities.Contains(spellId))
                        resolved.AllAbilities.Add(spellId);
            }
        }

        private int ComputeMaxHP(CharacterSheet sheet, ResolvedCharacter resolved)
        {
            int conMod = CharacterSheet.GetModifier(resolved.AbilityScores[AbilityType.Constitution]);
            int totalHP = 0;
            
            var classLevelCounts = new Dictionary<string, int>();
            foreach (var cl in sheet.ClassLevels)
            {
                if (!classLevelCounts.ContainsKey(cl.ClassId))
                    classLevelCounts[cl.ClassId] = 0;
                classLevelCounts[cl.ClassId]++;
                int currentClassLevel = classLevelCounts[cl.ClassId];
                
                var classDef = _registry.GetClass(cl.ClassId);
                if (classDef == null) continue;
                
                if (classLevelCounts[cl.ClassId] == 1 && cl == sheet.ClassLevels[0])
                {
                    // First level of starting class: max hit die + CON
                    totalHP += classDef.HpAtFirstLevel + conMod;
                }
                else
                {
                    // Subsequent levels: average + CON
                    totalHP += classDef.HpPerLevelAfterFirst + conMod;
                }
            }
            
            // Apply HP bonuses from features (e.g., Dwarven Toughness)
            int hpPerLevelBonus = resolved.Features
                .Where(f => f.HpPerLevel > 0)
                .Sum(f => f.HpPerLevel);
            totalHP += hpPerLevelBonus * sheet.TotalLevel;
            
            int flatHpBonus = resolved.Features.Sum(f => f.HpBonus);
            totalHP += flatHpBonus;
            
            return Math.Max(1, totalHP);
        }
        
        /// <summary>
        /// For multiclass characters with 2+ spellcasting classes (non-Warlock),
        /// replace per-class spell slots with the 5e/BG3 merged multiclass table.
        /// Warlock Pact Magic slots are kept separate (stored as pact_slots/pact_slot_level).
        /// Single-class characters or characters with only one casting class are unaffected.
        /// </summary>
        private void MergeMulticlassSpellSlots(CharacterSheet sheet, ResolvedCharacter resolved)
        {
            // Count distinct spellcasting classes (non-Warlock) and compute combined caster level
            var classLevelCounts = new Dictionary<string, int>();
            foreach (var cl in sheet.ClassLevels)
            {
                if (!classLevelCounts.ContainsKey(cl.ClassId))
                    classLevelCounts[cl.ClassId] = 0;
                classLevelCounts[cl.ClassId]++;
            }
            
            int casterClassCount = 0;
            double rawCasterLevel = 0;
            
            foreach (var (classId, levels) in classLevelCounts)
            {
                if (string.Equals(classId, "warlock", StringComparison.OrdinalIgnoreCase))
                    continue; // Warlock Pact Magic is separate
                    
                var classDef = _registry.GetClass(classId);
                if (classDef == null)
                    continue;

                double spellcasterModifier = classDef.SpellcasterModifier;
                if (spellcasterModifier <= 0)
                    spellcasterModifier = ResolveSubclassSpellcasterModifier(sheet, classId, classDef);
                if (spellcasterModifier <= 0)
                    continue;
                
                casterClassCount++;
                rawCasterLevel += GetCasterLevelContribution(levels, spellcasterModifier);
            }
            
            // Only merge if character has 2+ spellcasting classes
            if (casterClassCount < 2)
                return;
            
            int casterLevel = Math.Clamp((int)rawCasterLevel, 1, 12);
            
            // Clear existing per-class spell slots
            var slotKeysToRemove = resolved.Resources.Keys
                .Where(k => k.StartsWith("spell_slot_", StringComparison.Ordinal))
                .ToList();
            foreach (var key in slotKeysToRemove)
                resolved.Resources.Remove(key);
            
            // Apply merged table
            int[] slots = MulticlassSlotTable[casterLevel - 1];
            for (int spellLevel = 1; spellLevel <= 6; spellLevel++)
            {
                if (slots[spellLevel - 1] > 0)
                    resolved.Resources[$"spell_slot_{spellLevel}"] = slots[spellLevel - 1];
            }
        }

        private static double ResolveSubclassSpellcasterModifier(CharacterSheet sheet, string classId, ClassDefinition classDef)
        {
            if (sheet?.ClassLevels == null || classDef?.Subclasses == null)
                return 0;

            string activeSubclassId = sheet.ClassLevels
                .Where(cl => string.Equals(cl.ClassId, classId, StringComparison.OrdinalIgnoreCase))
                .Select(cl => cl.SubclassId)
                .LastOrDefault(id => !string.IsNullOrWhiteSpace(id));

            if (string.IsNullOrWhiteSpace(activeSubclassId))
                return 0;

            var subclass = classDef.Subclasses.FirstOrDefault(s =>
                string.Equals(s.Id, activeSubclassId, StringComparison.OrdinalIgnoreCase));
            return subclass?.SpellcasterModifier ?? 0;
        }

        private static double GetCasterLevelContribution(int levels, double modifier)
        {
            if (levels <= 0 || modifier <= 0)
                return 0;

            if (Math.Abs(modifier - 1.0) < 0.0001)
                return levels;
            if (Math.Abs(modifier - 0.5) < 0.0001)
                return levels / 2;
            if (Math.Abs(modifier - 0.3333) < 0.001 || Math.Abs(modifier - (1.0 / 3.0)) < 0.001)
                return levels / 3;

            return levels * modifier;
        }
        
        private void ApplyFeature(Feature feature, ResolvedCharacter resolved)
        {
            // Proficiencies
            if (feature.Proficiencies != null)
                ApplyProficiencyGrant(feature.Proficiencies, resolved.Proficiencies);
            
            // Resistances
            if (feature.Resistances != null)
                resolved.DamageResistances.UnionWith(feature.Resistances);
            
            // Immunities
            if (feature.Immunities != null)
                resolved.DamageImmunities.UnionWith(feature.Immunities);
            
            // Condition immunities
            if (feature.ConditionImmunities != null)
                resolved.ConditionImmunities.UnionWith(feature.ConditionImmunities);
            
            // Speed modifier
            if (feature.SpeedModifier != 0)
                resolved.Speed += feature.SpeedModifier;
            
            // Darkvision (take the highest)
            if (feature.DarkvisionRange > resolved.DarkvisionRange)
                resolved.DarkvisionRange = feature.DarkvisionRange;
            
            // Resource grants
            if (feature.ResourceGrants != null)
            {
                foreach (var kvp in feature.ResourceGrants)
                {
                    resolved.Resources[kvp.Key] = kvp.Value; // Overwrite (latest level wins)
                }
            }
        }

        private static void ApplyLevelProgressionResources(LevelProgression progression, string classId, ResolvedCharacter resolved)
        {
            if (progression == null || resolved == null)
                return;

            if (progression.Resources != null)
            {
                foreach (var (resourceId, value) in progression.Resources)
                {
                    resolved.Resources[resourceId] = value;
                }
            }

            if (progression.InvocationsKnown.HasValue)
                resolved.Resources["invocations_known"] = progression.InvocationsKnown.Value;

            if (progression.SpellSlots != null)
            {
                // Warlock Pact Magic: slots are all at the same (highest) level.
                // Clear previous spell_slot_* entries so old levels don't accumulate.
                if (string.Equals(classId, "warlock", StringComparison.OrdinalIgnoreCase))
                {
                    var staleSlotKeys = resolved.Resources.Keys
                        .Where(k => k.StartsWith("spell_slot_", StringComparison.Ordinal))
                        .ToList();
                    foreach (var key in staleSlotKeys)
                        resolved.Resources.Remove(key);
                }

                foreach (var (slotKey, slotCount) in progression.SpellSlots)
                {
                    if (int.TryParse(slotKey, out int spellLevel) && spellLevel > 0)
                    {
                        if (string.Equals(classId, "warlock", StringComparison.OrdinalIgnoreCase))
                        {
                            resolved.Resources["pact_slots"] = slotCount;
                            resolved.Resources["pact_slot_level"] = spellLevel;
                            // Also expose as a regular spell slot so spell-slot-based checks work
                            resolved.Resources[$"spell_slot_{spellLevel}"] = slotCount;
                        }
                        else
                        {
                            resolved.Resources[$"spell_slot_{spellLevel}"] = slotCount;
                        }
                    }
                }
            }
        }
        
        private void ApplyProficiencyGrant(ProficiencyGrant grant, ProficiencySet proficiencies)
        {
            if (grant == null) return;
            
            if (grant.SavingThrows != null)
                foreach (var s in grant.SavingThrows)
                    if (Enum.TryParse<AbilityType>(s, true, out var st)) proficiencies.SavingThrows.Add(st);
            
            if (grant.Skills != null)
                foreach (var s in grant.Skills)
                    if (Enum.TryParse<Skill>(s, true, out var sk)) proficiencies.Skills.Add(sk);
            
            if (grant.Expertise != null)
                foreach (var s in grant.Expertise)
                    if (Enum.TryParse<Skill>(s, true, out var sk)) proficiencies.Expertise.Add(sk);
            
            if (grant.WeaponCategories != null)
                foreach (var s in grant.WeaponCategories)
                    if (Enum.TryParse<WeaponCategory>(s, true, out var wc)) proficiencies.WeaponCategories.Add(wc);
            
            if (grant.Weapons != null)
                foreach (var s in grant.Weapons)
                    if (Enum.TryParse<WeaponType>(s, true, out var wt)) proficiencies.Weapons.Add(wt);
            
            if (grant.ArmorCategories != null)
                foreach (var s in grant.ArmorCategories)
                    if (Enum.TryParse<ArmorCategory>(s, true, out var ac)) proficiencies.ArmorCategories.Add(ac);
        }
    }
    
    /// <summary>
    /// The fully resolved character — all features applied, all stats computed.
    /// </summary>
    public class ResolvedCharacter
    {
        public string Name { get; set; }
        public CharacterSheet Sheet { get; set; }
        
        // Final ability scores (after all bonuses)
        public Dictionary<AbilityType, int> AbilityScores { get; set; } = new()
        {
            { AbilityType.Strength, 10 },
            { AbilityType.Dexterity, 10 },
            { AbilityType.Constitution, 10 },
            { AbilityType.Intelligence, 10 },
            { AbilityType.Wisdom, 10 },
            { AbilityType.Charisma, 10 }
        };
        
        public int GetModifier(AbilityType ability) => CharacterSheet.GetModifier(AbilityScores[ability]);
        
        // Computed stats
        public int MaxHP { get; set; }
        public int BaseAC { get; set; } = 10;
        public float Speed { get; set; } = 30f;
        public float DarkvisionRange { get; set; } = 0f;
        public float FlySpeed { get; set; } = 0f;
        public float SwimSpeed { get; set; } = 0f;
        public float ClimbSpeed { get; set; } = 0f;
        
        // Proficiencies
        public ProficiencySet Proficiencies { get; set; } = new();
        
        // Resistances & Immunities
        public HashSet<DamageType> DamageResistances { get; set; } = new();
        public HashSet<DamageType> DamageImmunities { get; set; } = new();
        public HashSet<string> ConditionImmunities { get; set; } = new();

        // Elemental Adept: damage types where 1s count as 2s and resistance is bypassed
        public HashSet<string> ElementalAdeptTypes { get; set; } = new();
        
        // Resources (rage charges, ki points, etc.)
        public Dictionary<string, int> Resources { get; set; } = new();
        
        // All features applied
        public List<Feature> Features { get; set; } = new();
        
        // All ability IDs from features
        public List<string> AllAbilities { get; set; } = new();
        
        // Extra attacks from class features
        public int ExtraAttacks { get; set; } = 0;
        
        /// <summary>
        /// Get the skill check bonus for a skill.
        /// </summary>
        public int GetSkillBonus(Skill skill, int proficiencyBonus)
        {
            var ability = GetSkillAbility(skill);
            int abilityMod = GetModifier(ability);
            
            if (Proficiencies.HasExpertise(skill))
                return abilityMod + proficiencyBonus * 2;
            if (Proficiencies.IsProficientInSkill(skill))
                return abilityMod + proficiencyBonus;
            return abilityMod;
        }
        
        /// <summary>
        /// Get the saving throw bonus for an ability.
        /// </summary>
        public int GetSavingThrowBonus(AbilityType ability, int proficiencyBonus)
        {
            int abilityMod = GetModifier(ability);
            if (Proficiencies.IsProficientInSave(ability))
                return abilityMod + proficiencyBonus;
            return abilityMod;
        }
        
        /// <summary>
        /// Get the ability that governs a skill.
        /// </summary>
        public static AbilityType GetSkillAbility(Skill skill) => skill switch
        {
            Skill.Athletics => AbilityType.Strength,
            Skill.Acrobatics or Skill.SleightOfHand or Skill.Stealth => AbilityType.Dexterity,
            Skill.Arcana or Skill.History or Skill.Investigation or Skill.Nature or Skill.Religion => AbilityType.Intelligence,
            Skill.AnimalHandling or Skill.Insight or Skill.Medicine or Skill.Perception or Skill.Survival => AbilityType.Wisdom,
            Skill.Deception or Skill.Intimidation or Skill.Performance or Skill.Persuasion => AbilityType.Charisma,
            _ => AbilityType.Intelligence
        };
    }
}
