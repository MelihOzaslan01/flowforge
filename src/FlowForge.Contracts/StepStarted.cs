namespace FlowForge.Contracts;

public sealed record StepStarted(
    Guid RunId,
    int StepNo,
    string WorkerId);
