using Phoodab.Domain;

namespace Phoodab.Application;

public sealed class ReplenishmentSuggestionService
{
    private readonly IUtcDateProvider _utcDateProvider;
    private readonly InventoryLotExpiryCalculator _inventoryLotExpiryCalculator;

    public ReplenishmentSuggestionService(
        IUtcDateProvider utcDateProvider,
        InventoryLotExpiryCalculator inventoryLotExpiryCalculator)
    {
        _utcDateProvider = utcDateProvider;
        _inventoryLotExpiryCalculator = inventoryLotExpiryCalculator;
    }

    public IReadOnlyList<ReplenishmentSuggestionReadModel> GetSuggestions(
        IEnumerable<ReplenishmentRule> rules,
        IEnumerable<InventoryEntry> inventoryEntries)
    {
        var todayUtc = _utcDateProvider.TodayUtc;
        var entriesByItem = inventoryEntries.ToDictionary(e => e.ItemDefinitionId);
        var results = new List<ReplenishmentSuggestionReadModel>();

        foreach (var rule in rules)
        {
            if (rule.IsHidden || rule.IsDisabled)
            {
                continue;
            }

            entriesByItem.TryGetValue(rule.ItemDefinitionId, out var entry);
            var currentAndValid = GetCurrentAmount(entry, rule.Unit);
            if (!currentAndValid.isValid)
            {
                continue;
            }

            var requiredAmount = Math.Max(0, rule.TargetAmount.Value - currentAndValid.currentAmount);
            var lots = entry?.Lots.Select(lot => _inventoryLotExpiryCalculator.ToReadModel(lot, todayUtc)).ToList() ?? new List<InventoryLotReadModel>();
            results.Add(new ReplenishmentSuggestionReadModel(
                rule.ItemDefinitionId,
                entry?.ItemDefinition.Name ?? "Unknown Item",
                currentAndValid.currentAmount,
                rule.TargetAmount.Value,
                requiredAmount,
                rule.Unit.Symbol,
                lots));
        }

        return results;
    }

    private static (decimal currentAmount, bool isValid) GetCurrentAmount(InventoryEntry? entry, Unit expectedUnit)
    {
        if (entry is null)
        {
            return (0, true);
        }

        if (entry.Lots.Any(lot => !string.Equals(lot.Unit.Symbol, expectedUnit.Symbol, StringComparison.OrdinalIgnoreCase)))
        {
            return (0, false);
        }

        return (entry.Lots.Sum(lot => lot.Quantity.Value), true);
    }

}
