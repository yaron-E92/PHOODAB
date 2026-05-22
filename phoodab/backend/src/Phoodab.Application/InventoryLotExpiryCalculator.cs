namespace Phoodab.Application;

public static class InventoryLotExpiryCalculator
{
    public static string GetExpiryStatus(int? expiresInDays)
    {
        return expiresInDays switch
        {
            null => "Unknown",
            < 0 => "Expired",
            <= 2 => "Urgent",
            <= 7 => "Soon",
            _ => "Safe"
        };
    }
}
