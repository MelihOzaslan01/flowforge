using System.Text.Json;
using FlowForge.Contracts;

namespace FlowForge.UnitTests;

public sealed class EventEnvelopeTests
{
    [Fact]
    public void Envelope_round_trips_with_camel_case_payload()
    {
        var runId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var jobId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var stepId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var occurredAt = DateTimeOffset.Parse("2026-06-10T14:32:11+00:00");
        var payload = new JobRunRequested(
            runId,
            jobId,
            [
                new JobStepDefinition(
                    stepId,
                    1,
                    "ExtractData",
                    JsonSerializer.SerializeToElement(new { chaos_fail_rate = 0 }, ContractJson.Options),
                    3)
            ]);

        var envelope = EventEnvelope.From(
            payload,
            "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01",
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            occurredAt);

        var json = JsonSerializer.Serialize(envelope, ContractJson.Options);
        var roundTripped = JsonSerializer.Deserialize<EventEnvelope>(json, ContractJson.Options);

        Assert.NotNull(roundTripped);
        Assert.Contains("\"messageId\"", json);
        Assert.Contains("\"eventType\":\"JobRunRequested\"", json);
        Assert.Contains("\"occurredAt\"", json);
        Assert.Contains("\"traceParent\"", json);
        Assert.Contains("\"version\":1", json);
        Assert.Contains("\"payload\"", json);
        Assert.Contains("\"runId\"", json);
        Assert.DoesNotContain("\"MessageId\"", json);
        Assert.Equal(envelope.MessageId, roundTripped.MessageId);
        Assert.Equal(occurredAt, roundTripped.OccurredAt);
        Assert.Equal("00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01", roundTripped.TraceParent);
        Assert.Equal("JobRunRequested", roundTripped.EventType);
        Assert.Equal(EventEnvelope.CurrentVersion, roundTripped.Version);

        var roundTrippedPayload = roundTripped.DeserializePayload<JobRunRequested>();

        Assert.Equal(runId, roundTrippedPayload.RunId);
        Assert.Equal(jobId, roundTrippedPayload.JobId);
        Assert.Single(roundTrippedPayload.Steps);
        Assert.Equal(stepId, roundTrippedPayload.Steps[0].StepId);
        Assert.Equal(1, roundTrippedPayload.Steps[0].StepNo);
        Assert.Equal("ExtractData", roundTrippedPayload.Steps[0].StepType);
        Assert.Equal(3, roundTrippedPayload.Steps[0].MaxRetries);
        Assert.Equal(0, roundTrippedPayload.Steps[0].Config.GetProperty("chaos_fail_rate").GetInt32());
    }

    [Fact]
    public void Event_type_names_match_design_document()
    {
        var runId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var steps = new[]
        {
            new JobStepDefinition(
                Guid.NewGuid(),
                1,
                "ExtractData",
                JsonSerializer.SerializeToElement(new { }, ContractJson.Options),
                3)
        };

        Assert.Equal("JobRunRequested", EventEnvelope.From(new JobRunRequested(runId, Guid.NewGuid(), [])).EventType);
        Assert.Equal("StepStarted", EventEnvelope.From(new StepStarted(runId, 1, "worker-1")).EventType);
        Assert.Equal(
            "StepCompleted",
            EventEnvelope.From(new StepCompleted(runId, 1, JsonSerializer.SerializeToElement(new { ok = true }, ContractJson.Options), [])).EventType);
        Assert.Equal("StepFailed", EventEnvelope.From(new StepFailed(runId, 3, "boom", 3, steps)).EventType);
        Assert.Equal("CompensateStep", EventEnvelope.From(new CompensateStep(runId, 2, 3, steps)).EventType);
        Assert.Equal("StepCompensated", EventEnvelope.From(new StepCompensated(runId, 2, 3, steps)).EventType);
        Assert.Equal("JobRunCompleted", EventEnvelope.From(new JobRunCompleted(runId)).EventType);
        Assert.Equal("JobRunFailed", EventEnvelope.From(new JobRunFailed(runId, 3, "GenerateReport failed")).EventType);
    }

    [Fact]
    public void Kafka_topics_match_design_document()
    {
        Assert.Equal("flowforge.job.events", KafkaTopics.JobEvents);
        Assert.Equal("flowforge.job.events.dlq", KafkaTopics.JobEventsDlq);
        Assert.Equal("flowforge.job.logs", KafkaTopics.JobLogs);
    }
}
