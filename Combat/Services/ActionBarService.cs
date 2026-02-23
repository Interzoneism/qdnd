using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Actions;
using QDND.Combat.UI;
using QDND.Data.Passives;
using QDND.Combat.Statuses;

namespace QDND.Combat.Services
{
    internal sealed class ActionBarService
    {
        private readonly CombatContext _combatContext;
        private readonly ActionRegistry _actionRegistry;
        private readonly ActionBarModel _actionBarModel;
        private readonly PassiveRegistry _passiveRegistry;
        private readonly EffectPipeline _effectPipeline;
        private readonly Action<string, string> _logOnce;

        private readonly Dictionary<string, Dictionary<int, string>> _actionBarSlotOverrides = new();

        public ActionBarService(
            CombatContext context,
            ActionRegistry actionRegistry,
            ActionBarModel actionBarModel,
            PassiveRegistry passiveRegistry,
            EffectPipeline effectPipeline,
            Action<string, string> logOnce)
        {
            _combatContext = context;
            _actionRegistry = actionRegistry;
            _actionBarModel = actionBarModel;
            _passiveRegistry = passiveRegistry;
            _effectPipeline = effectPipeline;
            _logOnce = logOnce ?? ((_, _) => { });
        }

        private static readonly string[][] CommonActionAliasGroups = new[]
        {
            new[] { "Target_MainHandAttack", "main_hand_attack" },
            new[] { "Projectile_MainHandAttack", "ranged_attack" },
            new[] { "Target_UnarmedStrike", "unarmed_strike" },
            new[] { "Target_OffhandAttack", "offhand_attack" },
            new[] { "Shout_Dash", "dash", "dash_action" },
            new[] { "Shout_Disengage", "disengage", "disengage_action" },
            new[] { "Shout_Dodge", "dodge_action" },
            new[] { "Shout_Hide", "hide" },
            new[] { "Target_Shove", "shove" },
            new[] { "Target_Help", "help", "help_action" },
            new[] { "Throw_Throw", "throw" },
            new[] { "Shout_Jump", "jump", "jump_action" },
            new[] { "Target_Dip", "dip" }
        };

        public List<ActionDefinition> GetActionsForCombatant(string combatantId)
        {
            // Get the combatant
            var combatant = _combatContext?.GetCombatant(combatantId);
            if (combatant == null)
            {
                _logOnce($"missing_combatant:{combatantId}",
                    $"GetActionsForCombatant: Combatant {combatantId} not found");
                return new List<ActionDefinition>();
            }

            // Filter actions to only those the combatant knows
            var actions = new List<ActionDefinition>();
            if (combatant.KnownActions != null)
            {
                foreach (var actionId in combatant.KnownActions)
                {
                    var action = _actionRegistry?.GetAction(actionId);
                    if (action != null)
                    {
                        actions.Add(action);
                    }
                    else
                    {
                        _logOnce(
                            $"missing_action:{combatantId}:{actionId}",
                            $"GetActionsForCombatant: Action {actionId} not found in any registry for {combatantId}");
                    }
                }
            }

            return actions;
        }

        private const int _actionBarColumns = 12;
        private static readonly Dictionary<string, int> PrimaryAttackSortOrder = BuildActionOrderIndex(
            BG3ActionIds.MeleeMainHand,
            BG3ActionIds.RangedMainHand,
            BG3ActionIds.MeleeOffHand,
            BG3ActionIds.RangedOffHand,
            BG3ActionIds.UnarmedStrike,
            BG3ActionIds.SneakAttack);

        private static readonly Dictionary<string, int> CommonUtilitySortOrder = BuildActionOrderIndex(
            BG3ActionIds.Jump,
            BG3ActionIds.Dash,
            BG3ActionIds.Disengage,
            BG3ActionIds.Shove,
            BG3ActionIds.Throw,
            BG3ActionIds.Help,
            BG3ActionIds.Hide,
            BG3ActionIds.Dip,
            BG3ActionIds.Dodge);

        private static Dictionary<string, int> BuildActionOrderIndex(params string[] actionIds)
        {
            var order = new Dictionary<string, int>(StringComparer.Ordinal);
            if (actionIds == null)
            {
                return order;
            }

            for (int i = 0; i < actionIds.Length; i++)
            {
                string normalized = NormalizeActionSortId(actionIds[i]);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                order[normalized] = i;
            }

            return order;
        }

        private static string NormalizeActionSortId(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
            {
                return string.Empty;
            }

            string stripped = BG3ActionIds.StripPrefix(actionId.Trim());
            Span<char> buffer = stackalloc char[stripped.Length];
            int written = 0;
            foreach (char c in stripped)
            {
                if (char.IsLetterOrDigit(c))
                {
                    buffer[written++] = char.ToLowerInvariant(c);
                }
            }

            return written > 0 ? new string(buffer[..written]) : string.Empty;
        }

        private static int ParseSpellSlotLevel(string resourceKey)
        {
            if (string.IsNullOrWhiteSpace(resourceKey))
            {
                return -1;
            }

            string normalized = resourceKey.Trim().ToLowerInvariant().Replace('-', '_');
            if (!normalized.StartsWith("spell_slot", StringComparison.Ordinal))
            {
                return -1;
            }

            string[] parts = normalized.Split('_', StringSplitOptions.RemoveEmptyEntries);
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                if (int.TryParse(parts[i], out int level) && level >= 1 && level <= 9)
                {
                    return level;
                }
            }

            return 1;
        }

        private static bool IsSpellAction(ActionDefinition action)
        {
            if (action == null)
            {
                return false;
            }

            if (action.SpellLevel > 0 ||
                action.AttackType == AttackType.MeleeSpell ||
                action.AttackType == AttackType.RangedSpell ||
                action.Components != SpellComponents.None ||
                action.School != QDND.Combat.Actions.SpellSchool.None)
            {
                return true;
            }

            var tags = action.Tags?
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToLowerInvariant())
                .ToHashSet() ?? new HashSet<string>();

            if (tags.Contains("spell") || tags.Contains("cantrip") || tags.Contains("magic"))
            {
                return true;
            }

            return action.Cost?.ResourceCosts?.Keys.Any(k =>
                k.StartsWith("spell_slot", StringComparison.OrdinalIgnoreCase)) == true;
        }

        private static int ResolveActionSpellLevel(ActionDefinition action)
        {
            if (!IsSpellAction(action))
            {
                return -1;
            }

            if (action.SpellLevel > 0)
            {
                return action.SpellLevel;
            }

            var resourceCostKeys = action.Cost?.ResourceCosts?.Keys ?? Enumerable.Empty<string>();
            int parsedLevel = resourceCostKeys
                .Select(ParseSpellSlotLevel)
                .Where(level => level > 0)
                .DefaultIfEmpty(0)
                .Min();

            if (parsedLevel > 0)
            {
                return parsedLevel;
            }

            return 0;
        }

        private static int GetActionEconomyOrder(ActionDefinition action)
        {
            if (action?.Cost == null)
            {
                return 3;
            }

            if (action.Cost.UsesAction)
            {
                return 0;
            }

            if (action.Cost.UsesBonusAction)
            {
                return 1;
            }

            if (action.Cost.UsesReaction)
            {
                return 2;
            }

            return 3;
        }

        private static int GetActionBarBucket(ActionDefinition action)
        {
            if (action == null)
            {
                return int.MaxValue;
            }

            string normalizedId = NormalizeActionSortId(action.Id);
            if (PrimaryAttackSortOrder.ContainsKey(normalizedId))
            {
                return 0;
            }

            if (CommonUtilitySortOrder.ContainsKey(normalizedId))
            {
                return 1;
            }

            if (IsSpellAction(action))
            {
                int spellLevel = ResolveActionSpellLevel(action);
                return spellLevel <= 0 ? 3 : 4;
            }

            if (ClassifyActionCategory(action) == "item")
            {
                return 5;
            }

            return 2;
        }

        private static int GetActionBarPriority(ActionDefinition action)
        {
            if (action == null)
            {
                return int.MaxValue;
            }

            string normalizedId = NormalizeActionSortId(action.Id);
            if (PrimaryAttackSortOrder.TryGetValue(normalizedId, out int attackOrder))
            {
                return attackOrder;
            }

            if (CommonUtilitySortOrder.TryGetValue(normalizedId, out int utilityOrder))
            {
                return utilityOrder;
            }

            return GetActionEconomyOrder(action) * 100;
        }

        private static List<ActionDefinition> SortActionBarAbilities(IEnumerable<ActionDefinition> actions)
        {
            if (actions == null)
            {
                return new List<ActionDefinition>();
            }

            return actions
                .Where(a => a != null && !string.IsNullOrWhiteSpace(a.Id))
                .GroupBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(GetActionBarBucket)
                .ThenBy(GetActionBarPriority)
                .ThenBy(a =>
                {
                    int spellLevel = ResolveActionSpellLevel(a);
                    return spellLevel >= 0 ? spellLevel : int.MaxValue;
                })
                .ThenBy(GetActionEconomyOrder)
                .ThenBy(a => a.Name ?? a.Id, StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private List<ActionDefinition> GetCommonActions()
        {
            var commonActions = new List<ActionDefinition>();
            var addedIds = new HashSet<string>();

            foreach (var aliases in CommonActionAliasGroups)
            {
                foreach (var id in aliases)
                {
                    var action = _actionRegistry?.GetAction(id);
                    if (action == null)
                    {
                        continue;
                    }

                    if (addedIds.Add(action.Id))
                    {
                        commonActions.Add(action);
                    }

                    // Stop at first available alias for this slot.
                    break;
                }
            }

            return commonActions;
        }

        private const string FallbackAttackIconPath = "res://assets/Images/Icons General/Generic_Physical_Unfaded_Icon.png";
        private const string FallbackSpellIconPath = "res://assets/Images/Icons General/Generic_Magical_Unfaded_Icon.png";
        private const string FallbackItemIconPath = "res://assets/Images/Icons General/Generic_Feature_Unfaded_Icon.png";
        private const string FallbackSpecialIconPath = "res://assets/Images/Icons General/Generic_Feature_Unfaded_Icon.png";

        // Ordered list of icon search folders for bare-name lookups.
        private static readonly string[] IconSearchFolders = new[]
        {
            "res://assets/Images/Icons Spells/",
            "res://assets/Images/Icons Actions/",
            "res://assets/Images/Icons Weapon Actions/",
            "res://assets/Images/Icons Passive Features/",
            "res://assets/Images/Icons Conditions/",
            "res://assets/Images/Icons General/",
            "res://assets/Images/Icons Weapons and Other/",
            "res://assets/Images/Icons Armour/",
        };

        private string ResolveIconPath(string iconName, string category = null)
        {
            if (!string.IsNullOrWhiteSpace(iconName))
            {
                iconName = iconName.Trim();
                if (iconName.StartsWith("res://", StringComparison.Ordinal))
                {
                    if (ResourceLoader.Exists(iconName))
                    {
                        return iconName;
                    }

                    // Recover common data mismatch: icon path references .webp but only .png exists.
                    if (iconName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                    {
                        string pngPath = iconName.Substring(0, iconName.Length - ".webp".Length) + ".png";
                        if (ResourceLoader.Exists(pngPath))
                        {
                            return pngPath;
                        }
                    }
                }
                else
                {
                    // Bare icon name (no res:// prefix): search known icon directories.
                    // Try both the raw name and the BG3-style _Unfaded_Icon suffix.
                    string[] candidates = iconName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                        ? new[] { iconName }
                        : new[] { iconName + "_Unfaded_Icon.png", iconName + ".png" };

                    foreach (var folder in IconSearchFolders)
                    {
                        foreach (var candidate in candidates)
                        {
                            string fullPath = folder + candidate;
                            if (ResourceLoader.Exists(fullPath))
                                return fullPath;
                        }
                    }
                }
            }

            string fallback = category switch
            {
                "spell" => FallbackSpellIconPath,
                "item" => FallbackItemIconPath,
                "special" => FallbackSpecialIconPath,
                _ => FallbackAttackIconPath
            };

            return ResourceLoader.Exists(fallback) ? fallback : string.Empty;
        }

        public void Populate(string combatantId)
        {
            var combatant = _combatContext.GetCombatant(combatantId);
            if (combatant == null)
            {
                _actionBarModel.SetActions(new List<ActionBarEntry>());
                return;
            }

            var actionDefs = GetActionsForCombatant(combatantId);
            GD.Print($"[DEBUG-ABILITIES] {combatant.Name} ({combatantId}) known={string.Join(", ", combatant.KnownActions ?? new List<string>())} resolved={string.Join(", ", actionDefs.Select(a => a.Id))}");
            var commonActions = GetCommonActions();

            // Filter out summon actions (forbidden in canonical scenarios)
            var nonSummonActions = actionDefs.Where(a => !a.IsSummon).ToList();
            var filteredCommonActions = commonActions.Where(a => !a.IsSummon).ToList();

            var finalAbilities = new List<ActionDefinition>(nonSummonActions);
            var existingIds = new HashSet<string>(nonSummonActions.Select(a => a.Id));

            // Add common actions if they are not already present
            foreach (var action in filteredCommonActions)
            {
                if (existingIds.Contains(action.Id)) continue;

                bool shouldAdd = action.Id switch
                {
                    "Target_MainHandAttack" or "main_hand_attack" => combatant.MainHandWeapon == null || !combatant.MainHandWeapon.IsRanged,
                    "Projectile_MainHandAttack" or "ranged_attack" => combatant.MainHandWeapon != null && combatant.MainHandWeapon.IsRanged,
                    "Target_UnarmedStrike" or "unarmed_strike" => combatant.MainHandWeapon == null,
                    "Target_OffhandAttack" or "offhand_attack" => combatant.OffHandWeapon != null,
                    // BG3 prefixed common actions
                    "Shout_Dash" or "Shout_Disengage" or "Shout_Dodge" or "Shout_Hide" or "Shout_Jump" or
                    "Target_Shove" or "Target_Help" or "Target_Dip" or "Throw_Throw" or
                    // Legacy lowercase common actions
                    "dash" or "dash_action" or
                    "disengage" or "disengage_action" or
                    "shove" or
                    "help" or "help_action" or
                    "jump" or "jump_action" or
                    "dodge_action" or "hide" or "throw" or "dip" => true,
                    _ => false
                };

                if (shouldAdd)
                {
                    finalAbilities.Add(action);
                    existingIds.Add(action.Id);
                }
            }

            finalAbilities = SortActionBarAbilities(finalAbilities);

            var entries = new List<ActionBarEntry>();
            int slotIndex = 0;

            foreach (var def in finalAbilities)
            {
                var category = ClassifyActionCategory(def);
                var entry = new ActionBarEntry
                {
                    ActionId = def.Id,
                    DisplayName = def.Name,
                    Description = def.Description,
                    IconPath = ResolveIconPath(def.Icon, category),
                    SlotIndex = slotIndex++,
                    ActionPointCost = def.Cost.UsesAction ? 1 : 0,
                    BonusActionCost = def.Cost.UsesBonusAction ? 1 : 0,
                    MovementCost = def.Cost.MovementCost,
                    CooldownTotal = def.Cooldown?.TurnCooldown ?? 0,
                    ChargesMax = def.Cooldown?.MaxCharges ?? 0,
                    ChargesRemaining = def.Cooldown?.MaxCharges ?? 0,
                    ResourceCosts = BuildActionBarResourceCosts(def),
                    Category = category,
                    SpellLevel = ResolveActionSpellLevel(def),
                    Usability = ActionUsability.Available,
                    Range = def.Range,
                    AreaRadius = def.AreaRadius,
                    AoEShape = def.TargetType switch
                    {
                        TargetType.Cone => "cone",
                        TargetType.Line => "line",
                        TargetType.Circle => "sphere",
                        _ => null
                    },
                    SaveType = def.SaveType,
                    SaveDC = ComputeTooltipSaveDC(def, combatant?.ProficiencyBonus ?? 0),
                    SpellSchool = def.School != SpellSchool.None ? def.School.ToString() : null,
                    RequiresConcentration = def.RequiresConcentration,
                    DamageSummary = BuildDamageSummary(def),
                };
                entries.Add(entry);
            }

            // Add toggleable passives
            if (combatant.PassiveManager != null)
            {
                var toggleables = combatant.PassiveManager.GetToggleablePassives();

                foreach (var passiveId in toggleables)
                {
                    if (_passiveRegistry == null)
                        continue;

                    var passive = _passiveRegistry.GetPassive(passiveId);
                    if (passive == null)
                        continue;

                    var toggleEntry = new ActionBarEntry
                    {
                        ActionId = $"passive:{passiveId}",
                        DisplayName = passive.DisplayName ?? passiveId,
                        Description = passive.Description ?? "",
                        IconPath = ResolveIconPath(passive.Icon, "special"),
                        SlotIndex = slotIndex++,
                        ActionPointCost = 0,
                        BonusActionCost = 0,
                        MovementCost = 0,
                        Category = "passive",
                        Usability = ActionUsability.Available,
                        IsToggle = true,
                        IsToggledOn = combatant.PassiveManager.IsToggled(passiveId),
                        ToggleGroup = passive.ToggleGroup
                    };
                    entries.Add(toggleEntry);
                }
            }

            // Mark concentration-active entry (Step 8.3A)
            var concentrationSystem = _combatContext?.GetService<ConcentrationSystem>();
            if (concentrationSystem != null)
            {
                var concentratedActionId = concentrationSystem.GetConcentratedEffect(combatantId)?.ActionId;
                if (!string.IsNullOrEmpty(concentratedActionId))
                {
                    var concentratedEntry = entries.FirstOrDefault(e =>
                        string.Equals(e.ActionId, concentratedActionId, StringComparison.OrdinalIgnoreCase));
                    if (concentratedEntry != null)
                        concentratedEntry.IsConcentrationActive = true;
                }
            }

            ApplyActionBarSlotOverrides(combatantId, entries);

            _actionBarModel.SetActions(entries);
            RefreshUsability(combatantId);
        }

        private void ApplyActionBarSlotOverrides(string combatantId, List<ActionBarEntry> entries)
        {
            if (entries == null || entries.Count == 0 || string.IsNullOrWhiteSpace(combatantId))
            {
                return;
            }

            _actionBarSlotOverrides.TryGetValue(combatantId, out var overrideMap);

            // Ensure one unique entry per action ID.
            var uniqueEntries = entries
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.ActionId))
                .GroupBy(e => e.ActionId)
                .Select(g => g.First())
                .ToList();

            var entryByActionId = uniqueEntries.ToDictionary(e => e.ActionId, e => e, StringComparer.Ordinal);
            var assignedSlots = new HashSet<int>();

            if (overrideMap != null)
            {
                foreach (var kvp in overrideMap.OrderBy(k => k.Key))
                {
                    if (!entryByActionId.TryGetValue(kvp.Value, out var entry))
                    {
                        continue;
                    }

                    int slotIndex = Math.Max(0, kvp.Key);
                    if (assignedSlots.Contains(slotIndex))
                    {
                        continue;
                    }

                    entry.SlotIndex = slotIndex;
                    assignedSlots.Add(slotIndex);
                    entryByActionId.Remove(kvp.Value);
                }
            }

            int nextSlot = 0;
            foreach (var entry in uniqueEntries.Where(e => entryByActionId.ContainsKey(e.ActionId)).OrderBy(e => e.SlotIndex))
            {
                while (assignedSlots.Contains(nextSlot))
                {
                    nextSlot++;
                }

                entry.SlotIndex = nextSlot;
                assignedSlots.Add(nextSlot);
            }

            entries.Clear();
            entries.AddRange(uniqueEntries.OrderBy(e => e.SlotIndex));

            PersistActionBarSlotOverrides(combatantId, entries);
        }

        private void PersistActionBarSlotOverrides(string combatantId, IEnumerable<ActionBarEntry> entries)
        {
            if (string.IsNullOrWhiteSpace(combatantId) || entries == null)
            {
                return;
            }

            var map = new Dictionary<int, string>();
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.ActionId))
                {
                    continue;
                }

                map[Math.Max(0, entry.SlotIndex)] = entry.ActionId;
            }

            _actionBarSlotOverrides[combatantId] = map;
        }

        public void ReorderSlots(string combatantId, int fromSlot, int toSlot)
        {
            if (_actionBarModel == null || string.IsNullOrWhiteSpace(combatantId))
            {
                return;
            }

            if (fromSlot < 0 || toSlot < 0 || fromSlot == toSlot)
            {
                return;
            }

            var fromEntry = _actionBarModel.Actions.FirstOrDefault(a => a.SlotIndex == fromSlot);
            if (fromEntry == null || string.IsNullOrWhiteSpace(fromEntry.ActionId))
            {
                return;
            }

            var mutableEntries = _actionBarModel.Actions
                .Where(a => a != null && !string.IsNullOrWhiteSpace(a.ActionId))
                .Select(a => new ActionBarEntry
                {
                    ActionId = a.ActionId,
                    DisplayName = a.DisplayName,
                    Description = a.Description,
                    IconPath = a.IconPath,
                    SlotIndex = a.SlotIndex,
                    Hotkey = a.Hotkey,
                    Usability = a.Usability,
                    UsabilityReason = a.UsabilityReason,
                    ActionPointCost = a.ActionPointCost,
                    BonusActionCost = a.BonusActionCost,
                    MovementCost = a.MovementCost,
                    ResourceCosts = a.ResourceCosts != null ? new Dictionary<string, int>(a.ResourceCosts) : new Dictionary<string, int>(),
                    CooldownRemaining = a.CooldownRemaining,
                    CooldownTotal = a.CooldownTotal,
                    ChargesRemaining = a.ChargesRemaining,
                    ChargesMax = a.ChargesMax,
                    Category = a.Category,
                    IsToggle = a.IsToggle,
                    IsToggledOn = a.IsToggledOn,
                    ToggleGroup = a.ToggleGroup,
                })
                .ToList();

            var mutableFrom = mutableEntries.FirstOrDefault(a => a.SlotIndex == fromSlot);
            if (mutableFrom == null)
            {
                return;
            }

            var mutableTo = mutableEntries.FirstOrDefault(a => a.SlotIndex == toSlot);
            mutableFrom.SlotIndex = toSlot;
            if (mutableTo != null)
            {
                mutableTo.SlotIndex = fromSlot;
            }

            mutableEntries = mutableEntries
                .OrderBy(e => e.SlotIndex)
                .ThenBy(e => e.ActionId, StringComparer.Ordinal)
                .ToList();

            PersistActionBarSlotOverrides(combatantId, mutableEntries);
            _actionBarModel.SetActions(mutableEntries);
            RefreshUsability(combatantId);
        }

        private static string ClassifyActionCategory(ActionDefinition action)
        {
            if (action == null)
            {
                return "attack";
            }

            var tags = action.Tags?
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToLowerInvariant())
                .ToHashSet() ?? new HashSet<string>();

            if (IsSpellAction(action))
            {
                return "spell";
            }

            if (tags.Contains("item") ||
                tags.Contains("consumable") ||
                tags.Contains("potion") ||
                tags.Contains("scroll"))
            {
                return "item";
            }

            if (action.AttackType == AttackType.MeleeWeapon ||
                action.AttackType == AttackType.RangedWeapon ||
                action.Cost?.UsesAction == true ||
                action.Cost?.UsesBonusAction == true ||
                action.Cost?.UsesReaction == true)
            {
                return "attack";
            }

            return "attack";
        }

        private static Dictionary<string, int> BuildActionBarResourceCosts(ActionDefinition action)
        {
            var costs = action?.Cost?.ResourceCosts != null
                ? new Dictionary<string, int>(action.Cost.ResourceCosts)
                : new Dictionary<string, int>();

            if (action?.Cost?.UsesReaction == true)
            {
                if (costs.ContainsKey("reaction"))
                {
                    costs["reaction"] = Math.Max(costs["reaction"], 1);
                }
                else
                {
                    costs["reaction"] = 1;
                }
            }

            return costs;
        }

        /// <summary>
        /// Public entry point for external callers (e.g. UIAwareAIController) to
        /// force an action-bar usability refresh after resource-modifying abilities.
        /// </summary>
        public void RequestRefresh(string combatantId)
        {
            RefreshUsability(combatantId);
        }

        public void RefreshUsability(string combatantId)
        {
            if (_actionBarModel == null || _effectPipeline == null || string.IsNullOrEmpty(combatantId))
            {
                return;
            }

            var combatant = _combatContext?.GetCombatant(combatantId);
            if (combatant == null)
            {
                return;
            }

            foreach (var action in _actionBarModel.Actions)
            {
                if (action == null || string.IsNullOrEmpty(action.ActionId))
                {
                    continue;
                }

                // Toggle passives are not routed through action effect execution.
                if (action.ActionId.StartsWith("passive:", StringComparison.Ordinal))
                {
                    // Suppress passives in the same toggle group when another is active (Step 8.5)
                    if (!string.IsNullOrEmpty(action.ToggleGroup) && !action.IsToggledOn
                        && combatant.PassiveManager != null)
                    {
                        string passiveId = action.ActionId.Substring("passive:".Length);
                        bool anotherActive = combatant.PassiveManager.GetToggleablePassives()
                            .Where(pid => !string.Equals(pid, passiveId, StringComparison.Ordinal))
                            .Any(pid =>
                            {
                                var p = _passiveRegistry?.GetPassive(pid);
                                return p?.ToggleGroup == action.ToggleGroup
                                    && combatant.PassiveManager.IsToggled(pid);
                            });
                        if (anotherActive)
                        {
                            _actionBarModel.UpdateUsability(action.ActionId, ActionUsability.Disabled, "Another stance is active");
                            continue;
                        }
                    }
                    _actionBarModel.UpdateUsability(action.ActionId, ActionUsability.Available, null);
                    continue;
                }

                var (canUseAbility, reason) = _effectPipeline.CanUseAbility(action.ActionId, combatant);
                ActionUsability usability = MapActionUsability(canUseAbility, reason);

                _actionBarModel.UpdateUsability(action.ActionId, usability, reason);
            }
        }

        private static ActionUsability MapActionUsability(bool canUseAbility, string reason)
        {
            if (canUseAbility)
            {
                return ActionUsability.Available;
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                return ActionUsability.Disabled;
            }

            if (reason.Contains("cooldown", StringComparison.OrdinalIgnoreCase))
            {
                return ActionUsability.OnCooldown;
            }

            if (reason.Contains("used", StringComparison.OrdinalIgnoreCase))
            {
                return ActionUsability.Used;
            }

            if (reason.Contains("target", StringComparison.OrdinalIgnoreCase))
            {
                return ActionUsability.NoTargets;
            }

            bool isResourceFailure =
                reason.Contains("No action", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("No bonus action", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("No reaction", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("Insufficient movement", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("resource", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("cost", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("spell slot", StringComparison.OrdinalIgnoreCase);

            return isResourceFailure ? ActionUsability.NoResources : ActionUsability.Disabled;
        }

        private static int ComputeTooltipSaveDC(ActionDefinition action, int proficiencyBonus)
        {
            if (action.SaveDC.HasValue)
                return action.SaveDC.Value;
            if (string.IsNullOrEmpty(action.SaveType))
                return 0;
            return 8 + Math.Max(0, proficiencyBonus) + action.SaveDCBonus;
        }

        private static string BuildDamageSummary(ActionDefinition action)
        {
            var dmgEffect = action?.Effects?.FirstOrDefault(e =>
                string.Equals(e.Type, "damage", StringComparison.OrdinalIgnoreCase));
            if (dmgEffect == null)
                return null;
            if (!string.IsNullOrEmpty(dmgEffect.DiceFormula) && !string.IsNullOrEmpty(dmgEffect.DamageType))
                return $"{dmgEffect.DiceFormula} {dmgEffect.DamageType}";
            if (!string.IsNullOrEmpty(dmgEffect.DiceFormula))
                return dmgEffect.DiceFormula;
            return null;
        }
    }
}
