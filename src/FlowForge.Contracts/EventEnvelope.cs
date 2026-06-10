using System.Text.Json;

namespace FlowForge.Contracts;

public sealed record EventEnvelope(
    Guid MessageId,
    string EventType,
    DateTimeOffset OccurredAt,
    string? TraceParent,
    int Version,
    JsonElement Payload)
{
    public const int CurrentVersion = 1;

    public static EventEnvelope From<TPayload>(
        TPayload payload,
        string? traceParent = null,
        Guid? messageId = null,
        DateTimeOffset? occurredAt = null)
        where TPayload : notnull
    {
        return new EventEnvelope(
            messageId ?? Guid.NewGuid(),
            typeof(TPayload).Name,
            occurredAt ?? DateTimeOffset.UtcNow,
            traceParent,
            CurrentVersion,
            JsonSerializer.SerializeToElement(payload, ContractJson.Options));
    }

    public TPayload DeserializePayload<TPayload>()
    {
        return Payload.Deserialize<TPayload>(ContractJson.Options)
            ?? throw new JsonException($"Payload could not be deserialized as {typeof(TPayload).Name}.");
    }
}
