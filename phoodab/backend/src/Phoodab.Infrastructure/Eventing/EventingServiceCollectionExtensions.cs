using Microsoft.Extensions.DependencyInjection;
using Phoodab.Application;
using Yaref92.Events;
using Yaref92.Events.Abstractions;

namespace Phoodab.Infrastructure.Eventing;

public static class EventingServiceCollectionExtensions
{
    public static IServiceCollection AddPhoodabEventing(this IServiceCollection services)
    {
        services.AddSingleton<IEventHistoryStore, InMemoryEventHistoryStore>();
        services.AddSingleton<IAsyncEventHandler<BatchHistoryEvent>, BatchHistoryEventHandler>();

        services.AddSingleton<IEventAggregator>(sp =>
        {
            var aggregator = new EventAggregator();
            aggregator.RegisterEventType<BatchHistoryEvent>();
            aggregator.Subscribe(sp.GetRequiredService<IAsyncEventHandler<BatchHistoryEvent>>());
            return aggregator;
        });

        return services;
    }
}
