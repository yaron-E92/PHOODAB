using System.Text.Json;
using Phoodab.Domain;

namespace Phoodab.Application;

public interface IInventoryMvpStore
{
    ItemDefinition CreateItemDefinition(string name, ItemKind kind);
    InventoryEntry? CreateInventoryEntry(Guid itemDefinitionId, Guid? storageSlotId);
    InventoryLot? AddInventoryLot(Guid inventoryEntryId, decimal quantity, string unit, DateOnly? expiresOn, Guid? storageSlotId);
    IReadOnlyList<object> GetSummary();
    IReadOnlyList<InventoryLotReadModel> GetExpiringLots(DateOnly todayUtc);
    IReadOnlyList<ReplenishmentRule> GetRules();
    IReadOnlyList<InventoryEntry> GetInventoryEntries();
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

    public ItemDefinition CreateItemDefinition(string name, ItemKind kind)
    {
        lock (_sync)
        {
            var state = LoadState();
            var item = new ItemDefinition(Guid.NewGuid(), name, kind);
            state.ItemDefinitions.Add(new ItemDefinitionState(item.Id, item.Name, item.Kind));
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
    }

    private sealed record ItemDefinitionState(Guid Id, string Name, ItemKind Kind);
    private sealed record InventoryEntryState(Guid Id, Guid ItemDefinitionId, Guid? StorageSlotId);
    private sealed record InventoryLotState(Guid Id, Guid InventoryEntryId, Guid ItemDefinitionId, decimal Quantity, string Unit, DateOnly? ExpiresOn, Guid? StorageSlotId);
    private sealed record ReplenishmentRuleState(Guid Id, Guid ItemDefinitionId, decimal TargetAmount, string Unit, int ExpiryWarningDays, bool IsHidden, bool IsDisabled);
}
