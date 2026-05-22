using Phoodab.Domain;

namespace Phoodab.Application;

public sealed class InventoryMvpStore
{
    private readonly List<ItemDefinition> _itemDefinitions = [];
    private readonly List<InventoryEntry> _inventoryEntries = [];
    private readonly List<InventoryLot> _inventoryLots = [];
    private readonly List<ReplenishmentRule> _rules = [];

    public ItemDefinition CreateItemDefinition(string name, ItemKind kind)
    {
        var item = new ItemDefinition(Guid.NewGuid(), name, kind);
        _itemDefinitions.Add(item);
        return item;
    }

    public InventoryEntry? CreateInventoryEntry(Guid itemDefinitionId, Guid? storageSlotId)
    {
        var item = _itemDefinitions.SingleOrDefault(i => i.Id == itemDefinitionId);
        if (item is null)
        {
            return null;
        }

        var entry = new InventoryEntry(Guid.NewGuid(), item, storageSlotId);
        _inventoryEntries.Add(entry);

        if (_rules.All(r => r.ItemDefinitionId != itemDefinitionId))
        {
            _rules.Add(new ReplenishmentRule(Guid.NewGuid(), itemDefinitionId, Quantity.From(2), new Unit("unit")));
        }

        return entry;
    }

    public InventoryLot? AddInventoryLot(Guid inventoryEntryId, decimal quantity, string unit, DateOnly? expiresOn, Guid? storageSlotId)
    {
        var entry = _inventoryEntries.SingleOrDefault(e => e.Id == inventoryEntryId);
        if (entry is null)
        {
            return null;
        }

        var lot = new InventoryLot(Guid.NewGuid(), entry.ItemDefinitionId, Quantity.From(quantity), new Unit(unit), expiresOn, storageSlotId);
        _inventoryLots.Add(lot);

        var existingRule = _rules.SingleOrDefault(r => r.ItemDefinitionId == entry.ItemDefinitionId);
        if (existingRule is not null && string.Equals(existingRule.Unit.Value, "unit", StringComparison.OrdinalIgnoreCase))
        {
            _rules.Remove(existingRule);
            _rules.Add(new ReplenishmentRule(existingRule.Id, existingRule.ItemDefinitionId, existingRule.TargetAmount, new Unit(unit), existingRule.ExpiryWarningDays, existingRule.IsHidden, existingRule.IsDisabled));
        }

        return lot;
    }

    public IReadOnlyList<object> GetSummary()
    {
        return _inventoryEntries.Select(e =>
        {
            var lots = _inventoryLots.Where(l => l.ItemDefinitionId == e.ItemDefinitionId).ToList();
            return new
            {
                inventoryEntryId = e.Id,
                itemDefinitionId = e.ItemDefinitionId,
                itemName = e.ItemDefinition.Name,
                totalQuantity = lots.Sum(l => l.Quantity.Value),
                unit = lots.FirstOrDefault()?.Unit.Value,
                lotCount = lots.Count
            } as object;
        }).ToList();
    }

    public IReadOnlyList<InventoryLotReadModel> GetExpiringLots(DateOnly todayUtc)
    {
        return [.. _inventoryLots
            .Select(lot => InventoryLotReadModel.From(lot, todayUtc))
            .Where(lot => lot.ExpiryStatus is "Expired" or "Urgent" or "Soon")];
    }

    public IReadOnlyList<ReplenishmentRule> GetRules() => _rules;

    public IReadOnlyList<InventoryEntry> GetInventoryEntries()
    {
        return _inventoryEntries.Select(e =>
        {
            var projected = new InventoryEntry(e.Id, e.ItemDefinition, e.StorageSlotId);
            foreach (var lot in _inventoryLots.Where(l => l.ItemDefinitionId == e.ItemDefinitionId))
            {
                projected.AddLot(lot);
            }

            return projected;
        }).ToList();
    }
}
