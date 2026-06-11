using FlowForge.ControlPlane.Data;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.ControlPlane.Dashboard;

public sealed class DashboardQueries(
    IDbContextFactory<ControlPlaneDbContext> controlDbFactory,
    IDbContextFactory<WorkerReadDbContext> workerDbFactory)
{
    public async Task<IReadOnlyList<JobDashboardItem>> GetJobsAsync(CancellationToken ct)
    {
        await using var db = await controlDbFactory.CreateDbContextAsync(ct);
        var jobs = await db.Jobs
            .Include(job => job.Steps)
            .Include(job => job.Runs)
            .AsNoTracking()
            .OrderBy(job => job.Name)
            .ToListAsync(ct);

        return jobs
            .Select(job => new JobDashboardItem(
                job.Id,
                job.Name,
                job.Steps.Count,
                job.Runs
                    .OrderByDescending(run => run.RequestedAt)
                    .Take(5)
                    .Select(run => new RunSummary(
                        run.Id,
                        run.Status,
                        run.RequestedAt,
                        run.FinishedAt,
                        run.FailedStep))
                    .ToList()))
            .ToList();
    }

    public async Task<RunDetailViewModel?> GetRunDetailAsync(Guid runId, CancellationToken ct)
    {
        await using var controlDb = await controlDbFactory.CreateDbContextAsync(ct);
        var run = await controlDb.JobRuns
            .Include(item => item.Job)
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == runId, ct);

        if (run?.Job is null)
        {
            return null;
        }

        await using var workerDb = await workerDbFactory.CreateDbContextAsync(ct);
        var steps = await workerDb.JobStepRuns
            .AsNoTracking()
            .Where(step => step.RunId == runId)
            .OrderBy(step => step.StartedAt)
            .ThenBy(step => step.StepNo)
            .ThenBy(step => step.AttemptCount)
            .Select(step => new StepTimelineItem(
                step.Id,
                step.StepNo,
                step.Status,
                step.AttemptCount,
                step.StartedAt,
                step.FinishedAt,
                step.WorkerId,
                step.Error))
            .ToListAsync(ct);

        return new RunDetailViewModel(
            run.Id,
            run.Job.Name,
            run.Status,
            run.RequestedAt,
            run.FinishedAt,
            run.FailedStep,
            steps);
    }
}

public sealed record JobDashboardItem(
    Guid Id,
    string Name,
    int StepCount,
    IReadOnlyList<RunSummary> LastRuns);

public sealed record RunSummary(
    Guid Id,
    string Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? FinishedAt,
    int? FailedStep);

public sealed record RunDetailViewModel(
    Guid Id,
    string JobName,
    string Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? FinishedAt,
    int? FailedStep,
    IReadOnlyList<StepTimelineItem> Steps);

public sealed record StepTimelineItem(
    Guid Id,
    int StepNo,
    string Status,
    int AttemptCount,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string WorkerId,
    string? Error);
