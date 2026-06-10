using System.Text.Json;
using FlowForge.Contracts;
using FlowForge.ControlPlane.Data;
using FlowForge.ControlPlane.Features.Runs;
using FlowForge.Outbox;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.ControlPlane.Features.Jobs;

public static class JobEndpoints
{
    public static RouteGroupBuilder MapJobEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").WithTags("Jobs");

        group.MapPost("/jobs", CreateJobAsync)
            .WithName("CreateJob");

        group.MapGet("/jobs", GetJobsAsync)
            .WithName("GetJobs");

        group.MapPost("/jobs/{name}/run", RunJobAsync)
            .WithName("RunJob");

        group.MapGet("/runs/{runId:guid}", GetRunAsync)
            .WithName("GetRun");

        return group;
    }

    private static async Task<Results<Created<JobResponse>, BadRequest<string>, Conflict<string>>> CreateJobAsync(
        CreateJobRequest request,
        ControlPlaneDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return TypedResults.BadRequest("Job name is required.");
        }

        if (request.Steps.Count == 0)
        {
            return TypedResults.BadRequest("At least one job step is required.");
        }

        if (request.Steps.Select(step => step.StepNo).Distinct().Count() != request.Steps.Count)
        {
            return TypedResults.BadRequest("Step numbers must be unique.");
        }

        var exists = await db.Jobs.AnyAsync(job => job.Name == request.Name, ct);
        if (exists)
        {
            return TypedResults.Conflict($"Job '{request.Name}' already exists.");
        }

        var job = new Job
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Cron = request.Cron,
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            Steps = request.Steps
                .OrderBy(step => step.StepNo)
                .Select(step => new JobStep
                {
                    Id = Guid.NewGuid(),
                    StepNo = step.StepNo,
                    StepType = step.StepType,
                    Config = CreateConfigDocument(step.Config),
                    MaxRetries = step.MaxRetries ?? 3
                })
                .ToList()
        };

        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);

        return TypedResults.Created($"/api/jobs/{job.Name}", ToJobResponse(job));
    }

    private static async Task<Ok<IReadOnlyList<JobResponse>>> GetJobsAsync(
        ControlPlaneDbContext db,
        CancellationToken ct)
    {
        var jobs = await db.Jobs
            .Include(job => job.Steps)
            .OrderBy(job => job.Name)
            .AsNoTracking()
            .ToListAsync(ct);

        return TypedResults.Ok<IReadOnlyList<JobResponse>>(jobs.Select(ToJobResponse).ToList());
    }

    private static async Task<Results<Accepted<RunCreatedResponse>, NotFound<string>, BadRequest<string>>> RunJobAsync(
        string name,
        ControlPlaneDbContext db,
        CancellationToken ct)
    {
        var job = await db.Jobs
            .Include(item => item.Steps)
            .SingleOrDefaultAsync(item => item.Name == name, ct);

        if (job is null)
        {
            return TypedResults.NotFound($"Job '{name}' was not found.");
        }

        if (!job.IsEnabled)
        {
            return TypedResults.BadRequest($"Job '{name}' is disabled.");
        }

        if (job.Steps.Count == 0)
        {
            return TypedResults.BadRequest($"Job '{name}' has no steps.");
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

        var envelope = EventEnvelope.From(requested, occurredAt: requestedAt);
        db.OutboxMessages.Add(OutboxMessage.From(runId, envelope));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return TypedResults.Accepted($"/api/runs/{runId}", new RunCreatedResponse(runId));
    }

    private static async Task<Results<Ok<RunResponse>, NotFound<string>>> GetRunAsync(
        Guid runId,
        ControlPlaneDbContext db,
        CancellationToken ct)
    {
        var run = await db.JobRuns
            .Include(item => item.Job)
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == runId, ct);

        if (run is null || run.Job is null)
        {
            return TypedResults.NotFound($"Run '{runId}' was not found.");
        }

        return TypedResults.Ok(new RunResponse(
            run.Id,
            run.JobId,
            run.Job.Name,
            run.Status,
            run.RequestedAt,
            run.FinishedAt,
            run.FailedStep));
    }

    private static JobResponse ToJobResponse(Job job)
    {
        return new JobResponse(
            job.Id,
            job.Name,
            job.Cron,
            job.IsEnabled,
            job.CreatedAt,
            job.Steps
                .OrderBy(step => step.StepNo)
                .Select(step => new JobStepResponse(
                    step.Id,
                    step.StepNo,
                    step.StepType,
                    step.Config.RootElement.Clone(),
                    step.MaxRetries))
                .ToList());
    }

    private static JsonDocument CreateConfigDocument(JsonElement? config)
    {
        return config is { } value
            ? JsonDocument.Parse(value.GetRawText())
            : JsonDocument.Parse("{}");
    }
}
