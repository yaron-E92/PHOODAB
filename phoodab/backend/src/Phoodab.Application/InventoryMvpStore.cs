using System.Text.Json;
using Phoodab.Domain;

namespace Phoodab.Application;

public interface IInventoryMvpStore
{
    ItemDefinition CreateItemDefinition(string name, ItemKind kind, decimal? desiredAmount = null, string? desiredUnit = null);
    InventoryEntry? CreateInventoryEntry(Guid itemDefinitionId, Guid? storageSlotId);
    InventoryLot? AddInventoryLot(Guid inventoryEntryId, decimal quantity, string unit, DateOnly? expiresOn, Guid? storageSlotId);
    IReadOnlyList<object> GetSummary();
    IReadOnlyList<InventoryLotReadModel> GetExpiringLots(DateOnly todayUtc);
    IReadOnlyList<ReplenishmentRule> GetRules();
    ReplenishmentRule? UpdateRule(Guid ruleId, decimal? desiredAmount, string? desiredUnit, bool? isDisabled, int? expiryWarningDays);
    IReadOnlyList<InventoryEntry> GetInventoryEntries();
    void EnsureDevelopmentSeedData(DateOnly todayUtc);
    object CreateOrUpdateShoppingListItemFromSuggestion(Guid itemDefinitionId, decimal quantity, string unit);
    object? UpdateShoppingListItemStatus(Guid shoppingListItemId, bool? isResolved, bool? isPurchased);
    IReadOnlyList<object> GetShoppingListItems();
}

public sealed class FileInventoryMvpStore : IInventoryMvpStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _storePath;
    private readonly object _sync = new();

    public FileInventoryMvpStore()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var directory = Path.Combine(basePath, "phoodab");
        Directory.CreateDirectory(directory);
        _storePath = Path.Combine(directory, "inventory-mvp-store.json");
    }

    public ItemDefinition CreateItemDefinition(string name, ItemKind kind, decimal? desiredAmount = null, string? desiredUnit = null)
    {
        lock (_sync)
        {
            var state = LoadState();
            var item = new ItemDefinition(Guid.NewGuid(), name, kind);
            state.ItemDefinitions.Add(new ItemDefinitionState(item.Id, item.Name, item.Kind));
            state.Rules.Add(new ReplenishmentRuleState(Guid.NewGuid(), item.Id, desiredAmount ?? 2m, desiredUnit ?? "unit", 2, false, false));
            SaveState(state);
            return item;
        }
    }

    public InventoryEntry? CreateInventoryEntry(Guid itemDefinitionId, Guid? storageSlotId)
    {
        lock (_sync)
        {
            var state = LoadState();
            var itemState = state.ItemDefinitions.SingleOrDefault(i => i.Id == itemDefinitionId);
            if (itemState is null)
            {
                return null;
            }

            var item = new ItemDefinition(itemState.Id, itemState.Name, itemState.Kind);
            var entry = new InventoryEntry(Guid.NewGuid(), item, storageSlotId);
            state.InventoryEntries.Add(new InventoryEntryState(entry.Id, entry.ItemDefinitionId, entry.StorageSlotId));

            if (state.Rules.All(r => r.ItemDefinitionId != itemDefinitionId))
            {
                state.Rules.Add(new ReplenishmentRuleState(Guid.NewGuid(), itemDefinitionId, 2m, "unit", 2, false, false));
            }

            SaveState(state);
            return entry;
        }
    }

    public InventoryLot? AddInventoryLot(Guid inventoryEntryId, decimal quantity, string unit, DateOnly? expiresOn, Guid? storageSlotId)
    {
        lock (_sync)
        {
            var state = LoadState();
            var entryState = state.InventoryEntries.SingleOrDefault(e => e.Id == inventoryEntryId);
            if (entryState is null)
            {
                return null;
            }

            var lot = new InventoryLot(Guid.NewGuid(), entryState.ItemDefinitionId, Quantity.From(quantity), new Unit(unit), expiresOn, storageSlotId);
            state.InventoryLots.Add(new InventoryLotState(lot.Id, inventoryEntryId, lot.ItemDefinitionId, lot.Quantity.Value, lot.Unit.Value, lot.ExpiresOn, lot.StorageSlotId));

            var existingRule = state.Rules.SingleOrDefault(r => r.ItemDefinitionId == entryState.ItemDefinitionId);
            if (existingRule is not null && string.Equals(existingRule.Unit, "unit", StringComparison.OrdinalIgnoreCase))
            {
                state.Rules.Remove(existingRule);
                state.Rules.Add(existingRule with { Unit = unit });
            }

            SaveState(state);
            return lot;
        }
    }

    public IReadOnlyList<object> GetSummary()
    {
        lock (_sync)
        {
            var state = LoadState();
            return state.InventoryEntries.Select(e =>
            {
                var item = state.ItemDefinitions.Single(i => i.Id == e.ItemDefinitionId);
                var lots = state.InventoryLots.Where(l => l.ItemDefinitionId == e.ItemDefinitionId).ToList();
                return new
                {
                    inventoryEntryId = e.Id,
                    itemDefinitionId = e.ItemDefinitionId,
                    itemName = item.Name,
                    totalQuantity = lots.Sum(l => l.Quantity),
                    unit = lots.FirstOrDefault()?.Unit,
                    lotCount = lots.Count
                } as object;
            }).ToList();
        }
    }

    public IReadOnlyList<InventoryLotReadModel> GetExpiringLots(DateOnly todayUtc)
    {
        lock (_sync)
        {
            var state = LoadState();
            return [.. state.InventoryLots
                .Select(l => new InventoryLot(l.Id, l.ItemDefinitionId, Quantity.From(l.Quantity), new Unit(l.Unit), l.ExpiresOn, l.StorageSlotId))
                .Select(lot => InventoryLotReadModel.From(lot, todayUtc))
                .Where(lot => lot.ExpiryStatus is "Expired" or "Urgent" or "Soon")];
        }
    }

    public IReadOnlyList<ReplenishmentRule> GetRules()
    {
        lock (_sync)
        {
            var state = LoadState();
            return state.Rules
                .Select(r => new ReplenishmentRule(r.Id, r.ItemDefinitionId, Quantity.From(r.TargetAmount), new Unit(r.Unit), r.ExpiryWarningDays, r.IsHidden, r.IsDisabled))
                .ToList();
        }
    }

    public ReplenishmentRule? UpdateRule(Guid ruleId, decimal? desiredAmount, string? desiredUnit, bool? isDisabled, int? expiryWarningDays)
    {
        lock (_sync)
        {
            var state = LoadState();
            var existing = state.Rules.SingleOrDefault(r => r.Id == ruleId);
            if (existing is null)
            {
                return null;
            }

            var updated = existing with
            {
                TargetAmount = desiredAmount ?? existing.TargetAmount,
                Unit = string.IsNullOrWhiteSpace(desiredUnit) ? existing.Unit : desiredUnit.Trim(),
                IsDisabled = isDisabled ?? existing.IsDisabled,
                ExpiryWarningDays = expiryWarningDays ?? existing.ExpiryWarningDays
            };
            state.Rules.Remove(existing);
            state.Rules.Add(updated);
            SaveState(state);
            return new ReplenishmentRule(updated.Id, updated.ItemDefinitionId, Quantity.From(updated.TargetAmount), new Unit(updated.Unit), updated.ExpiryWarningDays, updated.IsHidden, updated.IsDisabled);
        }
    }

    public IReadOnlyList<InventoryEntry> GetInventoryEntries()
    {
        lock (_sync)
        {
            var state = LoadState();

            return state.InventoryEntries.Select(e =>
            {
                var itemState = state.ItemDefinitions.Single(i => i.Id == e.ItemDefinitionId);
                var item = new ItemDefinition(itemState.Id, itemState.Name, itemState.Kind);
                var projected = new InventoryEntry(e.Id, item, e.StorageSlotId);

                foreach (var lot in state.InventoryLots.Where(l => l.ItemDefinitionId == e.ItemDefinitionId))
                {
                    projected.AddLot(new InventoryLot(lot.Id, lot.ItemDefinitionId, Quantity.From(lot.Quantity), new Unit(lot.Unit), lot.ExpiresOn, lot.StorageSlotId));
                }

                return projected;
            }).ToList();
        }
    }

    public void EnsureDevelopmentSeedData(DateOnly todayUtc)
    {
        lock (_sync)
        {
            var state = LoadState();
            if (state.InventoryEntries.Count > 0)
            {
                return;
            }

            SeedItem(state, "Milk", 2m, "liter", todayUtc.AddDays(14));
            SeedItem(state, "Eggs", 1m, "dozen", todayUtc.AddDays(2));
            SeedItem(state, "Pasta", 0.5m, "kg", todayUtc.AddDays(-1));
            SeedItem(state, "Rice", 0.25m, "kg", null);

            SaveState(state);
        }
    }

    public object CreateOrUpdateShoppingListItemFromSuggestion(Guid itemDefinitionId, decimal quantity, string unit)
    {
        lock (_sync)
        {
            var state = LoadState();
            var itemDefinition = state.ItemDefinitions.SingleOrDefault(x => x.Id == itemDefinitionId)
                ?? throw new InvalidOperationException("Item definition not found.");

            var existing = state.ShoppingListItems.SingleOrDefault(x => x.ItemDefinitionId == itemDefinitionId && !x.IsResolved && !x.IsPurchased);
            if (existing is not null)
            {
                var updated = existing with { Quantity = quantity, Unit = unit };
                state.ShoppingListItems.Remove(existing);
                state.ShoppingListItems.Add(updated);
                SaveState(state);
                return ToShoppingListReadModel(updated, itemDefinition.Name);
            }

            var created = new ShoppingListItemState(Guid.NewGuid(), itemDefinitionId, itemDefinition.Name, quantity, unit, false, false);
            state.ShoppingListItems.Add(created);
            SaveState(state);
            return ToShoppingListReadModel(created, itemDefinition.Name);
        }
    }

    public object? UpdateShoppingListItemStatus(Guid shoppingListItemId, bool? isResolved, bool? isPurchased)
    {
        lock (_sync)
        {
            var state = LoadState();
            var existing = state.ShoppingListItems.SingleOrDefault(x => x.Id == shoppingListItemId);
            if (existing is null)
            {
                return null;
            }

            var updated = existing with
            {
                IsResolved = isResolved ?? existing.IsResolved,
                IsPurchased = isPurchased ?? existing.IsPurchased
            };

            state.ShoppingListItems.Remove(existing);
            state.ShoppingListItems.Add(updated);
            SaveState(state);
            return ToShoppingListReadModel(updated, existing.ItemName);
        }
    }

    public IReadOnlyList<object> GetShoppingListItems()
    {
        lock (_sync)
        {
            var state = LoadState();
            return state.ShoppingListItems
                .Select(x => ToShoppingListReadModel(x, x.ItemName))
                .Cast<object>()
                .ToList();
        }
    }

    private InventoryMvpState LoadState()
    {
        if (!File.Exists(_storePath))
        {
            return new InventoryMvpState();
        }

        var json = File.ReadAllText(_storePath);
        return JsonSerializer.Deserialize<InventoryMvpState>(json, JsonOptions) ?? new InventoryMvpState();
    }

    private void SaveState(InventoryMvpState state)
    {
        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(_storePath, json);
    }

    // Persisted state models are kept separate from domain entities because domain types are immutable
    // and include value objects/collections that are reconstructed through domain constructors.
    private sealed class InventoryMvpState
    {
        public List<ItemDefinitionState> ItemDefinitions { get; set; } = [];
        public List<InventoryEntryState> InventoryEntries { get; set; } = [];
        public List<InventoryLotState> InventoryLots { get; set; } = [];
        public List<ReplenishmentRuleState> Rules { get; set; } = [];
        public List<ShoppingListItemState> ShoppingListItems { get; set; } = [];
    }

    private static void SeedItem(InventoryMvpState state, string name, decimal quantity, string unit, DateOnly? expiresOn)
    {
        var item = new ItemDefinitionState(Guid.NewGuid(), name, ItemKind.Consumable);
        state.ItemDefinitions.Add(item);

        var entry = new InventoryEntryState(Guid.NewGuid(), item.Id, null);
        state.InventoryEntries.Add(entry);

        state.InventoryLots.Add(new InventoryLotState(Guid.NewGuid(), entry.Id, item.Id, quantity, unit, expiresOn, null));
        state.Rules.Add(new ReplenishmentRuleState(Guid.NewGuid(), item.Id, 2m, unit, 2, false, false));
    }

    private static object ToShoppingListReadModel(ShoppingListItemState state, string itemName) => new
    {
        id = state.Id,
        itemDefinitionId = state.ItemDefinitionId,
        itemName,
        quantity = state.Quantity,
        unit = state.Unit,
        isResolved = state.IsResolved,
        isPurchased = state.IsPurchased
    };

    private sealed record ItemDefinitionState(Guid Id, string Name, ItemKind Kind);
    private sealed record InventoryEntryState(Guid Id, Guid ItemDefinitionId, Guid? StorageSlotId);
    private sealed record InventoryLotState(Guid Id, Guid InventoryEntryId, Guid ItemDefinitionId, decimal Quantity, string Unit, DateOnly? ExpiresOn, Guid? StorageSlotId);
    private sealed record ReplenishmentRuleState(Guid Id, Guid ItemDefinitionId, decimal TargetAmount, string Unit, int ExpiryWarningDays, bool IsHidden, bool IsDisabled);
    private sealed record ShoppingListItemState(Guid Id, Guid ItemDefinitionId, string ItemName, decimal Quantity, string Unit, bool IsResolved, bool IsPurchased);
}
