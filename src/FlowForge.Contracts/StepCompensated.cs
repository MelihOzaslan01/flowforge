namespace FlowForge.Contracts;

public sealed record StepCompensated(
    Guid RunId,
    int StepNo,
    int FailedStep,
    IReadOnlyList<JobStepDefinition> Steps);
