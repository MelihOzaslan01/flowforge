using FlowForge.Contracts;
using FlowForge.Worker.Kafka;

namespace FlowForge.UnitTests;

public sealed class WorkerStepLogFactoryTests
{
    [Fact]
    public void Creates_structured_log_outbox_message_for_job_logs_topic()
    {
        var runId = Guid.NewGuid();
        var startedAt = DateTimeOffset.Parse("2026-06-11T08:00:00Z");
        var finishedAt = startedAt.AddMilliseconds(1250);

        var message = WorkerStepLogFactory.Create(
            runId,
            2,
            "TransformData",
            "Information",
            "worker-1",
            "Step 2 completed.",
            3,
            finishedAt,
            startedAt);

        Assert.Equal(runId, message.AggregateId);
        Assert.Equal(KafkaTopics.JobLogs, message.Topic);
        Assert.Equal("WorkerStepLog", message.EventType);
        Assert.Equal(finishedAt, message.OccurredAt);

        var payload = message.Payload.RootElement;
        Assert.Equal(runId, payload.GetProperty("runId").GetGuid());
        Assert.Equal(2, payload.GetProperty("stepNo").GetInt32());
        Assert.Equal("TransformData", payload.GetProperty("stepType").GetString());
        Assert.Equal("Information", payload.GetProperty("level").GetString());
        Assert.Equal("worker-1", payload.GetProperty("workerId").GetString());
        Assert.Equal("Step 2 completed.", payload.GetProperty("message").GetString());
        Assert.Equal(3, payload.GetProperty("attempt").GetInt32());
        Assert.Equal(1250, payload.GetProperty("durationMs").GetInt64());
        Assert.Equal(finishedAt, payload.GetProperty("timestamp").GetDateTimeOffset());
    }
}
