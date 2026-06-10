using FlowForge.ControlPlane.Features.Jobs;

namespace FlowForge.ControlPlane.Features.Runs;

public sealed class JobRun
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
    public int? FailedStep { get; set; }
    public Job? Job { get; set; }
}
