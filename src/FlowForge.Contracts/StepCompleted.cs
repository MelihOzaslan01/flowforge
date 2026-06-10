using System.Text.Json;

namespace FlowForge.Contracts;

public sealed record StepCompleted(
    Guid RunId,
    int StepNo,
    JsonElement Output,
    IReadOnlyList<JobStepDefinition> Steps);
