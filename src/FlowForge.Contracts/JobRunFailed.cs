namespace FlowForge.Contracts;

public sealed record JobRunFailed(
    Guid RunId,
    int FailedStep,
    string Reason);
