using System.Text.Json;
using FlowForge.Contracts;

namespace FlowForge.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public Guid AggregateId { get; set; }
    public string? Topic { get; set; }
    public required string EventType { get; set; }
    public required JsonDocument Payload { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }
    public int AttemptCount { get; set; }

    public static OutboxMessage From(Guid aggregateId, EventEnvelope envelope, string? topic = null)
    {
        return new OutboxMessage
        {
            Id = envelope.MessageId,
            AggregateId = aggregateId,
            Topic = topic,
            EventType = envelope.EventType,
            Payload = JsonSerializer.SerializeToDocument(envelope, ContractJson.Options),
            OccurredAt = envelope.OccurredAt,
            AttemptCount = 0
        };
    }

    public static OutboxMessage FromPayload(
        Guid aggregateId,
        string eventType,
        JsonDocument payload,
        DateTimeOffset occurredAt,
        string topic)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            AggregateId = aggregateId,
            Topic = topic,
            EventType = eventType,
            Payload = payload,
            OccurredAt = occurredAt,
            AttemptCount = 0
        };
    }
}
