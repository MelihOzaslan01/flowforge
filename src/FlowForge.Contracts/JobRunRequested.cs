namespace FlowForge.Contracts;

public sealed record JobRunRequested(
    Guid RunId,
    Guid JobId,
    IReadOnlyList<JobStepDefinition> Steps);
