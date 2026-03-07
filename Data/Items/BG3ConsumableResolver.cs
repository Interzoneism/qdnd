using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using QDND.Combat.Actions;
using QDND.Data.Spells;
using QDND.Data.Stats;
using QDND.Data.Statuses;

namespace QDND.Data.Items
{
    /// <summary>
    /// Resolves BG3 consumable object entries into runtime-linked item definitions.
    /// </summary>
    public class BG3ConsumableResolver
    {
        private static readonly Dictionary<string, string> HealingPotionFormulas = new(StringComparer.OrdinalIgnoreCase)
        {
            ["OBJ_Potion_Healing"] = "2d4+2",
            ["OBJ_Potion_Healing_Greater"] = "4d4+4",
            ["OBJ_Potion_Healing_Superior"] = "8d4+8",
            ["OBJ_Potion_Healing_Supreme"] = "10d4+20",
        };

        private static readonly Dictionary<string, string> ExplicitStatusMappings = new(StringComparer.OrdinalIgnoreCase)
        {
            ["OBJ_Potion_Of_Hill_Giant_Strength"] = "POTION_OF_STRENGTH_HILL_GIANT",
            ["OBJ_Potion_Of_Cloud_Giant_Strength"] = "POTION_OF_STRENGTH_CLOUD_GIANT",
            ["OBJ_Potion_Of_Animal_Speaking"] = "POTION_OF_ANIMAL_SPEAKING",
            ["OBJ_Potion_Of_Fire_Breath"] = "POTION_OF_FIRE_BREATH",
            ["OBJ_Antitoxin"] = "ANTITOXIN",
            ["OBJ_Potion_Sleep"] = "SLEEP",
            ["OBJ_Potion_Poison_A"] = "POISONED",
        };

        private readonly ActionRegistry _actionRegistry;
        private readonly StatusRegistry _statusRegistry;

        public BG3ConsumableResolver(ActionRegistry actionRegistry, StatusRegistry statusRegistry = null)
        {
            _actionRegistry = actionRegistry;
            _statusRegistry = statusRegistry;
        }

        public ItemDefinition Resolve(BG3ObjectData obj, ItemUseCategory category)
        {
            if (obj == null || string.IsNullOrWhiteSpace(obj.Name))
                return null;

            string normalizedObjectName = NormalizeObjectName(obj.Name);
            var definition = new ItemDefinition
            {
                Id = obj.Name,
                DisplayName = BuildDisplayName(obj.Name, category),
                Description = string.Empty,
                UseCategory = category,
                Rarity = obj.Rarity,
                ObjectCategory = obj.ObjectCategory,
                InventoryTab = obj.InventoryTab,
                Weight = obj.Weight,
                ValueLevel = obj.ValueLevel,
                UseCosts = obj.UseCosts,
                UseConditions = obj.UseConditions,
                IsConsumable = category is not ItemUseCategory.None and not ItemUseCategory.Arrow,
                MaxStackSize = GetDefaultStackSize(category),
                UseActionId = $"use_{normalizedObjectName}",
                IconPath = GetRawProperty(obj, "Icon"),
                Boosts = obj.Boosts,
                DefaultBoosts = obj.DefaultBoosts,
                PassivesOnEquip = obj.PassivesOnEquip,
                StatusOnEquip = obj.StatusOnEquip,
            };

            switch (category)
            {
                case ItemUseCategory.Scroll:
                    ResolveScroll(obj, definition);
                    break;
                case ItemUseCategory.Potion:
                case ItemUseCategory.Consumable:
                case ItemUseCategory.Throwable:
                case ItemUseCategory.Grenade:
                    ResolvePotionOrConsumable(obj, definition);
                    break;
            }

            if (string.IsNullOrWhiteSpace(definition.Description))
                definition.Description = "Usable consumable item";

            return definition;
        }

        private void ResolveScroll(BG3ObjectData obj, ItemDefinition definition)
        {
            string rawSpellName = ExtractScrollSpellName(obj.Name);
            string normalizedSpellName = SpellUpcastRules.NormalizeBG3SpellId(rawSpellName);
            if (string.IsNullOrWhiteSpace(normalizedSpellName))
                normalizedSpellName = NormalizeObjectName(rawSpellName);

            definition.UseActionId = $"scroll_{normalizedSpellName}";

            var linkedSpell = ResolveLinkedScrollAction(rawSpellName, normalizedSpellName);
            if (linkedSpell == null)
            {
                Console.WriteLine($"[BG3ConsumableResolver] Warning: could not resolve scroll spell for '{obj.Name}'");
                definition.DisplayName = BuildDisplayName(obj.Name, ItemUseCategory.Scroll);
                definition.Description = "Casts a spell from this scroll";
                return;
            }

            definition.LinkedSpellId = linkedSpell.Id;
            definition.DisplayName = $"Scroll: {linkedSpell.Name}";
            definition.Description = !string.IsNullOrWhiteSpace(linkedSpell.Description)
                ? linkedSpell.Description
                : $"Casts {linkedSpell.Name}";
            if (string.IsNullOrWhiteSpace(definition.IconPath))
                definition.IconPath = linkedSpell.Icon;
        }

        private void ResolvePotionOrConsumable(BG3ObjectData obj, ItemDefinition definition)
        {
            if (HealingPotionFormulas.TryGetValue(obj.Name, out var healingFormula))
            {
                definition.HealingFormula = healingFormula;
                definition.Description = $"Heals {healingFormula} hit points";
                return;
            }

            string statusId = ResolveStatusIdForObject(obj.Name);
            if (!string.IsNullOrWhiteSpace(statusId))
            {
                definition.LinkedStatusId = statusId;
                definition.Description = BuildStatusDescription(statusId);
            }
            else
            {
                definition.Description = BuildDisplayName(obj.Name, definition.UseCategory);
            }
        }

        private ActionDefinition ResolveLinkedScrollAction(string rawSpellName, string normalizedSpellName)
        {
            if (_actionRegistry == null)
                return null;

            var candidates = new List<string>
            {
                normalizedSpellName,
                rawSpellName,
                $"Projectile_{rawSpellName}",
                $"Target_{rawSpellName}",
                $"Zone_{rawSpellName}",
            };

            var tried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                if (tried.Add(candidate))
                {
                    var action = _actionRegistry.GetAction(candidate);
                    if (action != null)
                        return action;
                }

                string normalized = SpellUpcastRules.NormalizeBG3SpellId(candidate);
                if (!string.IsNullOrWhiteSpace(normalized) && tried.Add(normalized))
                {
                    var action = _actionRegistry.GetAction(normalized);
                    if (action != null)
                        return action;
                }
            }

            return null;
        }

        private string ResolveStatusIdForObject(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId))
                return null;

            if (ExplicitStatusMappings.TryGetValue(objectId, out var mappedStatus))
                return mappedStatus;

            if (objectId.StartsWith("OBJ_Potion_Of_", StringComparison.OrdinalIgnoreCase))
            {
                string suffix = objectId["OBJ_Potion_Of_".Length..];

                if (suffix.EndsWith("_Resistance", StringComparison.OrdinalIgnoreCase))
                {
                    string resistanceType = suffix[..^"_Resistance".Length].ToUpperInvariant();
                    return $"POTION_OF_RESISTANCE_{resistanceType}";
                }

                return $"POTION_OF_{suffix.ToUpperInvariant()}";
            }

            if (objectId.StartsWith("OBJ_Potion_Poison", StringComparison.OrdinalIgnoreCase))
                return "POISONED";

            return null;
        }

        private string BuildStatusDescription(string statusId)
        {
            if (statusId.StartsWith("POTION_OF_RESISTANCE_", StringComparison.OrdinalIgnoreCase))
            {
                string resistanceType = statusId["POTION_OF_RESISTANCE_".Length..]
                    .Replace('_', ' ')
                    .ToLowerInvariant();
                return $"Gain resistance to {resistanceType} damage";
            }

            var status = _statusRegistry?.GetStatus(statusId);
            if (!string.IsNullOrWhiteSpace(status?.Description))
                return status.Description;

            string displayName = !string.IsNullOrWhiteSpace(status?.DisplayName)
                ? status.DisplayName
                : HumanizeIdentifier(statusId);

            return $"Applies {displayName}";
        }

        private static string BuildDisplayName(string objectId, ItemUseCategory category)
        {
            if (string.IsNullOrWhiteSpace(objectId))
                return "Unknown Item";

            if (objectId.StartsWith("OBJ_Scroll_", StringComparison.OrdinalIgnoreCase))
            {
                string spellToken = objectId["OBJ_Scroll_".Length..];
                return $"Scroll of {HumanizeIdentifier(spellToken)}";
            }

            if (objectId.StartsWith("OBJ_Potion_Of_", StringComparison.OrdinalIgnoreCase))
            {
                string potionToken = objectId["OBJ_Potion_Of_".Length..];
                return $"Potion of {HumanizeIdentifier(potionToken)}";
            }

            if (objectId.StartsWith("OBJ_Potion_", StringComparison.OrdinalIgnoreCase))
            {
                string potionToken = objectId["OBJ_Potion_".Length..];
                return $"Potion of {HumanizeIdentifier(potionToken)}";
            }

            string withoutPrefix = objectId.StartsWith("OBJ_", StringComparison.OrdinalIgnoreCase)
                ? objectId[4..]
                : objectId;

            string prefix = category switch
            {
                ItemUseCategory.Scroll => "Scroll",
                ItemUseCategory.Potion => "Potion",
                _ => null,
            };

            if (!string.IsNullOrWhiteSpace(prefix) && !withoutPrefix.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return $"{prefix} {HumanizeIdentifier(withoutPrefix)}";

            return HumanizeIdentifier(withoutPrefix);
        }

        private static string ExtractScrollSpellName(string objectId)
        {
            if (objectId.StartsWith("OBJ_Scroll_", StringComparison.OrdinalIgnoreCase))
                return objectId["OBJ_Scroll_".Length..];

            if (objectId.StartsWith("OBJ_", StringComparison.OrdinalIgnoreCase))
                return objectId[4..];

            return objectId;
        }

        private static string NormalizeObjectName(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId))
                return string.Empty;

            string stripped = objectId.StartsWith("OBJ_", StringComparison.OrdinalIgnoreCase)
                ? objectId[4..]
                : objectId;

            string spaced = Regex.Replace(stripped, @"([a-z\d])([A-Z])", "$1_$2");
            spaced = spaced.Replace('-', '_').Replace(' ', '_');
            spaced = Regex.Replace(spaced, "_+", "_");

            return spaced.Trim('_').ToLowerInvariant();
        }

        private static string HumanizeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string spaced = value.Replace('_', ' ');
            spaced = Regex.Replace(spaced, @"([a-z\d])([A-Z])", "$1 $2");
            spaced = Regex.Replace(spaced, @"\s+", " ").Trim();

            string[] words = spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var lowerCaseWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "of", "and", "the", "to", "for", "a", "an"
            };

            for (int i = 0; i < words.Length; i++)
            {
                string lower = words[i].ToLowerInvariant();
                if (i > 0 && lowerCaseWords.Contains(lower))
                {
                    words[i] = lower;
                }
                else
                {
                    words[i] = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(lower);
                }
            }

            return string.Join(" ", words);
        }

        private static string GetRawProperty(BG3ObjectData obj, string propertyName)
        {
            if (obj?.RawProperties == null || string.IsNullOrWhiteSpace(propertyName))
                return string.Empty;

            return obj.RawProperties.TryGetValue(propertyName, out var value) ? value : string.Empty;
        }

        private static int GetDefaultStackSize(ItemUseCategory category)
        {
            return category switch
            {
                ItemUseCategory.Potion => 10,
                ItemUseCategory.Scroll => 10,
                ItemUseCategory.Consumable => 10,
                ItemUseCategory.Throwable => 5,
                ItemUseCategory.Grenade => 5,
                ItemUseCategory.Arrow => 40,
                _ => 1,
            };
        }
    }
}