using System.Text.Json;
using Confluent.Kafka;
using FlowForge.Contracts;
using FlowForge.Outbox;
using FlowForge.Worker.Data;
using FlowForge.Worker.Steps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

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
        await Task.Yield();

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

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetPartitionsAssignedHandler((_, partitions) =>
            {
                logger.LogInformation(
                    "Worker {WorkerId} assigned Kafka partitions: {Partitions}",
                    _workerId,
                    FormatPartitions(partitions));
            })
            .SetPartitionsRevokedHandler((_, partitions) =>
            {
                logger.LogInformation(
                    "Worker {WorkerId} revoked Kafka partitions: {Partitions}",
                    _workerId,
                    FormatPartitions(partitions));
            })
            .Build();
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

        var alreadyProcessed = await db.ProcessedMessages
            .AnyAsync(message => message.MessageId == envelope.MessageId, ct);
        if (alreadyProcessed)
        {
            consumer.Commit(result);
            return;
        }

        var work = ResolveWork(envelope);
        if (work is null)
        {
            consumer.Commit(result);
            return;
        }

        var (runId, step, steps) = work.Value;
        logger.LogInformation(
            "Worker {WorkerId} processing {EventType} for run {RunId}, step {StepNo} from {TopicPartitionOffset}.",
            _workerId,
            envelope.EventType,
            runId,
            step.StepNo,
            result.TopicPartitionOffset);

        var execution = await RunStepWithRetryAsync(db, executor, runId, step, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        alreadyProcessed = await db.ProcessedMessages
            .AnyAsync(message => message.MessageId == envelope.MessageId, ct);
        if (alreadyProcessed)
        {
            await tx.RollbackAsync(ct);
            consumer.Commit(result);
            return;
        }

        db.JobStepRuns.Add(new JobStepRun
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            StepNo = step.StepNo,
            Status = "Completed",
            WorkerId = _workerId,
            AttemptCount = execution.Attempt,
            StartedAt = execution.StartedAt,
            FinishedAt = execution.FinishedAt,
            LastHeartbeatAt = execution.FinishedAt
        });

        db.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId = envelope.MessageId,
            Consumer = ConsumerName,
            ProcessedAt = execution.FinishedAt
        });

        var nextEnvelope = CreateNextEnvelope(runId, step.StepNo, steps, execution.FinishedAt);
        db.OutboxMessages.Add(OutboxMessage.From(runId, nextEnvelope));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        consumer.Commit(result);
    }

    private async Task<StepExecutionResult> RunStepWithRetryAsync(
        WorkerDbContext db,
        StepExecutor executor,
        Guid runId,
        JobStepDefinition step,
        CancellationToken ct)
    {
        var previousAttempts = await db.JobStepRuns
            .Where(run => run.RunId == runId && run.StepNo == step.StepNo)
            .Select(run => (int?)run.AttemptCount)
            .MaxAsync(ct) ?? 0;

        var currentStartedAt = DateTimeOffset.UtcNow;
        var attemptsStarted = 0;

        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = Math.Max(0, step.MaxRetries),
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = false,
                OnRetry = async args =>
                {
                    var attempt = previousAttempts + args.AttemptNumber + 1;
                    await RecordFailedAttemptAsync(runId, step, attempt, currentStartedAt, args.Outcome.Exception!, ct);
                    currentStartedAt = DateTimeOffset.UtcNow;
                }
            })
            .Build();

        try
        {
            await pipeline.ExecuteAsync(async token =>
            {
                attemptsStarted++;
                await executor.RunAsync(step, token);
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var attempt = previousAttempts + attemptsStarted;
            await RecordFailedAttemptAsync(runId, step, attempt, currentStartedAt, ex, ct);
            throw;
        }

        return new StepExecutionResult(
            previousAttempts + attemptsStarted,
            currentStartedAt,
            DateTimeOffset.UtcNow);
    }

    private async Task RecordFailedAttemptAsync(
        Guid runId,
        JobStepDefinition step,
        int attempt,
        DateTimeOffset startedAt,
        Exception exception,
        CancellationToken ct)
    {
        var failedAt = DateTimeOffset.UtcNow;

        logger.LogWarning(
            exception,
            "Step attempt failed for run {RunId}, step {StepNo}, attempt {Attempt}.",
            runId,
            step.StepNo,
            attempt);

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkerDbContext>();
        var existing = await db.JobStepRuns.SingleOrDefaultAsync(
            run => run.RunId == runId && run.StepNo == step.StepNo && run.AttemptCount == attempt,
            ct);

        if (existing is null)
        {
            db.JobStepRuns.Add(new JobStepRun
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                StepNo = step.StepNo,
                Status = "Failed",
                WorkerId = _workerId,
                AttemptCount = attempt,
                StartedAt = startedAt,
                FinishedAt = failedAt,
                LastHeartbeatAt = failedAt,
                Error = exception.Message
            });
        }
        else
        {
            existing.Status = "Failed";
            existing.WorkerId = _workerId;
            existing.StartedAt = startedAt;
            existing.FinishedAt = failedAt;
            existing.LastHeartbeatAt = failedAt;
            existing.Error = exception.Message;
        }

        await db.SaveChangesAsync(ct);
    }

    private static string FormatPartitions(IEnumerable<TopicPartition> partitions)
    {
        return string.Join(", ", partitions.Select(partition =>
            $"{partition.Topic}[{partition.Partition.Value}]"));
    }

    private static string FormatPartitions(IEnumerable<TopicPartitionOffset> partitions)
    {
        return string.Join(", ", partitions.Select(partition =>
            $"{partition.Topic}[{partition.Partition.Value}]@{partition.Offset.Value}"));
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

    private sealed record StepExecutionResult(
        int Attempt,
        DateTimeOffset StartedAt,
        DateTimeOffset FinishedAt);
}
