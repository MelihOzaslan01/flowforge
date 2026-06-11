using System.Text.Json;
using FlowForge.Contracts;
using FlowForge.Worker.Kafka;

namespace FlowForge.UnitTests;

public sealed class DeadLetterMessageFactoryTests
{
    [Fact]
    public void Create_uses_run_id_as_aggregate_and_dlq_topic_with_error_metadata()
    {
        var runId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.Parse("2026-06-11T10:20:30+00:00");
        var envelope = EventEnvelope.From(
            new StepCompleted(
                runId,
                2,
                JsonSerializer.SerializeToElement(new { status = "Completed" }, ContractJson.Options),
                [new JobStepDefinition(Guid.NewGuid(), 3, "notify", JsonSerializer.SerializeToElement(new { }), 2)]),
            occurredAt: occurredAt.AddMinutes(-1));

        var exception = new InvalidOperationException("boom");

        var message = DeadLetterMessageFactory.Create(
            runId,
            envelope,
            exception,
            attempts: 3,
            workerId: "worker-a",
            occurredAt: occurredAt);

        Assert.Equal(runId, message.AggregateId);
        Assert.Equal(KafkaTopics.JobEventsDlq, message.Topic);
        Assert.Equal("StepCompleted.DLQ", message.EventType);
        Assert.Equal(occurredAt, message.OccurredAt);

        var payload = message.Payload.RootElement;
        Assert.Equal("StepCompleted", payload.GetProperty("originalMessage").GetProperty("eventType").GetString());
        Assert.Contains("boom", payload.GetProperty("exception").GetString());
        Assert.Equal(3, payload.GetProperty("attempts").GetInt32());
        Assert.Equal("worker-a", payload.GetProperty("workerId").GetString());
        Assert.Equal(occurredAt, payload.GetProperty("occurredAt").GetDateTimeOffset());
    }
}
