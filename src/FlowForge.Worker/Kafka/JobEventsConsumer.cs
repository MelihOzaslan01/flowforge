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
    JobLogPublisher jobLogPublisher,
    ILogger<JobEventsConsumer> logger)
    : BackgroundService
{
    private const string ConsumerName = "worker";
    private static readonly TimeSpan ZombieStaleAfter = TimeSpan.FromSeconds(60);
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

        if (envelope.EventType == nameof(StepFailed))
        {
            await ProcessStepFailedAsync(db, envelope, ct);
            consumer.Commit(result);
            return;
        }

        if (envelope.EventType == nameof(CompensateStep))
        {
            await ProcessCompensateStepAsync(db, executor, envelope, ct);
            consumer.Commit(result);
            return;
        }

        if (envelope.EventType == nameof(StepCompensated))
        {
            await ProcessStepCompensatedAsync(db, envelope, ct);
            consumer.Commit(result);
            return;
        }

        if (envelope.EventType is not nameof(JobRunRequested) and not nameof(StepCompleted))
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

        if (await TryCloseZombieRedeliveryAsync(db, envelope.MessageId, runId, step, steps, ct))
        {
            consumer.Commit(result);
            return;
        }

        StepExecutionResult execution;
        try
        {
            execution = await RunStepWithRetryAsync(db, executor, envelope.MessageId, runId, step, steps, ct);
        }
        catch (StepRetriesExhaustedException ex)
        {
            await MoveToDlqAsync(db, envelope, runId, step, steps, ex, ct);
            consumer.Commit(result);
            return;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        alreadyProcessed = await db.ProcessedMessages
            .AnyAsync(message => message.MessageId == envelope.MessageId, ct);
        if (alreadyProcessed)
        {
            await tx.RollbackAsync(ct);
            consumer.Commit(result);
            return;
        }

        var completedRun = await db.JobStepRuns.SingleOrDefaultAsync(
            run => run.RunId == runId && run.StepNo == step.StepNo && run.AttemptCount == execution.Attempt,
            ct);

        if (completedRun is null)
        {
            db.JobStepRuns.Add(new JobStepRun
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                SourceMessageId = envelope.MessageId,
                StepNo = step.StepNo,
                Status = "Completed",
                WorkerId = _workerId,
                AttemptCount = execution.Attempt,
                StartedAt = execution.StartedAt,
                FinishedAt = execution.FinishedAt,
                LastHeartbeatAt = execution.FinishedAt,
                Steps = CreateStepsDocument(steps)
            });
        }
        else
        {
            completedRun.Status = "Completed";
            completedRun.SourceMessageId = envelope.MessageId;
            completedRun.WorkerId = _workerId;
            completedRun.StartedAt = execution.StartedAt;
            completedRun.FinishedAt = execution.FinishedAt;
            completedRun.LastHeartbeatAt = execution.FinishedAt;
            completedRun.Error = null;
            completedRun.Steps = CreateStepsDocument(steps);
        }

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

        jobLogPublisher.Publish(
            runId,
            step.StepNo,
            step.StepType,
            "Information",
            _workerId,
            $"Step {step.StepNo} completed.",
            execution.Attempt,
            execution.FinishedAt,
            execution.StartedAt);

        consumer.Commit(result);
    }

    private async Task<bool> TryCloseZombieRedeliveryAsync(
        WorkerDbContext db,
        Guid messageId,
        Guid runId,
        JobStepDefinition step,
        IReadOnlyList<JobStepDefinition> steps,
        CancellationToken ct)
    {
        var running = await db.JobStepRuns
            .AsNoTracking()
            .Where(run =>
                run.SourceMessageId == messageId
                && run.RunId == runId
                && run.StepNo == step.StepNo
                && run.Status == "Running")
            .OrderByDescending(run => run.AttemptCount)
            .FirstOrDefaultAsync(ct);

        if (running is null)
        {
            return false;
        }

        var staleAt = running.LastHeartbeatAt + ZombieStaleAfter;
        var delay = staleAt - DateTimeOffset.UtcNow;
        if (delay > TimeSpan.Zero)
        {
            logger.LogWarning(
                "Redelivered message {MessageId} for run {RunId}, step {StepNo} has an active Running attempt; waiting {DelaySeconds:F1}s for zombie threshold.",
                messageId,
                runId,
                step.StepNo,
                delay.TotalSeconds);
            await Task.Delay(delay.Add(TimeSpan.FromSeconds(1)), ct);
        }

        var alreadyProcessed = await db.ProcessedMessages
            .AnyAsync(message => message.MessageId == messageId, ct);
        if (alreadyProcessed)
        {
            return true;
        }

        var failedAt = DateTimeOffset.UtcNow;
        var error = $"Zombie step detected during redelivery; heartbeat stale since {running.LastHeartbeatAt:O}.";

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var updated = await db.JobStepRuns
            .Where(run =>
                run.Id == running.Id
                && run.Status == "Running"
                && run.LastHeartbeatAt < failedAt - ZombieStaleAfter)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(run => run.Status, "Failed")
                    .SetProperty(run => run.FinishedAt, failedAt)
                    .SetProperty(run => run.LastHeartbeatAt, failedAt)
                    .SetProperty(run => run.Error, error),
                ct);

        if (updated == 0)
        {
            await tx.RollbackAsync(ct);
            return false;
        }

        db.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId = messageId,
            Consumer = ConsumerName,
            ProcessedAt = failedAt
        });

        var failed = new StepFailed(runId, step.StepNo, error, running.AttemptCount, steps);
        db.OutboxMessages.Add(OutboxMessage.From(runId, EventEnvelope.From(failed, occurredAt: failedAt)));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        jobLogPublisher.Publish(
            runId,
            step.StepNo,
            step.StepType,
            "Error",
            _workerId,
            $"Step {step.StepNo} failed as zombie.",
            running.AttemptCount,
            failedAt,
            running.StartedAt,
            error);

        logger.LogWarning(
            "Redelivered zombie step marked failed for run {RunId}, step {StepNo}, attempt {Attempt}.",
            runId,
            step.StepNo,
            running.AttemptCount);
        return true;
    }

    private async Task ProcessStepFailedAsync(
        WorkerDbContext db,
        EventEnvelope envelope,
        CancellationToken ct)
    {
        var failed = envelope.DeserializePayload<StepFailed>();
        var nextEnvelope = CompensationChain.CreateAfterStepFailed(failed);
        await SaveInboxAndOutboxAsync(db, envelope.MessageId, failed.RunId, nextEnvelope, ct);
    }

    private async Task ProcessCompensateStepAsync(
        WorkerDbContext db,
        StepExecutor executor,
        EventEnvelope envelope,
        CancellationToken ct)
    {
        var compensate = envelope.DeserializePayload<CompensateStep>();
        var step = compensate.Steps.FirstOrDefault(candidate => candidate.StepNo == compensate.StepNo);
        var startedAt = DateTimeOffset.UtcNow;
        var error = await TryCompensateAsync(executor, compensate, ct);
        var compensatedAt = DateTimeOffset.UtcNow;
        var nextEnvelope = CompensationChain.CreateAfterCompensateStep(compensate, compensatedAt);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var alreadyProcessed = await db.ProcessedMessages
            .AnyAsync(message => message.MessageId == envelope.MessageId, ct);
        if (alreadyProcessed)
        {
            await tx.RollbackAsync(ct);
            return;
        }

        var previousAttempt = await db.JobStepRuns
            .Where(run => run.RunId == compensate.RunId && run.StepNo == compensate.StepNo)
            .Select(run => (int?)run.AttemptCount)
            .MaxAsync(ct);
        var nextAttempt = (previousAttempt ?? 0) + 1;

        db.JobStepRuns.Add(new JobStepRun
        {
            Id = Guid.NewGuid(),
            RunId = compensate.RunId,
            StepNo = compensate.StepNo,
            Status = "Compensated",
            WorkerId = _workerId,
            AttemptCount = nextAttempt,
            StartedAt = startedAt,
            FinishedAt = compensatedAt,
            LastHeartbeatAt = compensatedAt,
            Error = error
        });

        db.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId = envelope.MessageId,
            Consumer = ConsumerName,
            ProcessedAt = compensatedAt
        });

        db.OutboxMessages.Add(OutboxMessage.From(compensate.RunId, nextEnvelope));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        jobLogPublisher.Publish(
            compensate.RunId,
            compensate.StepNo,
            step?.StepType ?? "Unknown",
            error is null ? "Information" : "Warning",
            _workerId,
            $"Step {compensate.StepNo} compensated.",
            nextAttempt,
            compensatedAt,
            startedAt,
            error);
    }

    private async Task ProcessStepCompensatedAsync(
        WorkerDbContext db,
        EventEnvelope envelope,
        CancellationToken ct)
    {
        var compensated = envelope.DeserializePayload<StepCompensated>();
        var nextEnvelope = CompensationChain.CreateAfterStepCompensated(compensated);
        await SaveInboxAndOutboxAsync(db, envelope.MessageId, compensated.RunId, nextEnvelope, ct);
    }

    private async Task SaveInboxAndOutboxAsync(
        WorkerDbContext db,
        Guid messageId,
        Guid runId,
        EventEnvelope nextEnvelope,
        CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var alreadyProcessed = await db.ProcessedMessages
            .AnyAsync(message => message.MessageId == messageId, ct);
        if (alreadyProcessed)
        {
            await tx.RollbackAsync(ct);
            return;
        }

        db.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId = messageId,
            Consumer = ConsumerName,
            ProcessedAt = nextEnvelope.OccurredAt
        });

        db.OutboxMessages.Add(OutboxMessage.From(runId, nextEnvelope));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task<string?> TryCompensateAsync(
        StepExecutor executor,
        CompensateStep compensate,
        CancellationToken ct)
    {
        var step = compensate.Steps.FirstOrDefault(candidate => candidate.StepNo == compensate.StepNo);
        if (step is null)
        {
            var error = $"Step definition for compensation step {compensate.StepNo} was not found.";
            logger.LogWarning(
                "Compensation skipped for run {RunId}, step {StepNo}: {Error}",
                compensate.RunId,
                compensate.StepNo,
                error);
            return error;
        }

        try
        {
            await executor.CompensateAsync(step, ct);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Compensation failed for run {RunId}, step {StepNo}; continuing compensation chain.",
                compensate.RunId,
                compensate.StepNo);
            return ex.Message;
        }
    }

    private async Task<StepExecutionResult> RunStepWithRetryAsync(
        WorkerDbContext db,
        StepExecutor executor,
        Guid messageId,
        Guid runId,
        JobStepDefinition step,
        IReadOnlyList<JobStepDefinition> steps,
        CancellationToken ct)
    {
        var previousAttempts = await db.JobStepRuns
            .Where(run => run.RunId == runId && run.StepNo == step.StepNo)
            .Select(run => (int?)run.AttemptCount)
            .MaxAsync(ct) ?? 0;
        if (previousAttempts >= step.MaxRetries + 1)
        {
            var lastError = await db.JobStepRuns
                .AsNoTracking()
                .Where(run => run.RunId == runId && run.StepNo == step.StepNo)
                .OrderByDescending(run => run.AttemptCount)
                .Select(run => run.Error)
                .FirstOrDefaultAsync(ct);

            throw new StepRetriesExhaustedException(
                previousAttempts,
                new InvalidOperationException(lastError ?? "Step retries already exhausted."));
        }

        var currentStartedAt = DateTimeOffset.UtcNow;

        StepRetryResult result;
        try
        {
            result = await StepRetryPipeline.ExecuteAsync(
                previousAttempts,
                step.MaxRetries,
                async (attempt, token) =>
                {
                    currentStartedAt = DateTimeOffset.UtcNow;
                    await RecordRunningAttemptAsync(messageId, runId, step, steps, attempt, currentStartedAt, token);
                    await using var heartbeat = await StepHeartbeat.StartAsync(
                        scopes,
                        runId,
                        step.StepNo,
                        attempt,
                        TimeSpan.FromSeconds(5),
                        logger,
                        token);
                    await executor.RunAsync(step, token);
                },
                async (attempt, token) =>
                {
                    await RecordFailedAttemptAsync(runId, step, attempt.Attempt, currentStartedAt, attempt.Exception, token);
                    currentStartedAt = DateTimeOffset.UtcNow;
                },
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var attempts = await db.JobStepRuns
                .AsNoTracking()
                .Where(run => run.RunId == runId && run.StepNo == step.StepNo)
                .Select(run => (int?)run.AttemptCount)
                .MaxAsync(ct) ?? previousAttempts + step.MaxRetries + 1;

            throw new StepRetriesExhaustedException(attempts, ex);
        }

        return new StepExecutionResult(result.Attempt, currentStartedAt, DateTimeOffset.UtcNow);
    }

    private async Task MoveToDlqAsync(
        WorkerDbContext db,
        EventEnvelope envelope,
        Guid runId,
        JobStepDefinition step,
        IReadOnlyList<JobStepDefinition> steps,
        StepRetriesExhaustedException exception,
        CancellationToken ct)
    {
        var failedAt = DateTimeOffset.UtcNow;
        var error = exception.InnerException?.Message ?? exception.Message;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var alreadyProcessed = await db.ProcessedMessages
            .AnyAsync(message => message.MessageId == envelope.MessageId, ct);
        if (alreadyProcessed)
        {
            await tx.RollbackAsync(ct);
            return;
        }

        db.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId = envelope.MessageId,
            Consumer = ConsumerName,
            ProcessedAt = failedAt
        });

        db.OutboxMessages.Add(DeadLetterMessageFactory.Create(
            runId,
            envelope,
            exception.InnerException ?? exception,
            exception.Attempts,
            _workerId,
            failedAt));

        var stepFailedEnvelope = EventEnvelope.From(
            new StepFailed(runId, step.StepNo, error, exception.Attempts, steps),
            occurredAt: failedAt.AddTicks(1));
        db.OutboxMessages.Add(OutboxMessage.From(runId, stepFailedEnvelope));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        logger.LogError(
            exception.InnerException,
            "Retries exhausted for run {RunId}, step {StepNo}; message moved to DLQ and StepFailed was queued.",
            runId,
            step.StepNo);
    }

    private async Task RecordRunningAttemptAsync(
        Guid messageId,
        Guid runId,
        JobStepDefinition step,
        IReadOnlyList<JobStepDefinition> steps,
        int attempt,
        DateTimeOffset startedAt,
        CancellationToken ct)
    {
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
                SourceMessageId = messageId,
                StepNo = step.StepNo,
                Status = "Running",
                WorkerId = _workerId,
                AttemptCount = attempt,
                StartedAt = startedAt,
                FinishedAt = null,
                LastHeartbeatAt = startedAt,
                Steps = CreateStepsDocument(steps)
            });
        }
        else
        {
            existing.Status = "Running";
            existing.SourceMessageId = messageId;
            existing.WorkerId = _workerId;
            existing.StartedAt = startedAt;
            existing.FinishedAt = null;
            existing.LastHeartbeatAt = startedAt;
            existing.Error = null;
            existing.Steps = CreateStepsDocument(steps);
        }

        await db.SaveChangesAsync(ct);

        jobLogPublisher.Publish(
            runId,
            step.StepNo,
            step.StepType,
            "Information",
            _workerId,
            $"Step {step.StepNo} started.",
            attempt,
            startedAt);
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
                Error = exception.Message,
                Steps = CreateStepsDocument([step])
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

        jobLogPublisher.Publish(
            runId,
            step.StepNo,
            step.StepType,
            "Error",
            _workerId,
            $"Step {step.StepNo} attempt {attempt} failed.",
            attempt,
            failedAt,
            startedAt,
            exception.Message);
    }

    private static JsonDocument CreateStepsDocument(IReadOnlyList<JobStepDefinition> steps)
    {
        return JsonSerializer.SerializeToDocument(steps, ContractJson.Options);
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

    private sealed class StepRetriesExhaustedException(int attempts, Exception innerException)
        : Exception("Step retries exhausted.", innerException)
    {
        public int Attempts { get; } = attempts;
    }
}
