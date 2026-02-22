using System;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Data.CharacterModel
{
    /// <summary>
    /// Builder for programmatically constructing CharacterSheet from user choices.
    /// Supports BG3 point buy (27 points) and validates build legality.
    /// </summary>
    public class CharacterBuilder
    {
        // --- Point Buy Constants (BG3 standard) ---
        public const int PointBuyBudget = 27;
        public const int MinAbilityScore = 8;
        public const int MaxAbilityScore = 15;

        /// <summary>
        /// Point buy cost table: score value -> cumulative cost from 8.
        /// 8=0, 9=1, 10=2, 11=3, 12=4, 13=5, 14=7, 15=9
        /// </summary>
        public static readonly Dictionary<int, int> PointBuyCosts = new()
        {
            { 8, 0 }, { 9, 1 }, { 10, 2 }, { 11, 3 },
            { 12, 4 }, { 13, 5 }, { 14, 7 }, { 15, 9 }
        };

        // --- Internal state ---
        private string _name;
        private string _raceId;
        private string _subraceId;
        private string _classId;
        private string _subclassId;
        private int _level = 1;
        private int _str = 10, _dex = 10, _con = 10, _int = 10, _wis = 10, _cha = 10;
        private readonly List<string> _featIds = new();
        private string _backgroundId;
        private readonly List<string> _backgroundSkills = new();
        private string _abilityBonus2; // +2 racial bonus target
        private string _abilityBonus1; // +1 racial bonus target
        
        // Multiclass state: explicit class level entries override the single-class _classId/_level
        private readonly List<ClassLevelEntry> _classLevelEntries = new();
        private readonly List<string> _metamagicIds = new();
        
        /// <summary>Compact entry for the builder's multiclass state.</summary>
        private class ClassLevelEntry
        {
            public string ClassId;
            public string SubclassId;
            public int Levels;
        }

        // --- Builder Methods ---

        /// <summary>Set the character's name.</summary>
        public CharacterBuilder SetName(string name)
        {
            _name = name;
            return this;
        }

        /// <summary>Set the character's race and optional subrace.</summary>
        public CharacterBuilder SetRace(string raceId, string subraceId = null)
        {
            _raceId = raceId;
            _subraceId = subraceId;
            return this;
        }

        /// <summary>Set the character's class and optional subclass.</summary>
        public CharacterBuilder SetClass(string classId, string subclassId = null)
        {
            _classId = classId;
            _subclassId = subclassId;
            return this;
        }

        /// <summary>Set the character level (1-12). Builds ClassLevels accordingly.</summary>
        public CharacterBuilder SetLevel(int level)
        {
            _level = Math.Clamp(level, 1, 12);
            return this;
        }

        /// <summary>Set base ability scores (before racial bonuses).</summary>
        public CharacterBuilder SetAbilityScores(int str, int dex, int con, int intel, int wis, int cha)
        {
            _str = str;
            _dex = dex;
            _con = con;
            _int = intel;
            _wis = wis;
            _cha = cha;
            return this;
        }

        /// <summary>Set the BG3 flexible racial ability bonuses (+2 to one, +1 to another).</summary>
        public CharacterBuilder SetRacialBonuses(string abilityForPlus2, string abilityForPlus1)
        {
            _abilityBonus2 = abilityForPlus2;
            _abilityBonus1 = abilityForPlus1;
            return this;
        }

        /// <summary>Add a feat to the build.</summary>
        public CharacterBuilder AddFeat(string featId)
        {
            if (!string.IsNullOrEmpty(featId) && !_featIds.Contains(featId))
                _featIds.Add(featId);
            return this;
        }

        /// <summary>Remove a feat from the build.</summary>
        public CharacterBuilder RemoveFeat(string featId)
        {
            _featIds.Remove(featId);
            return this;
        }

        /// <summary>Clear all selected feats.</summary>
        public CharacterBuilder ClearFeats()
        {
            _featIds.Clear();
            return this;
        }

        /// <summary>Set the background and its skill proficiencies.</summary>
        public CharacterBuilder SetBackground(string backgroundId, params string[] skills)
        {
            _backgroundId = backgroundId;
            _backgroundSkills.Clear();
            _backgroundSkills.AddRange(skills);
            return this;
        }

        // --- Metamagic (Sorcerer) ---

        /// <summary>Add a metamagic option. Valid: careful, distant, empowered, extended, heightened, quickened, subtle, twinned.</summary>
        public CharacterBuilder AddMetamagic(string metamagicId)
        {
            if (!string.IsNullOrEmpty(metamagicId) && !_metamagicIds.Contains(metamagicId))
                _metamagicIds.Add(metamagicId);
            return this;
        }

        // --- Multiclass API ---

        /// <summary>
        /// Add class levels for multiclass builds. Call multiple times for different classes.
        /// The first call defines the starting class (full proficiencies + saves).
        /// Overrides the single-class SetClass/SetLevel if used.
        /// </summary>
        public CharacterBuilder AddClassLevels(string classId, int levels, string subclassId = null)
        {
            if (string.IsNullOrWhiteSpace(classId) || levels < 1)
                return this;
            
            var existing = _classLevelEntries.Find(e => e.ClassId == classId);
            if (existing != null)
            {
                existing.Levels += levels;
                if (subclassId != null)
                    existing.SubclassId = subclassId;
            }
            else
            {
                _classLevelEntries.Add(new ClassLevelEntry
                {
                    ClassId = classId,
                    SubclassId = subclassId,
                    Levels = levels
                });
            }
            return this;
        }

        /// <summary>Whether this is a multiclass build (has explicit class level entries).</summary>
        public bool IsMulticlass => _classLevelEntries.Count > 1;

        /// <summary>
        /// Validate multiclass prerequisites against the character's ability scores.
        /// Returns true if the character meets all prereqs, false with error details otherwise.
        /// Requires a CharacterDataRegistry to look up class definitions.
        /// </summary>
        public bool ValidateMulticlassPrerequisites(CharacterDataRegistry registry, out List<string> errors)
        {
            errors = new List<string>();
            if (registry == null || _classLevelEntries.Count <= 1)
                return true;

            // BG3 rule: must meet prereqs of BOTH the starting class AND the class you're multiclassing INTO
            var abilityScores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "Strength", _str },
                { "Dexterity", _dex },
                { "Constitution", _con },
                { "Intelligence", _int },
                { "Wisdom", _wis },
                { "Charisma", _cha }
            };
            // Note: racial bonuses apply before prereq checks in BG3
            if (!string.IsNullOrEmpty(_abilityBonus2) && abilityScores.ContainsKey(_abilityBonus2))
                abilityScores[_abilityBonus2] += 2;
            if (!string.IsNullOrEmpty(_abilityBonus1) && abilityScores.ContainsKey(_abilityBonus1))
                abilityScores[_abilityBonus1] += 1;

            foreach (var entry in _classLevelEntries)
            {
                var classDef = registry.GetClass(entry.ClassId);
                if (classDef?.MulticlassPrerequisites == null || classDef.MulticlassPrerequisites.Count == 0)
                    continue;

                foreach (var (ability, minScore) in classDef.MulticlassPrerequisites)
                {
                    if (abilityScores.TryGetValue(ability, out int score) && score < minScore)
                    {
                        errors.Add($"Multiclass into {classDef.Name} requires {ability} >= {minScore} (have {score}).");
                    }
                }
            }
            return errors.Count == 0;
        }

        // --- Validation ---

        /// <summary>
        /// Validate the current build state.
        /// Returns true if valid, false with error messages otherwise.
        /// </summary>
        public bool IsValid(out List<string> errors)
        {
            errors = new List<string>();

            if (string.IsNullOrWhiteSpace(_name))
                errors.Add("Character must have a name.");

            if (string.IsNullOrWhiteSpace(_raceId))
                errors.Add("A race must be selected.");

            bool hasMulticlass = _classLevelEntries.Count > 0;
            
            if (!hasMulticlass && string.IsNullOrWhiteSpace(_classId))
                errors.Add("A class must be selected.");

            int totalLevel = hasMulticlass
                ? _classLevelEntries.Sum(e => e.Levels)
                : _level;
            
            if (totalLevel < 1 || totalLevel > 12)
                errors.Add($"Total level must be between 1 and 12 (got {totalLevel}).");

            // Validate ability scores are within point buy range
            int[] scores = { _str, _dex, _con, _int, _wis, _cha };
            foreach (int score in scores)
            {
                if (score < MinAbilityScore || score > MaxAbilityScore)
                {
                    errors.Add($"Ability scores must be between {MinAbilityScore} and {MaxAbilityScore} (got {score}).");
                    break;
                }
            }

            // Validate point buy budget
            int cost = CalculatePointBuyCost(_str, _dex, _con, _int, _wis, _cha);
            if (cost > PointBuyBudget)
                errors.Add($"Point buy cost ({cost}) exceeds budget ({PointBuyBudget}).");

            // Validate racial bonuses target different abilities
            if (!string.IsNullOrEmpty(_abilityBonus2) && !string.IsNullOrEmpty(_abilityBonus1))
            {
                if (_abilityBonus2.Equals(_abilityBonus1, StringComparison.OrdinalIgnoreCase))
                    errors.Add("Racial +2 and +1 bonuses must target different abilities.");
            }

            return errors.Count == 0;
        }

        /// <summary>
        /// Calculate total point buy cost for the given ability scores.
        /// Returns -1 if any score is outside the valid range.
        /// </summary>
        public int CalculatePointBuyCost(int str, int dex, int con, int intel, int wis, int cha)
        {
            int[] scores = { str, dex, con, intel, wis, cha };
            int total = 0;
            foreach (int score in scores)
            {
                if (PointBuyCosts.TryGetValue(score, out int cost))
                    total += cost;
                else
                    return -1; // out of range
            }
            return total;
        }

        /// <summary>Get the remaining point buy budget.</summary>
        public int GetPointBuyRemaining()
        {
            int cost = CalculatePointBuyCost(_str, _dex, _con, _int, _wis, _cha);
            return cost >= 0 ? PointBuyBudget - cost : 0;
        }

        /// <summary>
        /// Get the maximum number of feats available at the current level for the given class.
        /// Uses the class FeatLevels (default: 4, 8, 12).
        /// </summary>
        public int GetMaxFeats(ClassDefinition classDef = null)
        {
            var featLevels = classDef?.FeatLevels ?? new List<int> { 4, 8, 12 };
            return featLevels.Count(l => l <= _level);
        }

        // --- Build ---

        /// <summary>Build the CharacterSheet from current builder state.</summary>
        public CharacterSheet Build()
        {
            var sheet = new CharacterSheet
            {
                Name = _name ?? "Unnamed Hero",
                RaceId = _raceId,
                SubraceId = _subraceId,
                BaseStrength = _str,
                BaseDexterity = _dex,
                BaseConstitution = _con,
                BaseIntelligence = _int,
                BaseWisdom = _wis,
                BaseCharisma = _cha,
                AbilityBonus2 = _abilityBonus2,
                AbilityBonus1 = _abilityBonus1,
                FeatIds = new List<string>(_featIds),
                MetamagicIds = new List<string>(_metamagicIds),
                BackgroundId = _backgroundId,
                BackgroundSkills = new List<string>(_backgroundSkills)
            };

            sheet.ClassLevels = new List<ClassLevel>();
            
            if (_classLevelEntries.Count > 0)
            {
                // Multiclass build: expand each entry into individual ClassLevel instances
                foreach (var entry in _classLevelEntries)
                {
                    for (int i = 0; i < entry.Levels; i++)
                        sheet.ClassLevels.Add(new ClassLevel(entry.ClassId, entry.SubclassId));
                }
            }
            else
            {
                // Single-class build: all levels in the selected class
                for (int i = 0; i < _level; i++)
                    sheet.ClassLevels.Add(new ClassLevel(_classId, _subclassId));
            }

            return sheet;
        }

        /// <summary>
        /// Build the CharacterSheet and resolve it into a full ResolvedCharacter.
        /// </summary>
        public ResolvedCharacter BuildAndResolve(CharacterDataRegistry registry)
        {
            var sheet = Build();
            var resolver = new CharacterResolver(registry);
            return resolver.Resolve(sheet);
        }

        // --- Convenience: Create from existing sheet ---

        /// <summary>
        /// Initialize builder state from an existing CharacterSheet (for editing).
        /// </summary>
        public CharacterBuilder FromSheet(CharacterSheet sheet)
        {
            if (sheet == null) return this;

            _name = sheet.Name;
            _raceId = sheet.RaceId;
            _subraceId = sheet.SubraceId;
            _str = sheet.BaseStrength;
            _dex = sheet.BaseDexterity;
            _con = sheet.BaseConstitution;
            _int = sheet.BaseIntelligence;
            _wis = sheet.BaseWisdom;
            _cha = sheet.BaseCharisma;
            _abilityBonus2 = sheet.AbilityBonus2;
            _abilityBonus1 = sheet.AbilityBonus1;
            _backgroundId = sheet.BackgroundId;
            _backgroundSkills.Clear();
            if (sheet.BackgroundSkills != null)
                _backgroundSkills.AddRange(sheet.BackgroundSkills);
            _featIds.Clear();
            if (sheet.FeatIds != null)
                _featIds.AddRange(sheet.FeatIds);
            _metamagicIds.Clear();
            if (sheet.MetamagicIds != null)
                _metamagicIds.AddRange(sheet.MetamagicIds);

            // Extract class info from ClassLevels
            _classLevelEntries.Clear();
            if (sheet.ClassLevels != null && sheet.ClassLevels.Count > 0)
            {
                // Group by class to detect multiclass
                var groups = sheet.ClassLevels
                    .GroupBy(cl => cl.ClassId)
                    .ToList();
                
                if (groups.Count == 1)
                {
                    // Single-class: use legacy fields for backward compat
                    _classId = sheet.ClassLevels[0].ClassId;
                    _subclassId = sheet.ClassLevels[0].SubclassId;
                    _level = sheet.ClassLevels.Count;
                }
                else
                {
                    // Multiclass: populate entries
                    foreach (var group in groups)
                    {
                        _classLevelEntries.Add(new ClassLevelEntry
                        {
                            ClassId = group.Key,
                            SubclassId = group.First().SubclassId,
                            Levels = group.Count()
                        });
                    }
                }
            }

            return this;
        }

        // --- Read-only accessors for current state ---
        public string Name => _name;
        public string RaceId => _raceId;
        public string SubraceId => _subraceId;
        public string ClassId => _classId;
        public string SubclassId => _subclassId;
        public int Level => _level;
        public int Strength => _str;
        public int Dexterity => _dex;
        public int Constitution => _con;
        public int Intelligence => _int;
        public int Wisdom => _wis;
        public int Charisma => _cha;
        public string AbilityBonus2 => _abilityBonus2;
        public string AbilityBonus1 => _abilityBonus1;
        public IReadOnlyList<string> FeatIds => _featIds;
        public IReadOnlyList<string> MetamagicIds => _metamagicIds;
        public string BackgroundId => _backgroundId;
    }
}
