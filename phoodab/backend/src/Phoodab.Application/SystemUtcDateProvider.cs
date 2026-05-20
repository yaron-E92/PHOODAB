namespace Phoodab.Application;

public sealed class SystemUtcDateProvider : IUtcDateProvider
{
    public DateOnly TodayUtc => DateOnly.FromDateTime(DateTime.UtcNow);
}
