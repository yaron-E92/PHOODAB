using Phoodab.Domain;

namespace Phoodab.Application;

public sealed record InventoryLotReadModel(
    Guid LotId,
    decimal Quantity,
    string Unit,
    DateOnly? ExpiresOn,
    int? ExpiresInDays,
    string ExpiryStatus)
{
    public static InventoryLotReadModel From(InventoryLot lot, DateOnly todayUtc, int expiryWarningDays = 2)
    {
        int? expiresInDays = lot.ExpiresOn is null ? null : lot.ExpiresOn.Value.DayNumber - todayUtc.DayNumber;
        return new InventoryLotReadModel(
            lot.Id,
            lot.Quantity.Value,
            lot.Unit.Value,
            lot.ExpiresOn,
            expiresInDays,
            InventoryLotExpiryCalculator.GetExpiryStatus(expiresInDays, expiryWarningDays));
    }
}
