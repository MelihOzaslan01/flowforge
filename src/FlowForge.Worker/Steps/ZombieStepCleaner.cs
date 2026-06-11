using System.Text.Json;
using FlowForge.Contracts;
using FlowForge.Outbox;
using FlowForge.Worker.Data;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.Worker.Steps;

public sealed class ZombieStepCleaner(
    IServiceScopeFactory scopes,
    ILogger<ZombieStepCleaner> logger)
    : BackgroundService
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        try
        {
            await CleanOnceAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Zombie step cleaner failed.");
        }
    }

    private async Task CleanOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkerDbContext>();
        var cutoff = DateTimeOffset.UtcNow - StaleAfter;
        var zombies = await db.JobStepRuns
            .AsNoTracking()
            .Where(run => run.Status == "Running" && run.LastHeartbeatAt < cutoff)
            .OrderBy(run => run.LastHeartbeatAt)
            .ToListAsync(ct);

        foreach (var zombie in zombies)
        {
            await MarkZombieFailedAsync(zombie, ct);
        }
    }

    private async Task MarkZombieFailedAsync(JobStepRun zombie, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkerDbContext>();
        var failedAt = DateTimeOffset.UtcNow;
        var error = $"Zombie step detected; heartbeat stale since {zombie.LastHeartbeatAt:O}.";

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var updated = await db.JobStepRuns
            .Where(run => run.Id == zombie.Id && run.Status == "Running")
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
            return;
        }

        var steps = DeserializeSteps(zombie.Steps);
        var failed = new StepFailed(
            zombie.RunId,
            zombie.StepNo,
            error,
            zombie.AttemptCount,
            steps);
        var envelope = EventEnvelope.From(failed, occurredAt: failedAt);
        db.OutboxMessages.Add(OutboxMessage.From(zombie.RunId, envelope));

        if (zombie.SourceMessageId is { } sourceMessageId)
        {
            var alreadyProcessed = await db.ProcessedMessages
                .AnyAsync(message => message.MessageId == sourceMessageId, ct);
            if (!alreadyProcessed)
            {
                db.ProcessedMessages.Add(new ProcessedMessage
                {
                    MessageId = sourceMessageId,
                    Consumer = "worker",
                    ProcessedAt = failedAt
                });
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        logger.LogWarning(
            "Zombie step marked failed for run {RunId}, step {StepNo}, attempt {Attempt}.",
            zombie.RunId,
            zombie.StepNo,
            zombie.AttemptCount);
    }

    private static IReadOnlyList<JobStepDefinition> DeserializeSteps(JsonDocument? steps)
    {
        return steps?.RootElement.Deserialize<IReadOnlyList<JobStepDefinition>>(ContractJson.Options)
            ?? [];
    }
}
