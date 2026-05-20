namespace Phoodab.Application;

public interface IUtcDateProvider
{
    DateOnly TodayUtc { get; }
}
