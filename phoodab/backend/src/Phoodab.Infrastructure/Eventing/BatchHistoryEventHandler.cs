using Phoodab.Application;
using Yaref92.Events.Abstractions;

namespace Phoodab.Infrastructure.Eventing;

public sealed class BatchHistoryEventHandler(IEventHistoryStore eventHistoryStore) : IAsyncEventHandler<BatchHistoryEvent>
{
    public Task OnNextAsync(BatchHistoryEvent @event, CancellationToken cancellationToken = default)
    {
        var record = new EventHistoryRecord(
            EventId: @event.EventIdValue,
            AggregateRef: @event.AggregateRef,
            OccurredAt: @event.OccurredAtUtc,
            Actor: @event.Actor,
            Source: @event.Source,
            CorrelationId: @event.CorrelationId,
            ImportBatchId: @event.ImportBatchId,
            EventType: @event.EventType,
            Payload: @event.Payload);

        return eventHistoryStore.AppendAsync(record, cancellationToken);
    }
}
