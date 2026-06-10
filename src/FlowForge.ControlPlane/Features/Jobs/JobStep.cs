using System.Text.Json;

namespace FlowForge.ControlPlane.Features.Jobs;

public sealed class JobStep
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public int StepNo { get; set; }
    public required string StepType { get; set; }
    public JsonDocument Config { get; set; } = JsonDocument.Parse("{}");
    public int MaxRetries { get; set; } = 3;
    public Job? Job { get; set; }
}
