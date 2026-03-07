using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Actions;
using QDND.Data.Stats;
using QDND.Data.Statuses;

namespace QDND.Data.Items
{
    /// <summary>
    /// Registry of resolved item definitions sourced from BG3 Object.txt entries.
    /// </summary>
    public class ItemDefinitionRegistry
    {
        private readonly Dictionary<string, ItemDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<ItemUseCategory, List<ItemDefinition>> _byCategory = new();

        public int Count => _definitions.Count;

        public void Initialize(StatsRegistry statsRegistry, ActionRegistry actionRegistry)
        {
            Initialize(statsRegistry, actionRegistry, null);
        }

        /// <summary>
        /// Initialize from parsed BG3 objects and resolve consumable spell/status links.
        /// Call this after both stats and action registries are loaded.
        /// </summary>
        public void Initialize(StatsRegistry statsRegistry, ActionRegistry actionRegistry, StatusRegistry statusRegistry)
        {
            _definitions.Clear();
            _byCategory.Clear();

            if (statsRegistry == null)
            {
                Console.WriteLine("[ItemDefinitionRegistry] Warning: StatsRegistry is null; no item definitions loaded");
                return;
            }

            var objects = statsRegistry.GetAllObjects();
            var resolver = new BG3ConsumableResolver(actionRegistry, statusRegistry);

            foreach (var (_, obj) in objects)
            {
                if (obj == null || ShouldSkip(obj))
                    continue;

                var category = DetermineUseCategory(obj, objects);
                if (category == ItemUseCategory.None)
                    continue;

                var definition = resolver.Resolve(obj, category);
                if (definition == null)
                    continue;

                _definitions[definition.Id] = definition;

                if (!_byCategory.TryGetValue(definition.UseCategory, out var list))
                {
                    list = new List<ItemDefinition>();
                    _byCategory[definition.UseCategory] = list;
                }

                list.Add(definition);
            }

            int consumableCount = GetAllConsumables().Count;
            Console.WriteLine($"[ItemDefinitionRegistry] Resolved {_definitions.Count} item definitions ({consumableCount} consumables)");
        }

        public ItemDefinition GetDefinition(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId))
                return null;

            _definitions.TryGetValue(objectId, out var definition);
            return definition;
        }

        public IReadOnlyList<ItemDefinition> GetByCategory(ItemUseCategory category)
        {
            return _byCategory.TryGetValue(category, out var list)
                ? list
                : (IReadOnlyList<ItemDefinition>)Array.Empty<ItemDefinition>();
        }

        public IReadOnlyList<ItemDefinition> GetAllConsumables()
        {
            return _definitions.Values
                .Where(d => d.IsConsumable)
                .ToList();
        }

        private static bool ShouldSkip(BG3ObjectData obj)
        {
            if (string.IsNullOrWhiteSpace(obj.Name))
                return true;

            if (obj.Name.StartsWith("_", StringComparison.Ordinal))
                return true;

            if (!string.IsNullOrWhiteSpace(obj.Flags) &&
                obj.Flags.Contains("ToBeDeleted", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrWhiteSpace(obj.ObjectCategory) &&
                obj.ObjectCategory.Contains("ToBeDeleted", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static ItemUseCategory DetermineUseCategory(BG3ObjectData obj, IReadOnlyDictionary<string, BG3ObjectData> objects)
        {
            string itemUseType = obj.ItemUseType ?? string.Empty;

            if (itemUseType.Equals("Potion", StringComparison.OrdinalIgnoreCase))
                return ItemUseCategory.Potion;
            if (itemUseType.Equals("Scroll", StringComparison.OrdinalIgnoreCase))
                return ItemUseCategory.Scroll;
            if (itemUseType.Equals("Consumable", StringComparison.OrdinalIgnoreCase))
                return ItemUseCategory.Consumable;
            if (itemUseType.Equals("Throwable", StringComparison.OrdinalIgnoreCase))
                return ItemUseCategory.Throwable;
            if (itemUseType.Equals("Grenade", StringComparison.OrdinalIgnoreCase))
                return ItemUseCategory.Grenade;
            if (itemUseType.Equals("Arrow", StringComparison.OrdinalIgnoreCase))
                return ItemUseCategory.Arrow;

            if (InheritsFromTemplate(obj, objects, "_Potion"))
                return ItemUseCategory.Potion;
            if (InheritsFromTemplate(obj, objects, "_MagicScroll"))
                return ItemUseCategory.Scroll;
            if (InheritsFromTemplate(obj, objects, "_Consumable"))
                return ItemUseCategory.Consumable;
            if (InheritsFromTemplate(obj, objects, "_Arrow"))
                return ItemUseCategory.Arrow;
            if (InheritsFromTemplate(obj, objects, "_GenericGrenade"))
                return ItemUseCategory.Grenade;

            if (obj.Name.StartsWith("OBJ_Potion_", StringComparison.OrdinalIgnoreCase))
                return ItemUseCategory.Potion;
            if (obj.Name.StartsWith("OBJ_Scroll_", StringComparison.OrdinalIgnoreCase))
                return ItemUseCategory.Scroll;

            return ItemUseCategory.None;
        }

        private static bool InheritsFromTemplate(BG3ObjectData obj, IReadOnlyDictionary<string, BG3ObjectData> objects, string templateName)
        {
            string parentId = obj.ParentId;
            int depth = 0;

            while (!string.IsNullOrWhiteSpace(parentId) && depth < 32)
            {
                if (parentId.Equals(templateName, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (!objects.TryGetValue(parentId, out var parentObj))
                    return false;

                parentId = parentObj.ParentId;
                depth++;
            }

            return false;
        }
    }
}