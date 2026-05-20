using Phoodab.Domain;

namespace Phoodab.Application;

public sealed class InventoryLotExpiryCalculator
{
    public InventoryLotReadModel ToReadModel(InventoryLot lot, DateOnly todayUtc)
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

    public static string GetExpiryStatus(int? expiresInDays)
    {
        if (expiresInDays is null) return "Unknown";
        if (expiresInDays < 0) return "Expired";
        if (expiresInDays <= 2) return "Urgent";
        if (expiresInDays <= 7) return "Soon";
        return "Safe";
    }
}
