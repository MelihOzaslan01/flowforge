namespace FlowForge.ControlPlane.Features.Runs;

public sealed record RunCreatedResponse(Guid RunId);

public sealed record RunResponse(
    Guid Id,
    Guid JobId,
    string JobName,
    string Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? FinishedAt,
    int? FailedStep);
