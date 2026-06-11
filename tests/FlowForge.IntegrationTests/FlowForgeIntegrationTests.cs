using System.Text.Json;
using Confluent.Kafka;
using FlowForge.Contracts;
using FlowForge.Outbox;
using FlowForge.Worker.Data;
using FlowForge.Worker.Steps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace FlowForge.IntegrationTests;

[Collection(FlowForgeCollection.Name)]
public sealed class FlowForgeIntegrationTests(FlowForgeFixture fixture)
{
    [Fact]
    public async Task Outbox_event_survives_kafka_downtime()
    {
        await fixture.ResetAsync();

        var runId = Guid.NewGuid();
        var envelope = EventEnvelope.From(new JobRunCompleted(runId));
        await using (var db = fixture.CreateDbContext())
        {
            db.OutboxMessages.Add(OutboxMessage.From(runId, envelope));
            await db.SaveChangesAsync();
        }

        await fixture.StopKafkaAsync();
        using var host = IntegrationTestHost.CreateOutboxPublisherHost(fixture);
        try
        {
            await host.StartAsync();

            await Task.Delay(TimeSpan.FromSeconds(2));
            await using (var db = fixture.CreateDbContext())
            {
                var message = await db.OutboxMessages.SingleAsync(message => message.Id == envelope.MessageId);
                Assert.Null(message.PublishedAt);
            }

            await fixture.StartKafkaAsync();

            await WaitUntilAsync(async () =>
            {
                await using var db = fixture.CreateDbContext();
                return await db.OutboxMessages
                    .AnyAsync(message => message.Id == envelope.MessageId && message.PublishedAt != null);
            }, TimeSpan.FromSeconds(90));

            using var consumer = fixture.CreateConsumer($"outbox-downtime-{Guid.NewGuid():N}");
            consumer.Subscribe(KafkaTopics.JobEvents);
            var published = await ConsumeEventAsync(consumer, nameof(JobRunCompleted), runId, TimeSpan.FromSeconds(30));

            Assert.Equal(envelope.MessageId, published.MessageId);
        }
        finally
        {
            await fixture.StartKafkaAsync();
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Duplicate_message_is_processed_exactly_once()
    {
        await fixture.ResetAsync();

        using var host = IntegrationTestHost.CreateWorkerHost(fixture);
        try
        {
            await host.StartAsync();

            var runId = Guid.NewGuid();
            var step = CreateStep(1, "NotifyUsers", durationMs: 10);
            var envelope = EventEnvelope.From(new JobRunRequested(runId, Guid.NewGuid(), [step]));

            using (var producer = fixture.CreateProducer())
            {
                await ProduceAsync(producer, envelope, runId);
                await ProduceAsync(producer, envelope, runId);
                producer.Flush(TimeSpan.FromSeconds(10));
            }

            await WaitUntilAsync(async () =>
            {
                await using var db = fixture.CreateDbContext();
                return await db.JobStepRuns.CountAsync(run =>
                        run.RunId == runId && run.StepNo == 1 && run.Status == "Completed") == 1
                    && await db.ProcessedMessages.CountAsync(message => message.MessageId == envelope.MessageId) == 1;
            }, TimeSpan.FromSeconds(60));

            await using (var db = fixture.CreateDbContext())
            {
                Assert.Equal(1, await db.JobStepRuns.CountAsync(run =>
                    run.RunId == runId && run.StepNo == 1 && run.Status == "Completed"));
                Assert.Equal(1, await db.ProcessedMessages.CountAsync(message => message.MessageId == envelope.MessageId));
            }
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Failed_step_triggers_full_compensation_chain()
    {
        await fixture.ResetAsync();

        using var host = IntegrationTestHost.CreateWorkerHost(fixture);
        try
        {
            await host.StartAsync();

            var runId = Guid.NewGuid();
            var steps = new[]
            {
                CreateStep(1, "ExtractData", durationMs: 10),
                CreateStep(2, "TransformData", durationMs: 10),
                CreateStep(3, "GenerateReport", durationMs: 10, chaosFailRate: 1, maxRetries: 0),
                CreateStep(4, "NotifyUsers", durationMs: 10)
            };
            var envelope = EventEnvelope.From(new JobRunRequested(runId, Guid.NewGuid(), steps));

            using (var producer = fixture.CreateProducer())
            {
                await ProduceAsync(producer, envelope, runId);
                producer.Flush(TimeSpan.FromSeconds(10));
            }

            await WaitUntilAsync(async () =>
            {
                await using var db = fixture.CreateDbContext();
                return await db.OutboxMessages.AnyAsync(message =>
                    message.AggregateId == runId
                    && message.EventType == nameof(JobRunFailed)
                    && message.PublishedAt != null);
            }, TimeSpan.FromSeconds(120));

            await using (var db = fixture.CreateDbContext())
            {
                var compensated = await db.JobStepRuns
                    .Where(run => run.RunId == runId && run.Status == "Compensated")
                    .OrderBy(run => run.StartedAt)
                    .Select(run => run.StepNo)
                    .ToListAsync();

                Assert.Equal([2, 1], compensated);
            }

            using var consumer = fixture.CreateConsumer($"failed-chain-{Guid.NewGuid():N}");
            consumer.Subscribe(KafkaTopics.JobEvents);
            var failed = await ConsumeEventAsync(consumer, nameof(JobRunFailed), runId, TimeSpan.FromSeconds(30));
            var payload = failed.DeserializePayload<JobRunFailed>();

            Assert.Equal(3, payload.FailedStep);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static async Task ProduceAsync(
        IProducer<string, string> producer,
        EventEnvelope envelope,
        Guid runId)
    {
        await producer.ProduceAsync(
            KafkaTopics.JobEvents,
            new Message<string, string>
            {
                Key = runId.ToString(),
                Value = JsonSerializer.Serialize(envelope, ContractJson.Options)
            });
    }

    private static async Task<EventEnvelope> ConsumeEventAsync(
        IConsumer<string, string> consumer,
        string eventType,
        Guid runId,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var result = consumer.Consume(TimeSpan.FromMilliseconds(500));
            if (result is null)
            {
                continue;
            }

            var envelope = JsonSerializer.Deserialize<EventEnvelope>(result.Message.Value, ContractJson.Options);
            if (envelope?.EventType == eventType && envelope.Payload.TryGetProperty("runId", out var runIdElement)
                && runIdElement.GetGuid() == runId)
            {
                return envelope;
            }

            await Task.Yield();
        }

        throw new TimeoutException($"Event {eventType} for run {runId} was not consumed within {timeout}.");
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException($"Condition was not met within {timeout}.");
    }

    private static JobStepDefinition CreateStep(
        int stepNo,
        string stepType,
        int durationMs,
        double chaosFailRate = 0,
        int maxRetries = 3)
    {
        return new JobStepDefinition(
            Guid.NewGuid(),
            stepNo,
            stepType,
            JsonSerializer.SerializeToElement(
                new
                {
                    duration_ms = durationMs,
                    chaos_fail_rate = chaosFailRate
                },
                ContractJson.Options),
            maxRetries);
    }
}
