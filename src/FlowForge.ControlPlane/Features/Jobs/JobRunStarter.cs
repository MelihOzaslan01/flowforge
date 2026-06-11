using FlowForge.Contracts;
using FlowForge.ControlPlane.Data;
using FlowForge.ControlPlane.Features.Runs;
using FlowForge.Outbox;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.ControlPlane.Features.Jobs;

public sealed class JobRunStarter(IDbContextFactory<ControlPlaneDbContext> dbFactory)
{
    public async Task<StartJobRunResult> StartByNameAsync(string name, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var job = await db.Jobs
            .Include(item => item.Steps)
            .SingleOrDefaultAsync(item => item.Name == name, ct);

        return await StartAsync(db, job, name, ct);
    }

    public async Task<StartJobRunResult> StartByIdAsync(Guid jobId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var job = await db.Jobs
            .Include(item => item.Steps)
            .SingleOrDefaultAsync(item => item.Id == jobId, ct);

        return await StartAsync(db, job, jobId.ToString(), ct);
    }

    private static async Task<StartJobRunResult> StartAsync(
        ControlPlaneDbContext db,
        Job? job,
        string label,
        CancellationToken ct)
    {
        if (job is null)
        {
            return StartJobRunResult.NotFound($"Job '{label}' was not found.");
        }

        if (!job.IsEnabled)
        {
            return StartJobRunResult.BadRequest($"Job '{job.Name}' is disabled.");
        }

        if (job.Steps.Count == 0)
        {
            return StartJobRunResult.BadRequest($"Job '{job.Name}' has no steps.");
        }

        var runId = Guid.NewGuid();
        var requestedAt = DateTimeOffset.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        db.JobRuns.Add(new JobRun
        {
            Id = runId,
            JobId = job.Id,
            Status = "Scheduled",
            RequestedAt = requestedAt
        });

        var requested = new JobRunRequested(
            runId,
            job.Id,
            job.Steps
                .OrderBy(step => step.StepNo)
                .Select(step => new JobStepDefinition(
                    step.Id,
                    step.StepNo,
                    step.StepType,
                    step.Config.RootElement.Clone(),
                    step.MaxRetries))
                .ToList());

        db.OutboxMessages.Add(OutboxMessage.From(
            runId,
            EventEnvelope.From(requested, occurredAt: requestedAt)));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return StartJobRunResult.Accepted(runId);
    }
}

public sealed record StartJobRunResult(
    Guid? RunId,
    StartJobRunError? ErrorType,
    string? Error)
{
    public static StartJobRunResult Accepted(Guid runId) => new(runId, null, null);

    public static StartJobRunResult NotFound(string error) => new(null, StartJobRunError.NotFound, error);

    public static StartJobRunResult BadRequest(string error) => new(null, StartJobRunError.BadRequest, error);
}

public enum StartJobRunError
{
    NotFound,
    BadRequest
}
