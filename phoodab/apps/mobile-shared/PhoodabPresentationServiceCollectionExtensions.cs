using Microsoft.Extensions.DependencyInjection;
using Phoodab.Application;

namespace Phoodab.Mobile.Shared;

public static class PhoodabPresentationServiceCollectionExtensions
{
    /// <summary>
    /// Registers host-neutral presentation dependencies that can be reused by
    /// standalone MAUI and SecondBrain presentation hosts.
    /// </summary>
    public static IServiceCollection AddPhoodabSharedPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IUtcDateProvider, SystemUtcDateProvider>();
        services.AddSingleton<ReplenishmentSuggestionService>();
        services.AddSingleton<IInventoryMvpStore, FileInventoryMvpStore>();

        return services;
    }
}
