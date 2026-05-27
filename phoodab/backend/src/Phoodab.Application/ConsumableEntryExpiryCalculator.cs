namespace Phoodab.Application;

public static class ConsumableEntryExpiryCalculator
{
    private const int SoonWindowDays = 5;

    public static string GetExpiryStatus(int? expiresInDays, int expiryWarningDays = 2)
    {
        return expiresInDays switch
        {
            null => "Unknown",
            < 0 => "Expired",
            _ when expiresInDays <= expiryWarningDays => "Urgent",
            _ when expiresInDays <= expiryWarningDays + SoonWindowDays => "Soon",
            _ => "Safe"
        };
    }
}
