using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.UI;
using QDND.Data;
using QDND.Data.Actions;
using QDND.Data.Passives;
using QDND.Combat.Statuses;
using QDND.Combat.UI.Base;

namespace QDND.Combat.Services
{
    internal sealed class ActionBarService
    {
        private readonly ICombatantRegistry _combatants;
        private readonly ActionRegistry _actionRegistry;
        private readonly ActionBarModel _actionBarModel;
        private readonly PassiveRegistry _passiveRegistry;
        private readonly EffectPipeline _effectPipeline;
        private readonly InventoryService _inventoryService;
        private readonly ConcentrationSystem _concentrationSystem;
        private readonly QDND.Data.CharacterModel.CharacterDataRegistry _characterDataRegistry;
        private readonly Action<string, string> _logOnce;

        private readonly Dictionary<string, Dictionary<int, string>> _actionBarSlotOverrides = new();

        public ActionBarService(
            ICombatantRegistry combatants,
            ActionRegistry actionRegistry,
            ActionBarModel actionBarModel,
            PassiveRegistry passiveRegistry,
            EffectPipeline effectPipeline,
            InventoryService inventoryService,
            ConcentrationSystem concentrationSystem,
            QDND.Data.CharacterModel.CharacterDataRegistry characterDataRegistry,
            Action<string, string> logOnce)
        {
            _combatants = combatants;
            _actionRegistry = actionRegistry;
            _actionBarModel = actionBarModel;
            _passiveRegistry = passiveRegistry;
            _effectPipeline = effectPipeline;
            _inventoryService = inventoryService;
            _concentrationSystem = concentrationSystem;
            _characterDataRegistry = characterDataRegistry;
            _logOnce = logOnce ?? ((_, _) => { });
        }

        private static readonly string[][] CommonActionAliasGroups = new[]
        {
            new[] { "main_hand_attack", "Target_MainHandAttack" },
            new[] { "ranged_attack", "Projectile_MainHandAttack" },
            new[] { "unarmed_strike", "Target_UnarmedAttack", "Target_UnarmedStrike" },
            new[] { "offhand_attack", "Target_OffhandAttack", "Target_OffHandAttack" },
            new[] { "ranged_offhand_attack", "Projectile_OffhandAttack", "Projectile_OffHandAttack" },
            new[] { "dash", "dash_action", "Shout_Dash" },
            new[] { "disengage", "disengage_action", "Shout_Disengage" },
            new[] { "dodge_action", "Shout_Dodge" },
            new[] { "hide", "hide_action", "Shout_Hide" },
            new[] { "shove", "Target_Shove" },
            new[] { "help", "help_action", "Target_Help" },
            new[] { "throw", "throw_action", "Throw_Throw", "Target_Throw" },
            new[] { "jump", "jump_action", "Shout_Jump" },
            new[] { "dip", "dip_action", "Target_Dip" }
        };

        public List<ActionDefinition> GetActionsForCombatant(string combatantId)
        {
            // Get the combatant
            var combatant = _combatants?.Get(combatantId);
            if (combatant == null)
            {
                _logOnce($"missing_combatant:{combatantId}",
                    $"GetActionsForCombatant: Combatant {combatantId} not found");
                return new List<ActionDefinition>();
            }

            // Filter actions to only those the combatant knows
            var actions = new List<ActionDefinition>();
            var seenActionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (combatant.KnownActions != null)
            {
                foreach (var actionId in combatant.KnownActions)
                {
                    var action = _actionRegistry?.GetAction(actionId);
                    if (action != null)
                    {
                        if (seenActionIds.Add(action.Id))
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

            if (_inventoryService != null)
            {
                var usableItems = _inventoryService.GetUsableItems(combatantId);
                foreach (var item in usableItems)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.UseActionId) || item.Quantity <= 0)
                        continue;

                    var itemAction = _actionRegistry?.GetAction(item.UseActionId);
                    if (itemAction == null)
                    {
                        _logOnce(
                            $"missing_item_action:{combatantId}:{item.UseActionId}",
                            $"GetActionsForCombatant: Item action {item.UseActionId} not found for {combatantId}");
                        continue;
                    }

                    if (seenActionIds.Add(itemAction.Id))
                        actions.Add(itemAction);
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

        private string ResolveIconPath(string iconName, string category = null)
        {
            if (HudIcons.TryResolveIconPath(iconName, out var resolvedPath))
                return resolvedPath;

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
            var combatant = _combatants?.Get(combatantId);
            if (combatant == null)
            {
                _actionBarModel.SetActions(new List<ActionBarEntry>());
                return;
            }

            // Preserve the originally requested known-action IDs (including aliases)
            // so AI/UI lookups can match action-bar entries while deduping by canonical ID.
            var requestedKnownActionIdsByCanonicalId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (combatant.KnownActions != null)
            {
                foreach (var requestedActionId in combatant.KnownActions)
                {
                    if (string.IsNullOrWhiteSpace(requestedActionId))
                    {
                        continue;
                    }

                    var resolvedAction = _actionRegistry?.GetAction(requestedActionId);
                    if (resolvedAction == null || string.IsNullOrWhiteSpace(resolvedAction.Id))
                    {
                        continue;
                    }

                    if (!requestedKnownActionIdsByCanonicalId.ContainsKey(resolvedAction.Id))
                    {
                        requestedKnownActionIdsByCanonicalId[resolvedAction.Id] = requestedActionId;
                    }
                }
            }

            var actionDefs = GetActionsForCombatant(combatantId);
            RuntimeSafety.Log($"[DEBUG-ABILITIES] {combatant.Name} ({combatantId}) known={string.Join(", ", combatant.KnownActions ?? new List<string>())} resolved={string.Join(", ", actionDefs.Select(a => a.Id))}");
            var commonActions = GetCommonActions();

            var usableItemsByActionId = new Dictionary<string, InventoryItem>(StringComparer.OrdinalIgnoreCase);
            var itemQuantityByActionId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (_inventoryService != null)
            {
                foreach (var item in _inventoryService.GetUsableItems(combatantId))
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.UseActionId) || item.Quantity <= 0)
                        continue;

                    if (!usableItemsByActionId.ContainsKey(item.UseActionId))
                        usableItemsByActionId[item.UseActionId] = item;

                    if (!itemQuantityByActionId.TryGetValue(item.UseActionId, out int currentQty))
                        currentQty = 0;

                    itemQuantityByActionId[item.UseActionId] = currentQty + Math.Max(0, item.Quantity);
                }
            }

            // Filter out internal summon-command actions (IsSummon = true on actions like
            // Hound of Ill Omen or Accursed Specter that ARE the summon mechanic themselves).
            // Castable spells that have a summon *effect* (e.g., flaming_sphere) do NOT carry
            // IsSummon = true and must pass through so they appear on the action bar.
            var nonSummonActions = actionDefs
                .Where(a => !a.IsSummon)
                .Where(a =>
                {
                    if (!string.Equals(ClassifyActionCategory(a), "item", StringComparison.OrdinalIgnoreCase))
                        return true;

                    return usableItemsByActionId.ContainsKey(a.Id);
                })
                .ToList();
            var filteredCommonActions = commonActions.Where(a => !a.IsSummon).ToList();

            var finalAbilities = new List<ActionDefinition>(nonSummonActions);
            var existingIds = new HashSet<string>(nonSummonActions.Select(a => a.Id));

            // Add common actions if they are not already present
            foreach (var action in filteredCommonActions)
            {
                if (existingIds.Contains(action.Id)) continue;

                bool shouldAdd = action.Id switch
                {
                    "main_hand_attack" or "Target_MainHandAttack" => combatant.MainHandWeapon == null || !combatant.MainHandWeapon.IsRanged,
                    "ranged_attack" or "Projectile_MainHandAttack" => combatant.MainHandWeapon != null && combatant.MainHandWeapon.IsRanged,
                    "unarmed_strike" or "Target_UnarmedAttack" or "Target_UnarmedStrike" => combatant.MainHandWeapon == null,
                    "offhand_attack" or "Target_OffhandAttack" or "Target_OffHandAttack" => combatant.OffHandWeapon != null,
                    "ranged_offhand_attack" or "Projectile_OffhandAttack" or "Projectile_OffHandAttack" =>
                        combatant.OffHandWeapon != null && combatant.OffHandWeapon.IsRanged,
                    // Canonical common actions
                    "dash" or "dash_action" or
                    "disengage" or "disengage_action" or
                    "shove" or
                    "help" or "help_action" or
                    "jump" or "jump_action" or
                    "dodge_action" or "hide" or "hide_action" or "throw" or "throw_action" or "dip" or "dip_action" or
                    // Legacy BG3-prefixed aliases
                    "Shout_Dash" or "Shout_Disengage" or "Shout_Dodge" or "Shout_Hide" or "Shout_Jump" or
                    "Target_Shove" or "Target_Help" or "Target_Dip" or "Throw_Throw" or "Target_Throw" => true,
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
                usableItemsByActionId.TryGetValue(def.Id, out var itemInstance);
                bool isItem = string.Equals(category, "item", StringComparison.OrdinalIgnoreCase)
                    && itemInstance != null;
                int itemCharges = isItem && itemQuantityByActionId.TryGetValue(def.Id, out int qty)
                    ? qty
                    : 0;
                string entryActionId = requestedKnownActionIdsByCanonicalId.TryGetValue(def.Id, out var requestedActionId)
                    && !string.IsNullOrWhiteSpace(requestedActionId)
                    ? requestedActionId
                    : def.Id;

                var entry = new ActionBarEntry
                {
                    ActionId = entryActionId,
                    DisplayName = def.Name,
                    Description = isItem ? BuildItemActionDescription(def, itemInstance, itemCharges) : BuildActionDescription(def, combatant),
                    IconPath = isItem && !string.IsNullOrWhiteSpace(itemInstance.IconPath)
                        ? itemInstance.IconPath
                        : ResolveIconPath(def.Icon, category),
                    SlotIndex = slotIndex++,
                    ActionPointCost = def.Cost.UsesAction ? 1 : 0,
                    BonusActionCost = def.Cost.UsesBonusAction ? 1 : 0,
                    MovementCost = def.Cost.MovementCost,
                    CooldownTotal = def.Cooldown?.TurnCooldown ?? 0,
                    ChargesMax = isItem ? itemCharges : (def.Cooldown?.MaxCharges ?? 0),
                    ChargesRemaining = isItem ? itemCharges : (def.Cooldown?.MaxCharges ?? 0),
                    ResourceCosts = BuildActionBarResourceCosts(def),
                    Category = category,
                    ItemInstanceId = isItem ? itemInstance.InstanceId : null,
                    ItemDefinitionId = isItem ? itemInstance.DefinitionId : null,
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
                    SaveDC = ComputeTooltipSaveDC(def, combatant?.ProficiencyBonus ?? 0, combatant),
                    SpellSchool = def.School != SpellSchool.None ? def.School.ToString() : null,
                    RequiresConcentration = def.RequiresConcentration,
                    DamageSummary = BuildDamageSummary(def, combatant),
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
            var concentrationSystem = _concentrationSystem;
            if (concentrationSystem != null)
            {
                var concentratedActionId = concentrationSystem.GetConcentratedEffect(combatantId)?.ActionId;
                if (!string.IsNullOrEmpty(concentratedActionId))
                {
                    var concentratedEntry = entries.FirstOrDefault(e =>
                        string.Equals(e.ActionId, concentratedActionId, StringComparison.OrdinalIgnoreCase));

                    if (concentratedEntry == null
                        && requestedKnownActionIdsByCanonicalId.TryGetValue(concentratedActionId, out var requestedConcentratedActionId)
                        && !string.IsNullOrWhiteSpace(requestedConcentratedActionId))
                    {
                        concentratedEntry = entries.FirstOrDefault(e =>
                            string.Equals(e.ActionId, requestedConcentratedActionId, StringComparison.OrdinalIgnoreCase));
                    }

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
                    ItemInstanceId = a.ItemInstanceId,
                    ItemDefinitionId = a.ItemDefinitionId,
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

        public void AssignItemToSlot(string combatantId, string itemInstanceId, int targetSlot)
        {
            if (string.IsNullOrWhiteSpace(combatantId) || string.IsNullOrWhiteSpace(itemInstanceId) || targetSlot < 0)
            {
                return;
            }

            if (_inventoryService == null)
            {
                return;
            }

            var item = _inventoryService.GetUsableItems(combatantId)
                .FirstOrDefault(i => i != null && string.Equals(i.InstanceId, itemInstanceId, StringComparison.Ordinal));

            if (item == null || string.IsNullOrWhiteSpace(item.UseActionId))
            {
                return;
            }

            var itemAction = _actionRegistry?.GetAction(item.UseActionId);
            if (itemAction == null || string.IsNullOrWhiteSpace(itemAction.Id))
            {
                return;
            }

            if (!_actionBarSlotOverrides.TryGetValue(combatantId, out var overrideMap))
            {
                overrideMap = new Dictionary<int, string>();
                _actionBarSlotOverrides[combatantId] = overrideMap;
            }

            overrideMap[targetSlot] = itemAction.Id;
            Populate(combatantId);
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

            var combatant = _combatants?.Get(combatantId);
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

        private string BuildActionDescription(ActionDefinition action, Combatant combatant)
        {
            if (action == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(action.Description))
            {
                return action.Description;
            }

            var sentences = new List<string>();
            if (IsSpellAction(action) && action.School != SpellSchool.None)
            {
                sentences.Add($"{action.School} spell.");
            }

            string damage = BuildDamageSummary(action, combatant);
            string healing = BuildHealingSummary(action, combatant);

            if (!string.IsNullOrWhiteSpace(damage) && !string.IsNullOrWhiteSpace(healing))
            {
                sentences.Add($"Deals {damage} damage and heals {healing} hit points.");
            }
            else if (!string.IsNullOrWhiteSpace(damage))
            {
                sentences.Add($"Deals {damage} damage.");
            }
            else if (!string.IsNullOrWhiteSpace(healing))
            {
                sentences.Add($"Heals {healing} hit points.");
            }
            else
            {
                string intentSentence = action.Intent switch
                {
                    VerbalIntent.Buff => "Applies a beneficial effect.",
                    VerbalIntent.Healing => "Applies a beneficial effect.",
                    VerbalIntent.Debuff => "Applies a harmful effect.",
                    VerbalIntent.Control => "Applies a control effect.",
                    _ => "Applies a control effect."
                };

                sentences.Add(intentSentence);
            }

            return string.Join(" ", sentences).Trim();
        }

        private int ComputeTooltipSaveDC(ActionDefinition action, int proficiencyBonus, Combatant combatant)
        {
            if (action.SaveDC.HasValue)
                return action.SaveDC.Value;
            if (string.IsNullOrEmpty(action.SaveType))
                return 0;
            int spellcastingMod = GetTooltipSpellcastingMod(combatant);
            return 8 + Math.Max(0, proficiencyBonus) + spellcastingMod + action.SaveDCBonus;
        }

        private int GetTooltipSpellcastingMod(Combatant combatant)
        {
            if (combatant?.ResolvedCharacter?.Sheet?.ClassLevels == null) return 0;
            var registry = _characterDataRegistry;
            if (registry != null)
            {
                foreach (var cl in combatant.ResolvedCharacter.Sheet.ClassLevels)
                {
                    var classDef = registry.GetClass(cl.ClassId);
                    if (!string.IsNullOrEmpty(classDef?.SpellcastingAbility) &&
                        Enum.TryParse<QDND.Data.CharacterModel.AbilityType>(classDef.SpellcastingAbility, true, out var ability))
                        return combatant.GetAbilityModifier(ability);
                }
                return 0;
            }
            // Fallback if registry unavailable
            foreach (var cl in combatant.ResolvedCharacter.Sheet.ClassLevels)
            {
                string classId = cl.ClassId?.ToLowerInvariant();
                switch (classId)
                {
                    case "wizard": return combatant.GetAbilityModifier(QDND.Data.CharacterModel.AbilityType.Intelligence);
                    case "cleric": case "druid": case "ranger": case "monk":
                        return combatant.GetAbilityModifier(QDND.Data.CharacterModel.AbilityType.Wisdom);
                    case "bard": case "sorcerer": case "warlock": case "paladin":
                        return combatant.GetAbilityModifier(QDND.Data.CharacterModel.AbilityType.Charisma);
                }
            }
            return 0;
        }

        private static string BuildDamageSummary(ActionDefinition action, Combatant combatant = null)
        {
            var dmgEffect = action?.Effects?.FirstOrDefault(e =>
                string.Equals(e.Type, "damage", StringComparison.OrdinalIgnoreCase));
            if (dmgEffect == null)
                return null;
            string formula = dmgEffect.DiceFormula ?? "";
            string type = dmgEffect.DamageType ?? "";
            if (combatant != null)
            {
                formula = SpellEffectConverter.ResolveDynamicFormula(formula, combatant);
                type = SpellEffectConverter.ResolveDynamicFormula(type, combatant);
            }
            if (!string.IsNullOrEmpty(formula) && !string.IsNullOrEmpty(type))
                return $"{formula} {type}";
            if (!string.IsNullOrEmpty(formula))
                return formula;
            return null;
        }

        private static string BuildHealingSummary(ActionDefinition action, Combatant combatant = null)
        {
            var healEffect = action?.Effects?.FirstOrDefault(e =>
                string.Equals(e.Type, "heal", StringComparison.OrdinalIgnoreCase));
            if (healEffect == null)
            {
                return null;
            }

            string formula = healEffect.DiceFormula ?? "";
            if (combatant != null)
            {
                formula = SpellEffectConverter.ResolveDynamicFormula(formula, combatant);
            }

            if (!string.IsNullOrWhiteSpace(formula))
            {
                return formula;
            }

            if (healEffect.Value <= 0)
            {
                return null;
            }

            return Math.Abs(healEffect.Value - MathF.Round(healEffect.Value)) < 0.001f
                ? MathF.Round(healEffect.Value).ToString()
                : healEffect.Value.ToString("0.#");
        }

        private static string BuildItemActionDescription(ActionDefinition action, InventoryItem item, int quantity)
        {
            if (item == null)
                return action?.Description ?? "No description available.";

            var lines = new List<string>
            {
                $"Rarity: {FormatRarity(item.Rarity)}"
            };

            if (!string.IsNullOrWhiteSpace(item.Description))
                lines.Add(item.Description);
            else if (!string.IsNullOrWhiteSpace(action?.Description))
                lines.Add(action.Description);

            if (item.SpecialEffects != null)
            {
                foreach (var effect in item.SpecialEffects)
                {
                    if (!string.IsNullOrWhiteSpace(effect) && !lines.Contains(effect, StringComparer.OrdinalIgnoreCase))
                        lines.Add(effect);
                }
            }

            lines.Add($"Use: {BuildActionCostLabel(action?.Cost, item.UseCosts)}");
            lines.Add($"Category: {item.Category}");
            if (item.Weight > 0)
                lines.Add($"Weight: {item.Weight} lb");
            if (quantity > 0)
                lines.Add($"Charges: {quantity}");

            return string.Join("\n", lines);
        }

        private static string BuildActionCostLabel(ActionCost cost, string fallbackUseCosts)
        {
            var parts = new List<string>();

            if (cost?.UsesAction == true)
                parts.Add("Action");
            if (cost?.UsesBonusAction == true)
                parts.Add("Bonus Action");
            if (cost?.UsesReaction == true)
                parts.Add("Reaction");

            if (parts.Count == 0 && !string.IsNullOrWhiteSpace(fallbackUseCosts))
            {
                foreach (var token in fallbackUseCosts.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (token.StartsWith("ActionPoint", StringComparison.OrdinalIgnoreCase))
                        parts.Add("Action");
                    else if (token.StartsWith("BonusActionPoint", StringComparison.OrdinalIgnoreCase))
                        parts.Add("Bonus Action");
                    else if (token.StartsWith("ReactionActionPoint", StringComparison.OrdinalIgnoreCase))
                        parts.Add("Reaction");
                }
            }

            return parts.Count > 0
                ? string.Join(" + ", parts.Distinct(StringComparer.OrdinalIgnoreCase))
                : "Action";
        }

        private static string FormatRarity(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.VeryRare => "Very Rare",
                _ => rarity.ToString(),
            };
        }
    }
}
