using Phoodab.Domain;

namespace Phoodab.Application;

public sealed record ConsumableEntryReadModel(
    Guid EntryId,
    Guid ItemDefinitionId,
    string ItemName,
    decimal Quantity,
    string Unit,
    DateOnly? ExpiresOn,
    int? ExpiresInDays,
    string ExpiryStatus,
    Guid? StorageSlotId)
{
    public Guid? LocationId => StorageSlotId;

    public static ConsumableEntryReadModel From(ConsumableEntry entry, DateOnly todayUtc, int expiryWarningDays = 2)
    {
        int? expiresInDays = entry.ExpiresOn is null ? null : entry.ExpiresOn.Value.DayNumber - todayUtc.DayNumber;
        return new ConsumableEntryReadModel(
            entry.Id,
            entry.ItemDefinitionId,
            entry.ItemDefinition.Name,
            entry.Quantity.Value,
            entry.Unit.Value,
            entry.ExpiresOn,
            expiresInDays,
            ConsumableEntryExpiryCalculator.GetExpiryStatus(expiresInDays, expiryWarningDays),
            entry.StorageSlotId);
    }
}
