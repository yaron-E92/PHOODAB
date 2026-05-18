namespace Phoodab.Application;

public interface IEventHistoryStore
{
    Task AppendAsync(EventHistoryRecord record, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EventHistoryRecord>> QueryAsync(EventHistoryQuery query, CancellationToken cancellationToken = default);
}

public sealed record EventHistoryRecord(
    Guid EventId,
    string AggregateRef,
    DateTimeOffset OccurredAt,
    string Actor,
    string Source,
    string CorrelationId,
    string ImportBatchId,
    string EventType,
    string Payload);

public sealed record EventHistoryQuery(
    string? AggregateRef = null,
    string? CorrelationId = null,
    string? ImportBatchId = null,
    DateTimeOffset? FromOccurredAt = null,
    DateTimeOffset? ToOccurredAt = null,
    int? Take = null);

public sealed class InMemoryEventHistoryStore : IEventHistoryStore
{
    private readonly List<EventHistoryRecord> _records = new();
    private readonly Lock _lock = new();

    public Task AppendAsync(EventHistoryRecord record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            _records.Add(record);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EventHistoryRecord>> QueryAsync(EventHistoryQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<EventHistoryRecord> results;
        lock (_lock)
        {
            IEnumerable<EventHistoryRecord> filtered = _records;

            if (!string.IsNullOrWhiteSpace(query.AggregateRef))
            {
                filtered = filtered.Where(x => x.AggregateRef == query.AggregateRef);
            }

            if (!string.IsNullOrWhiteSpace(query.CorrelationId))
            {
                filtered = filtered.Where(x => x.CorrelationId == query.CorrelationId);
            }

            if (!string.IsNullOrWhiteSpace(query.ImportBatchId))
            {
                filtered = filtered.Where(x => x.ImportBatchId == query.ImportBatchId);
            }

            if (query.FromOccurredAt is not null)
            {
                filtered = filtered.Where(x => x.OccurredAt >= query.FromOccurredAt.Value);
            }

            if (query.ToOccurredAt is not null)
            {
                filtered = filtered.Where(x => x.OccurredAt <= query.ToOccurredAt.Value);
            }

            filtered = filtered.OrderBy(x => x.OccurredAt);

            if (query.Take is > 0)
            {
                filtered = filtered.Take(query.Take.Value);
            }

            results = filtered.ToList();
        }

        return Task.FromResult<IReadOnlyList<EventHistoryRecord>>(results);
    }
}
