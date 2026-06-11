namespace FlowForge.ControlPlane.Data;

public sealed class WorkerStepRunReadModel
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public int StepNo { get; set; }
    public required string Status { get; set; }
    public required string WorkerId { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? Error { get; set; }
}
