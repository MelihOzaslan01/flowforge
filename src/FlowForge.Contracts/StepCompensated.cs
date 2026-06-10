namespace FlowForge.Contracts;

public sealed record StepCompensated(
    Guid RunId,
    int StepNo);
