using System.Text.Json;

namespace FlowForge.ControlPlane.Features.Jobs;

public sealed record CreateJobRequest(
    string Name,
    string? Cron,
    IReadOnlyList<CreateJobStepRequest> Steps);

public sealed record CreateJobStepRequest(
    int StepNo,
    string StepType,
    JsonElement? Config,
    int? MaxRetries);

public sealed record JobResponse(
    Guid Id,
    string Name,
    string? Cron,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    IReadOnlyList<JobStepResponse> Steps);

public sealed record JobStepResponse(
    Guid Id,
    int StepNo,
    string StepType,
    JsonElement Config,
    int MaxRetries);
