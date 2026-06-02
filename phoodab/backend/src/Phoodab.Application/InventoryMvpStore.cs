using System.Text.Json;
using Phoodab.Domain;

namespace Phoodab.Application;

public interface IInventoryMvpStore
{
    ItemDefinition CreateItemDefinition(string name, ItemKind kind, decimal? desiredAmount = null, string? desiredUnit = null);
    DurableEntry? CreateDurableEntry(Guid itemDefinitionId, Guid? storageSlotId);
    ConsumableEntry? AddConsumableEntry(Guid itemDefinitionId, decimal quantity, string unit, DateOnly? expiresOn, Guid? storageSlotId);
    IReadOnlyList<object> GetSummary();
    IReadOnlyList<ConsumableEntryReadModel> GetConsumableEntryReadModels(DateOnly todayUtc);
    ConsumableEntryReadModel? UpdateConsumableEntry(Guid entryId, decimal quantity, string unit, DateOnly? expiresOn, Guid? storageSlotId, DateOnly todayUtc);
    IReadOnlyList<ConsumableEntryReadModel> GetExpiringConsumableEntries(DateOnly todayUtc);
    IReadOnlyList<ReplenishmentRule> GetRules();
    ReplenishmentRule? UpdateRule(Guid ruleId, decimal? desiredAmount, string? desiredUnit, bool? isDisabled, int? expiryWarningDays);
    IReadOnlyList<ConsumableEntry> GetConsumableEntries();
    void EnsureDevelopmentSeedData(DateOnly todayUtc);
    object CreateOrUpdateShoppingListItemFromSuggestion(Guid itemDefinitionId, decimal quantity, string unit, decimal? deficitAmount, decimal? expiringSoonAmount, decimal? suggestedPurchaseAmount);
    object? UpdateShoppingListItemStatus(Guid shoppingListItemId, bool? isResolved, bool? isPurchased, string? status);
    IReadOnlyList<object> GetShoppingListItems();
}

public sealed class FileInventoryMvpStore : IInventoryMvpStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const decimal DefaultDesiredAmount = 2m;
    private const string DefaultDesiredUnit = "unit";
    private const int DefaultExpiryWarningDays = 2;
    private const string ShoppingStatusShoppingList = "ShoppingList";
    private const string ShoppingStatusInCart = "InCart";
    private const string ShoppingStatusBought = "Bought";
    private const string ShoppingStatusStockUpdateNeeded = "StockUpdateNeeded";
    private const string StockUpdateAction = "Add stock details for quantity, lot, expiry, and location.";

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
            if (item.Kind == ItemKind.Consumable)
            {
                state.Rules.Add(CreateDefaultRule(item.Id, desiredAmount, desiredUnit));
            }

            SaveState(state);
            return item;
        }
    }

    public DurableEntry? CreateDurableEntry(Guid itemDefinitionId, Guid? storageSlotId)
    {
        lock (_sync)
        {
            var state = LoadState();
            var itemState = state.ItemDefinitions.SingleOrDefault(i => i.Id == itemDefinitionId);
            if (itemState is null || itemState.Kind != ItemKind.Durable)
            {
                return null;
            }

            var item = new ItemDefinition(itemState.Id, itemState.Name, itemState.Kind);
            var entry = new DurableEntry(Guid.NewGuid(), item, storageSlotId);
            state.DurableEntries.Add(new DurableEntryState(entry.Id, entry.ItemDefinitionId, entry.StorageSlotId));
            SaveState(state);
            return entry;
        }
    }

    public ConsumableEntry? AddConsumableEntry(Guid itemDefinitionId, decimal quantity, string unit, DateOnly? expiresOn, Guid? storageSlotId)
    {
        lock (_sync)
        {
            var state = LoadState();
            var itemState = state.ItemDefinitions.SingleOrDefault(i => i.Id == itemDefinitionId);
            if (itemState is null || itemState.Kind != ItemKind.Consumable)
            {
                return null;
            }

            var item = new ItemDefinition(itemState.Id, itemState.Name, itemState.Kind);
            var entry = new ConsumableEntry(Guid.NewGuid(), item, Quantity.From(quantity), new Unit(unit), expiresOn, storageSlotId);
            state.ConsumableEntries.Add(new ConsumableEntryState(entry.Id, entry.ItemDefinitionId, entry.Quantity.Value, entry.Unit.Value, entry.ExpiresOn, entry.StorageSlotId));

            var existingRule = state.Rules.SingleOrDefault(r => r.ItemDefinitionId == itemDefinitionId);
            if (existingRule is not null && string.Equals(existingRule.Unit, "unit", StringComparison.OrdinalIgnoreCase))
            {
                state.Rules.Remove(existingRule);
                state.Rules.Add(existingRule with { Unit = unit });
            }

            SaveState(state);
            return entry;
        }
    }

    public IReadOnlyList<object> GetSummary()
    {
        lock (_sync)
        {
            var state = LoadState();
            return state.ConsumableEntries
                .GroupBy(e => e.ItemDefinitionId)
                .Select(group =>
            {
                var item = state.ItemDefinitions.Single(i => i.Id == group.Key);
                var entries = group.ToList();
                var units = entries.Select(e => e.Unit).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var hasMixedUnits = units.Count > 1;
                return new
                {
                    itemDefinitionId = group.Key,
                    itemName = item.Name,
                    totalQuantity = hasMixedUnits ? (decimal?)null : entries.Sum(e => e.Quantity),
                    unit = hasMixedUnits ? null : entries.FirstOrDefault()?.Unit,
                    entryCount = entries.Count,
                    hasMixedUnits,
                    mixedUnitWarning = hasMixedUnits ? "Mixed units cannot be totaled safely." : null
                } as object;
            }).ToList();
        }
    }

    public IReadOnlyList<ConsumableEntryReadModel> GetConsumableEntryReadModels(DateOnly todayUtc)
    {
        lock (_sync)
        {
            var state = LoadState();
            return [.. state.ConsumableEntries.Select(e => ToConsumableEntryReadModel(state, e, todayUtc))];
        }
    }

    public ConsumableEntryReadModel? UpdateConsumableEntry(Guid entryId, decimal quantity, string unit, DateOnly? expiresOn, Guid? storageSlotId, DateOnly todayUtc)
    {
        lock (_sync)
        {
            var state = LoadState();
            var existing = state.ConsumableEntries.SingleOrDefault(e => e.Id == entryId);
            if (existing is null)
            {
                return null;
            }

            var itemState = state.ItemDefinitions.Single(i => i.Id == existing.ItemDefinitionId);
            var item = new ItemDefinition(itemState.Id, itemState.Name, itemState.Kind);
            var entry = new ConsumableEntry(entryId, item, Quantity.From(quantity), new Unit(unit), expiresOn, storageSlotId);
            var updated = new ConsumableEntryState(entry.Id, entry.ItemDefinitionId, entry.Quantity.Value, entry.Unit.Value, entry.ExpiresOn, entry.StorageSlotId);

            state.ConsumableEntries.Remove(existing);
            state.ConsumableEntries.Add(updated);
            SaveState(state);

            var expiryWarningDays = state.Rules.SingleOrDefault(r => r.ItemDefinitionId == updated.ItemDefinitionId)?.ExpiryWarningDays ?? DefaultExpiryWarningDays;
            return ConsumableEntryReadModel.From(entry, todayUtc, expiryWarningDays);
        }
    }

    public IReadOnlyList<ConsumableEntryReadModel> GetExpiringConsumableEntries(DateOnly todayUtc)
    {
        lock (_sync)
        {
            var state = LoadState();
            return [.. state.ConsumableEntries
                .Select(e => ToConsumableEntryReadModel(state, e, todayUtc))
                .Where(entry => entry.ExpiryStatus is "Expired" or "Urgent" or "Soon")];
        }
    }

    public IReadOnlyList<ReplenishmentRule> GetRules()
    {
        lock (_sync)
        {
            var state = LoadState();
            return state.Rules
                .Select(ToReplenishmentRule)
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
                TargetAmount = desiredAmount ?? existing.TargetAmount ?? DefaultDesiredAmount,
                Unit = string.IsNullOrWhiteSpace(desiredUnit) ? existing.Unit : desiredUnit.Trim(),
                IsDisabled = isDisabled ?? existing.IsDisabled ?? false,
                ExpiryWarningDays = expiryWarningDays ?? existing.ExpiryWarningDays ?? DefaultExpiryWarningDays
            };
            state.Rules.Remove(existing);
            state.Rules.Add(updated);
            SaveState(state);
            return ToReplenishmentRule(updated);
        }
    }

    public IReadOnlyList<ConsumableEntry> GetConsumableEntries()
    {
        lock (_sync)
        {
            var state = LoadState();

            return state.ConsumableEntries.Select(e =>
            {
                var itemState = state.ItemDefinitions.Single(i => i.Id == e.ItemDefinitionId);
                var item = new ItemDefinition(itemState.Id, itemState.Name, itemState.Kind);
                return new ConsumableEntry(e.Id, item, Quantity.From(e.Quantity), new Unit(e.Unit), e.ExpiresOn, e.StorageSlotId);
            }).ToList();
        }
    }

    public void EnsureDevelopmentSeedData(DateOnly todayUtc)
    {
        lock (_sync)
        {
            var state = LoadState();
            if (state.DurableEntries.Count > 0 || state.ConsumableEntries.Count > 0)
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

    public object CreateOrUpdateShoppingListItemFromSuggestion(Guid itemDefinitionId, decimal quantity, string unit, decimal? deficitAmount, decimal? expiringSoonAmount, decimal? suggestedPurchaseAmount)
    {
        lock (_sync)
        {
            var state = LoadState();
            var itemDefinition = state.ItemDefinitions.SingleOrDefault(x => x.Id == itemDefinitionId)
                ?? throw new InvalidOperationException("Item definition not found.");

            var existing = state.ShoppingListItems.SingleOrDefault(x => x.ItemDefinitionId == itemDefinitionId && !x.IsResolved && !x.IsPurchased);
            if (existing is not null)
            {
                var updated = existing with
                {
                    Quantity = quantity,
                    Unit = unit,
                    SourceDeficitAmount = deficitAmount,
                    SourceExpiringSoonAmount = expiringSoonAmount,
                    SourceSuggestedPurchaseAmount = suggestedPurchaseAmount ?? quantity
                };
                state.ShoppingListItems.Remove(existing);
                state.ShoppingListItems.Add(updated);
                SaveState(state);
                return ToShoppingListReadModel(updated, itemDefinition.Name);
            }

            var created = new ShoppingListItemState(Guid.NewGuid(), itemDefinitionId, itemDefinition.Name, quantity, unit, false, false, ShoppingStatusShoppingList, deficitAmount, expiringSoonAmount, suggestedPurchaseAmount ?? quantity);
            state.ShoppingListItems.Add(created);
            SaveState(state);
            return ToShoppingListReadModel(created, itemDefinition.Name);
        }
    }

    public object? UpdateShoppingListItemStatus(Guid shoppingListItemId, bool? isResolved, bool? isPurchased, string? status)
    {
        lock (_sync)
        {
            var state = LoadState();
            var existing = state.ShoppingListItems.SingleOrDefault(x => x.Id == shoppingListItemId);
            if (existing is null)
            {
                return null;
            }

            var nextStatus = NormalizeShoppingStatus(status) ?? DeriveShoppingStatus(existing);
            var nextIsResolved = isResolved ?? existing.IsResolved;
            var nextIsPurchased = isPurchased ?? existing.IsPurchased;

            if (!string.IsNullOrWhiteSpace(status))
            {
                (nextIsResolved, nextIsPurchased) = GetCompatibilityFlags(nextStatus);
            }
            else if (isResolved.HasValue || isPurchased.HasValue)
            {
                nextStatus = DeriveShoppingStatus(nextIsResolved, nextIsPurchased, nextStatus);
            }

            var updated = existing with
            {
                IsResolved = nextIsResolved,
                IsPurchased = nextIsPurchased,
                Status = nextStatus
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
        var state = JsonSerializer.Deserialize<InventoryMvpState>(json, JsonOptions) ?? new InventoryMvpState();
        if (NormalizeState(state))
        {
            SaveState(state);
        }

        return state;
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
        public List<DurableEntryState> DurableEntries { get; set; } = [];
        public List<ConsumableEntryState> ConsumableEntries { get; set; } = [];
        public List<ReplenishmentRuleState> Rules { get; set; } = [];
        public List<ShoppingListItemState> ShoppingListItems { get; set; } = [];
    }

    private static void SeedItem(InventoryMvpState state, string name, decimal quantity, string unit, DateOnly? expiresOn)
    {
        var item = new ItemDefinitionState(Guid.NewGuid(), name, ItemKind.Consumable);
        state.ItemDefinitions.Add(item);

        state.ConsumableEntries.Add(new ConsumableEntryState(Guid.NewGuid(), item.Id, quantity, unit, expiresOn, null));
        state.Rules.Add(CreateDefaultRule(item.Id, unit: unit));
    }

    private static bool NormalizeState(InventoryMvpState state)
    {
        var changed = false;
        var consumableItemIds = state.ItemDefinitions
            .Where(item => item.Kind == ItemKind.Consumable)
            .Select(item => item.Id)
            .ToHashSet();

        var nonConsumableRules = state.Rules
            .Where(rule => !consumableItemIds.Contains(rule.ItemDefinitionId))
            .ToList();
        foreach (var rule in nonConsumableRules)
        {
            state.Rules.Remove(rule);
            changed = true;
        }

        foreach (var itemId in consumableItemIds)
        {
            if (state.Rules.All(rule => rule.ItemDefinitionId != itemId))
            {
                state.Rules.Add(CreateDefaultRule(itemId));
                changed = true;
            }
        }

        for (var i = 0; i < state.Rules.Count; i++)
        {
            var normalized = NormalizeRule(state.Rules[i]);
            if (normalized != state.Rules[i])
            {
                state.Rules[i] = normalized;
                changed = true;
            }
        }

        return changed;
    }

    private static ReplenishmentRuleState NormalizeRule(ReplenishmentRuleState rule)
    {
        return rule with
        {
            TargetAmount = rule.TargetAmount ?? DefaultDesiredAmount,
            Unit = string.IsNullOrWhiteSpace(rule.Unit) ? DefaultDesiredUnit : rule.Unit.Trim(),
            ExpiryWarningDays = rule.ExpiryWarningDays ?? DefaultExpiryWarningDays,
            IsDisabled = rule.IsDisabled ?? false
        };
    }

    private static ReplenishmentRuleState CreateDefaultRule(Guid itemDefinitionId, decimal? amount = null, string? unit = null)
    {
        return new ReplenishmentRuleState(
            Guid.NewGuid(),
            itemDefinitionId,
            amount ?? DefaultDesiredAmount,
            string.IsNullOrWhiteSpace(unit) ? DefaultDesiredUnit : unit.Trim(),
            DefaultExpiryWarningDays,
            false,
            false);
    }

    private static ReplenishmentRule ToReplenishmentRule(ReplenishmentRuleState rule)
    {
        var normalized = NormalizeRule(rule);
        return new ReplenishmentRule(
            normalized.Id,
            normalized.ItemDefinitionId,
            Quantity.From(normalized.TargetAmount ?? DefaultDesiredAmount),
            new Unit(normalized.Unit ?? DefaultDesiredUnit),
            normalized.ExpiryWarningDays ?? DefaultExpiryWarningDays,
            normalized.IsHidden,
            normalized.IsDisabled ?? false);
    }

    private static object ToShoppingListReadModel(ShoppingListItemState state, string itemName) => new
    {
        id = state.Id,
        itemDefinitionId = state.ItemDefinitionId,
        itemName,
        quantity = state.Quantity,
        unit = state.Unit,
        isResolved = state.IsResolved,
        isPurchased = state.IsPurchased,
        status = DeriveShoppingStatus(state),
        stockUpdateNeeded = DeriveShoppingStatus(state) is ShoppingStatusStockUpdateNeeded,
        nextInventoryAction = DeriveShoppingStatus(state) is ShoppingStatusStockUpdateNeeded ? StockUpdateAction : null,
        sourceDeficitAmount = state.SourceDeficitAmount,
        sourceExpiringSoonAmount = state.SourceExpiringSoonAmount,
        sourceSuggestedPurchaseAmount = state.SourceSuggestedPurchaseAmount
    };

    private static string? NormalizeShoppingStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return status.Trim().ToLowerInvariant() switch
        {
            "shoppinglist" or "shopping_list" or "shopping-list" or "list" => ShoppingStatusShoppingList,
            "incart" or "in_cart" or "in-cart" or "cart" or "buying" => ShoppingStatusInCart,
            "bought" or "purchased" => ShoppingStatusBought,
            "stockupdateneeded" or "stock_update_needed" or "stock-update-needed" => ShoppingStatusStockUpdateNeeded,
            _ => ShoppingStatusShoppingList
        };
    }

    private static string DeriveShoppingStatus(ShoppingListItemState state)
    {
        return NormalizeShoppingStatus(state.Status) ?? DeriveShoppingStatus(state.IsResolved, state.IsPurchased, ShoppingStatusShoppingList);
    }

    private static string DeriveShoppingStatus(bool isResolved, bool isPurchased, string fallback)
    {
        if (isPurchased && isResolved)
        {
            return ShoppingStatusBought;
        }

        if (isPurchased)
        {
            return ShoppingStatusStockUpdateNeeded;
        }

        return fallback;
    }

    private static (bool IsResolved, bool IsPurchased) GetCompatibilityFlags(string status)
    {
        return status switch
        {
            ShoppingStatusBought => (true, true),
            ShoppingStatusStockUpdateNeeded => (false, true),
            _ => (false, false)
        };
    }

    private static ConsumableEntryReadModel ToConsumableEntryReadModel(InventoryMvpState state, ConsumableEntryState entryState, DateOnly todayUtc)
    {
        var itemState = state.ItemDefinitions.Single(i => i.Id == entryState.ItemDefinitionId);
        var item = new ItemDefinition(itemState.Id, itemState.Name, itemState.Kind);
        var entry = new ConsumableEntry(entryState.Id, item, Quantity.From(entryState.Quantity), new Unit(entryState.Unit), entryState.ExpiresOn, entryState.StorageSlotId);
        var expiryWarningDays = state.Rules.SingleOrDefault(r => r.ItemDefinitionId == entryState.ItemDefinitionId)?.ExpiryWarningDays ?? DefaultExpiryWarningDays;
        return ConsumableEntryReadModel.From(entry, todayUtc, expiryWarningDays);
    }

    private sealed record ItemDefinitionState(Guid Id, string Name, ItemKind Kind);
    private sealed record DurableEntryState(Guid Id, Guid ItemDefinitionId, Guid? StorageSlotId);
    private sealed record ConsumableEntryState(Guid Id, Guid ItemDefinitionId, decimal Quantity, string Unit, DateOnly? ExpiresOn, Guid? StorageSlotId);
    private sealed record ReplenishmentRuleState(Guid Id, Guid ItemDefinitionId, decimal? TargetAmount, string? Unit, int? ExpiryWarningDays, bool IsHidden, bool? IsDisabled);
    private sealed record ShoppingListItemState(Guid Id, Guid ItemDefinitionId, string ItemName, decimal Quantity, string Unit, bool IsResolved, bool IsPurchased, string? Status = null, decimal? SourceDeficitAmount = null, decimal? SourceExpiringSoonAmount = null, decimal? SourceSuggestedPurchaseAmount = null);
}
