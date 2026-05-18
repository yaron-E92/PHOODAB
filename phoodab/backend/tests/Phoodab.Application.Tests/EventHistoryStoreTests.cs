using Phoodab.Application;

namespace Phoodab.Application.Tests;

public class EventHistoryStoreTests
{
    [Test]
    public async Task Append_And_Query_Returns_Required_Metadata_Fields()
    {
        var store = new InMemoryEventHistoryStore();

        var occurredAt = new DateTimeOffset(2026, 5, 16, 12, 0, 0, TimeSpan.Zero);
        var record = new EventHistoryRecord(
            EventId: Guid.NewGuid(),
            AggregateRef: "meal:42",
            OccurredAt: occurredAt,
            Actor: "import-service",
            Source: "csv",
            CorrelationId: "corr-123",
            ImportBatchId: "batch-9",
            EventType: "MealImported",
            Payload: "{}");

        await store.AppendAsync(record);

        var events = await store.QueryAsync(new EventHistoryQuery(AggregateRef: "meal:42"));

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(events[0].EventId, Is.EqualTo(record.EventId));
            Assert.That(events[0].AggregateRef, Is.EqualTo("meal:42"));
            Assert.That(events[0].OccurredAt, Is.EqualTo(occurredAt));
            Assert.That(events[0].Actor, Is.EqualTo("import-service"));
            Assert.That(events[0].Source, Is.EqualTo("csv"));
            Assert.That(events[0].CorrelationId, Is.EqualTo("corr-123"));
            Assert.That(events[0].ImportBatchId, Is.EqualTo("batch-9"));
        });
    }

    [Test]
    public async Task Query_Filters_And_Orders_Timeline_By_OccurredAt_Ascending()
    {
        var store = new InMemoryEventHistoryStore();
        var baseTime = new DateTimeOffset(2026, 5, 16, 12, 0, 0, TimeSpan.Zero);

        await store.AppendAsync(new EventHistoryRecord(Guid.NewGuid(), "meal:1", baseTime.AddMinutes(5), "u", "api", "corr-a", "batch-a", "EventA", "{}"));
        await store.AppendAsync(new EventHistoryRecord(Guid.NewGuid(), "meal:1", baseTime.AddMinutes(1), "u", "api", "corr-a", "batch-a", "EventB", "{}"));
        await store.AppendAsync(new EventHistoryRecord(Guid.NewGuid(), "meal:2", baseTime.AddMinutes(2), "u", "api", "corr-b", "batch-b", "EventC", "{}"));

        var events = await store.QueryAsync(new EventHistoryQuery(AggregateRef: "meal:1", CorrelationId: "corr-a"));

        Assert.That(events.Select(x => x.AggregateRef), Is.All.EqualTo("meal:1"));
        Assert.That(events.Select(x => x.CorrelationId), Is.All.EqualTo("corr-a"));
        Assert.That(events.Select(x => x.OccurredAt), Is.Ordered.Ascending);
    }
}
