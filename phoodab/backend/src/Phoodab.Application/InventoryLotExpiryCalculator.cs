namespace Phoodab.Application;

public static class InventoryLotExpiryCalculator
{
    public static string GetExpiryStatus(int? expiresInDays, int expiryWarningDays = 2)
    {
        return expiresInDays switch
        {
            null => "Unknown",
            < 0 => "Expired",
            _ when expiresInDays <= expiryWarningDays => "Urgent",
            <= 7 => "Soon",
            _ => "Safe"
        };
    }
}
