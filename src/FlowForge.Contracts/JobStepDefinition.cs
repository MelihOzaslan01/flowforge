using System.Text.Json;

namespace FlowForge.Contracts;

public sealed record JobStepDefinition(
    Guid StepId,
    int StepNo,
    string StepType,
    JsonElement Config,
    int MaxRetries);
