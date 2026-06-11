namespace FlowForge.Contracts;

public sealed record CompensateStep(
    Guid RunId,
    int StepNo,
    int FailedStep,
    IReadOnlyList<JobStepDefinition> Steps);
