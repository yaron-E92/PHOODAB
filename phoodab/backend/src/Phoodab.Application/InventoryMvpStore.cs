using Phoodab.Domain;

namespace Phoodab.Application;

public sealed class InventoryMvpStore
{
    private readonly List<ItemDefinition> _itemDefinitions = [];
    private readonly List<InventoryEntry> _inventoryEntries = [];
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
            var unit = entry.Lots.FirstOrDefault()?.Unit ?? new Unit("unit");
            _rules.Add(new ReplenishmentRule(Guid.NewGuid(), itemDefinitionId, Quantity.From(2), unit));
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
        entry.AddLot(lot);

        var existingRule = _rules.SingleOrDefault(r => r.ItemDefinitionId == entry.ItemDefinitionId);
        if (existingRule is not null && string.Equals(existingRule.Unit.Symbol, "unit", StringComparison.OrdinalIgnoreCase))
        {
            _rules.Remove(existingRule);
            _rules.Add(new ReplenishmentRule(existingRule.Id, existingRule.ItemDefinitionId, existingRule.TargetAmount, new Unit(unit), existingRule.ExpiryWarningDays, existingRule.IsHidden, existingRule.IsDisabled));
        }

        return lot;
    }

    public IReadOnlyList<object> GetSummary()
    {
        return _inventoryEntries.Select(e => new
        {
            inventoryEntryId = e.Id,
            itemDefinitionId = e.ItemDefinitionId,
            itemName = e.ItemDefinition.Name,
            totalQuantity = e.Lots.Sum(l => l.Quantity.Value),
            unit = e.Lots.FirstOrDefault()?.Unit.Symbol,
            lotCount = e.Lots.Count
        } as object).ToList();
    }

    public IReadOnlyList<InventoryLotReadModel> GetExpiringLots(DateOnly todayUtc, InventoryLotExpiryCalculator expiryCalculator)
    {
        return _inventoryEntries
            .SelectMany(e => e.Lots)
            .Select(lot => expiryCalculator.ToReadModel(lot, todayUtc))
            .Where(lot => lot.ExpiryStatus is "Expired" or "Urgent" or "Soon")
            .ToList();
    }

    public IReadOnlyList<ReplenishmentRule> GetRules() => _rules;
    public IReadOnlyList<InventoryEntry> GetInventoryEntries() => _inventoryEntries;
}
