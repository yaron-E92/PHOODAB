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
            var currentAndValid = GetCurrentAmount(entries, rule.Unit, todayUtc);
            if (!currentAndValid.isValid)
            {
                continue;
            }

            var expiringSoonAmount = GetExpiringSoonAmount(entries, rule.Unit, todayUtc, rule.ExpiryWarningDays);
            var deficitAmount = Math.Max(0, rule.TargetAmount.Value - currentAndValid.currentAmount);
            var suggestedPurchaseAmount = deficitAmount + expiringSoonAmount;
            var readModels = entries?.Select(entry => ConsumableEntryReadModel.From(entry, todayUtc, rule.ExpiryWarningDays)).ToList() ?? new List<ConsumableEntryReadModel>();
            results.Add(new ReplenishmentSuggestionReadModel(
                rule.ItemDefinitionId,
                entries?.FirstOrDefault()?.ItemDefinition.Name ?? "Unknown Item",
                currentAndValid.currentAmount,
                currentAndValid.currentAmount,
                rule.TargetAmount.Value,
                deficitAmount,
                expiringSoonAmount,
                suggestedPurchaseAmount,
                suggestedPurchaseAmount,
                rule.Unit.Value,
                readModels));
        }

        return results;
    }

    private static (decimal currentAmount, bool isValid) GetCurrentAmount(IReadOnlyList<ConsumableEntry>? entries, Unit expectedUnit, DateOnly todayUtc)
    {
        if (entries is null)
        {
            return (0, true);
        }

        if (entries.Any(entry => !string.Equals(entry.Unit.Value, expectedUnit.Value, StringComparison.OrdinalIgnoreCase)))
        {
            return (0, false);
        }

        return (entries.Where(entry => entry.ExpiresOn is not { } expiresOn || expiresOn >= todayUtc)
            .Sum(entry => entry.Quantity.Value), true);
    }

    private static decimal GetExpiringSoonAmount(IReadOnlyList<ConsumableEntry>? entries, Unit expectedUnit, DateOnly todayUtc, int expiryWarningDays)
    {
        if (entries is null)
        {
            return 0;
        }

        var warningEnd = todayUtc.AddDays(expiryWarningDays);
        return entries
            .Where(entry => string.Equals(entry.Unit.Value, expectedUnit.Value, StringComparison.OrdinalIgnoreCase))
            .Where(entry => entry.ExpiresOn is { } expiresOn && expiresOn >= todayUtc && expiresOn <= warningEnd)
            .Sum(entry => entry.Quantity.Value);
    }

}
