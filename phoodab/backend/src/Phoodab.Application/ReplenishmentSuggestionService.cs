using Phoodab.Domain;

namespace Phoodab.Application;

public sealed record ReplenishmentSuggestionReadModel(
    Guid ItemDefinitionId,
    string ItemName,
    decimal CurrentQuantity,
    decimal DesiredQuantity,
    decimal RequiredAmount,
    string Unit,
    IReadOnlyList<InventoryLotReadModel> Lots);

public sealed record InventoryLotReadModel(
    Guid LotId,
    decimal Quantity,
    string Unit,
    DateOnly? ExpiresOn,
    int? ExpiresInDays,
    string ExpiryStatus);

public interface IUtcDateProvider
{
    DateOnly TodayUtc { get; }
}

public sealed class SystemUtcDateProvider : IUtcDateProvider
{
    public DateOnly TodayUtc => DateOnly.FromDateTime(DateTime.UtcNow);
}

public sealed class ReplenishmentSuggestionService
{
    private readonly IUtcDateProvider _utcDateProvider;

    public ReplenishmentSuggestionService(IUtcDateProvider utcDateProvider)
    {
        _utcDateProvider = utcDateProvider;
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
            var lots = entry?.Lots.Select(lot => ToLotReadModel(lot, todayUtc)).ToList() ?? new List<InventoryLotReadModel>();
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

    private static InventoryLotReadModel ToLotReadModel(InventoryLot lot, DateOnly todayUtc)
    {
        var expiresInDays = lot.ExpiresOn is null ? null : lot.ExpiresOn.Value.DayNumber - todayUtc.DayNumber;
        return new InventoryLotReadModel(
            lot.Id,
            lot.Quantity.Value,
            lot.Unit.Symbol,
            lot.ExpiresOn,
            expiresInDays,
            GetExpiryStatus(expiresInDays));
    }

    private static string GetExpiryStatus(int? expiresInDays)
    {
        if (expiresInDays is null) return "Unknown";
        if (expiresInDays < 0) return "Expired";
        if (expiresInDays <= 2) return "Urgent";
        if (expiresInDays <= 7) return "Soon";
        return "Safe";
    }
}
