using Microsoft.Extensions.DependencyInjection;
using Phoodab.Application;

namespace Phoodab.Mobile.Shared;

public static class PhoodabPresentationServiceCollectionExtensions
{
    public static IServiceCollection AddPhoodabSharedPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IUtcDateProvider, SystemUtcDateProvider>();
        services.AddSingleton<ReplenishmentSuggestionService>();
        services.AddSingleton<IInventoryMvpStore, FileInventoryMvpStore>();

        return services;
    }
}
