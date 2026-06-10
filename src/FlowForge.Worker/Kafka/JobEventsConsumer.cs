using System.Text.Json;
using Confluent.Kafka;
using FlowForge.Contracts;
using FlowForge.Outbox;
using FlowForge.Worker.Data;
using FlowForge.Worker.Steps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FlowForge.Worker.Kafka;

public sealed class JobEventsConsumer(
    IServiceScopeFactory scopes,
    IOptions<KafkaOptions> options,
    ILogger<JobEventsConsumer> logger)
    : BackgroundService
{
    private const string ConsumerName = "worker";
    private readonly string _workerId = Environment.MachineName;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            GroupId = "flowforge-workers",
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            SessionTimeoutMs = 10000,
            MaxPollIntervalMs = 300000,
            PartitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(KafkaTopics.JobEvents);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    await ProcessMessageAsync(consumer, result, stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    logger.LogError(ex, "Kafka consume failed.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Worker message processing failed.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Worker consumer is stopping.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Worker consumer stopped unexpectedly.");
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task ProcessMessageAsync(
        IConsumer<string, string> consumer,
        ConsumeResult<string, string> result,
        CancellationToken ct)
    {
        EventEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<EventEnvelope>(result.Message.Value, ContractJson.Options)
                ?? throw new JsonException("Event envelope payload is empty.");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Invalid event envelope at {TopicPartitionOffset}.", result.TopicPartitionOffset);
            consumer.Commit(result);
            return;
        }

        if (envelope.EventType is not nameof(JobRunRequested) and not nameof(StepCompleted))
        {
            consumer.Commit(result);
            return;
        }

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkerDbContext>();
        var executor = scope.ServiceProvider.GetRequiredService<StepExecutor>();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var alreadyProcessed = await db.ProcessedMessages
            .AnyAsync(message => message.MessageId == envelope.MessageId, ct);
        if (alreadyProcessed)
        {
            await tx.RollbackAsync(ct);
            consumer.Commit(result);
            return;
        }

        var work = ResolveWork(envelope);
        if (work is null)
        {
            await tx.RollbackAsync(ct);
            consumer.Commit(result);
            return;
        }

        var (runId, step, steps) = work.Value;
        var startedAt = DateTimeOffset.UtcNow;
        await executor.RunAsync(step, ct);

        var finishedAt = DateTimeOffset.UtcNow;
        db.JobStepRuns.Add(new JobStepRun
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            StepNo = step.StepNo,
            Status = "Completed",
            WorkerId = _workerId,
            AttemptCount = 1,
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            LastHeartbeatAt = finishedAt
        });

        db.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId = envelope.MessageId,
            Consumer = ConsumerName,
            ProcessedAt = finishedAt
        });

        var nextEnvelope = CreateNextEnvelope(runId, step.StepNo, steps, finishedAt);
        db.OutboxMessages.Add(OutboxMessage.From(runId, nextEnvelope));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        consumer.Commit(result);
    }

    private static (Guid RunId, JobStepDefinition Step, IReadOnlyList<JobStepDefinition> Steps)? ResolveWork(
        EventEnvelope envelope)
    {
        if (envelope.EventType == nameof(JobRunRequested))
        {
            var requested = envelope.DeserializePayload<JobRunRequested>();
            var firstStep = requested.Steps.OrderBy(step => step.StepNo).FirstOrDefault();
            return firstStep is null ? null : (requested.RunId, firstStep, requested.Steps);
        }

        var completed = envelope.DeserializePayload<StepCompleted>();
        var nextStep = completed.Steps
            .OrderBy(step => step.StepNo)
            .FirstOrDefault(step => step.StepNo == completed.StepNo + 1);

        return nextStep is null ? null : (completed.RunId, nextStep, completed.Steps);
    }

    private static EventEnvelope CreateNextEnvelope(
        Guid runId,
        int completedStepNo,
        IReadOnlyList<JobStepDefinition> steps,
        DateTimeOffset occurredAt)
    {
        var maxStepNo = steps.Max(step => step.StepNo);
        if (completedStepNo < maxStepNo)
        {
            var completed = new StepCompleted(
                runId,
                completedStepNo,
                JsonSerializer.SerializeToElement(new { status = "Completed" }, ContractJson.Options),
                steps);

            return EventEnvelope.From(completed, occurredAt: occurredAt);
        }

        return EventEnvelope.From(new JobRunCompleted(runId), occurredAt: occurredAt);
    }
}
