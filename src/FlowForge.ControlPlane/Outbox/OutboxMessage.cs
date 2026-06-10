using System.Text.Json;

namespace FlowForge.ControlPlane.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public Guid AggregateId { get; set; }
    public required string EventType { get; set; }
    public required JsonDocument Payload { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }
    public int AttemptCount { get; set; }
}
