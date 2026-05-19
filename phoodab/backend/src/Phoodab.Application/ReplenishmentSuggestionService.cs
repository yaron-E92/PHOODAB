using Phoodab.Domain;

namespace Phoodab.Application;

public sealed record ReplenishmentSuggestionReadModel(
    Guid ItemDefinitionId,
    string ItemName,
    decimal CurrentQuantity,
    decimal DesiredQuantity,
    decimal RequiredAmount,
    string Unit);

public sealed class ReplenishmentSuggestionService
{
    public IReadOnlyList<ReplenishmentSuggestionReadModel> GetSuggestions(
        IEnumerable<ReplenishmentRule> rules,
        IEnumerable<InventoryEntry> inventoryEntries)
    {
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
            results.Add(new ReplenishmentSuggestionReadModel(
                rule.ItemDefinitionId,
                entry?.ItemDefinition.Name ?? "Unknown Item",
                currentAndValid.currentAmount,
                rule.TargetAmount.Value,
                requiredAmount,
                rule.Unit.Symbol));
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
