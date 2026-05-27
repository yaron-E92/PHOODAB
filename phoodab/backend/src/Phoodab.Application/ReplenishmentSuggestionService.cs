using Phoodab.Domain;

namespace Phoodab.Application;

public sealed class ReplenishmentSuggestionService
{
    private readonly IUtcDateProvider _utcDateProvider;

    public ReplenishmentSuggestionService(IUtcDateProvider utcDateProvider)
    {
        _utcDateProvider = utcDateProvider;
    }

    public IReadOnlyList<ReplenishmentSuggestionReadModel> GetSuggestions(
        IEnumerable<ReplenishmentRule> rules,
        IEnumerable<ConsumableEntry> consumableEntries)
    {
        var todayUtc = _utcDateProvider.TodayUtc;
        var entriesByItem = consumableEntries.GroupBy(e => e.ItemDefinitionId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var results = new List<ReplenishmentSuggestionReadModel>();

        foreach (var rule in rules)
        {
            if (rule.IsHidden || rule.IsDisabled)
            {
                continue;
            }

            entriesByItem.TryGetValue(rule.ItemDefinitionId, out var entries);
            var currentAndValid = GetCurrentAmount(entries, rule.Unit);
            if (!currentAndValid.isValid)
            {
                continue;
            }

            var requiredAmount = Math.Max(0, rule.TargetAmount.Value - currentAndValid.currentAmount);
            var readModels = entries?.Select(entry => ConsumableEntryReadModel.From(entry, todayUtc, rule.ExpiryWarningDays)).ToList() ?? new List<ConsumableEntryReadModel>();
            results.Add(new ReplenishmentSuggestionReadModel(
                rule.ItemDefinitionId,
                entries?.FirstOrDefault()?.ItemDefinition.Name ?? "Unknown Item",
                currentAndValid.currentAmount,
                rule.TargetAmount.Value,
                requiredAmount,
                rule.Unit.Value,
                readModels));
        }

        return results;
    }

    private static (decimal currentAmount, bool isValid) GetCurrentAmount(IReadOnlyList<ConsumableEntry>? entries, Unit expectedUnit)
    {
        if (entries is null)
        {
            return (0, true);
        }

        if (entries.Any(entry => !string.Equals(entry.Unit.Value, expectedUnit.Value, StringComparison.OrdinalIgnoreCase)))
        {
            return (0, false);
        }

        return (entries.Sum(entry => entry.Quantity.Value), true);
    }

}
