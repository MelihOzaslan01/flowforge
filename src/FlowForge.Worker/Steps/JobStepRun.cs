namespace FlowForge.Worker.Steps;

public sealed class JobStepRun
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public int StepNo { get; set; }
    public required string Status { get; set; }
    public required string WorkerId { get; set; }
    public int AttemptCount { get; set; } = 1;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
    public DateTimeOffset LastHeartbeatAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Error { get; set; }
}
