namespace FlowForge.Contracts;

public sealed record StepFailed(
    Guid RunId,
    int StepNo,
    string Error,
    int Attempts,
    IReadOnlyList<JobStepDefinition> Steps);
