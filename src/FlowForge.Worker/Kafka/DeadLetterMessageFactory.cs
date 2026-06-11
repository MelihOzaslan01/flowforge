using System.Text.Json;
using FlowForge.Contracts;
using FlowForge.Outbox;

namespace FlowForge.Worker.Kafka;

public static class DeadLetterMessageFactory
{
    public static OutboxMessage Create(
        Guid runId,
        EventEnvelope originalMessage,
        Exception exception,
        int attempts,
        string workerId,
        DateTimeOffset occurredAt)
    {
        var payload = JsonSerializer.SerializeToDocument(
            new
            {
                originalMessage,
                exception = exception.ToString(),
                attempts,
                workerId,
                occurredAt
            },
            ContractJson.Options);

        return OutboxMessage.FromPayload(
            runId,
            $"{originalMessage.EventType}.DLQ",
            payload,
            occurredAt,
            KafkaTopics.JobEventsDlq);
    }
}
