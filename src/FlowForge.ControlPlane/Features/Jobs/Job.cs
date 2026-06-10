using FlowForge.ControlPlane.Features.Runs;

namespace FlowForge.ControlPlane.Features.Jobs;

public sealed class Job
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Cron { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<JobStep> Steps { get; set; } = [];
    public List<JobRun> Runs { get; set; } = [];
}
