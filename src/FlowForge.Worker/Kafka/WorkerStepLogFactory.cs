using System.Text.Json;
using FlowForge.Contracts;
using FlowForge.Outbox;

namespace FlowForge.Worker.Kafka;

public static class WorkerStepLogFactory
{
    public static OutboxMessage Create(
        Guid runId,
        int stepNo,
        string stepType,
        string level,
        string workerId,
        string message,
        int attempt,
        DateTimeOffset timestamp,
        DateTimeOffset? startedAt = null,
        string? error = null)
    {
        long? durationMs = startedAt is null
            ? null
            : Math.Max(0, (long)(timestamp - startedAt.Value).TotalMilliseconds);

        var payload = JsonSerializer.SerializeToDocument(
            new WorkerStepLog(
                runId,
                null,
                stepNo,
                stepType,
                level,
                workerId,
                message,
                error,
                attempt,
                durationMs,
                timestamp),
            ContractJson.Options);

        return OutboxMessage.FromPayload(
            runId,
            "WorkerStepLog",
            payload,
            timestamp,
            KafkaTopics.JobLogs);
    }

    private sealed record WorkerStepLog(
        Guid RunId,
        string? JobName,
        int StepNo,
        string StepType,
        string Level,
        string WorkerId,
        string Message,
        string? Error,
        int Attempt,
        long? DurationMs,
        DateTimeOffset Timestamp);
}
