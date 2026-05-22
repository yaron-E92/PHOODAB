using Yaref92.Events;

namespace Phoodab.Infrastructure.Eventing;

public sealed class BatchHistoryEvent : DomainEventBase
{
    public required Guid EventIdValue { get; init; }
    public required string AggregateRef { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }
    public required string Actor { get; init; }
    public required string Source { get; init; }
    public required string CorrelationId { get; init; }
    public required string ImportBatchId { get; init; }
    public required string EventType { get; init; }
    public required string Payload { get; init; }
}
