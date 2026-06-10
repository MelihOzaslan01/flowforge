using FlowForge.Contracts;

namespace FlowForge.Worker.Steps;

public sealed class StepExecutor(ILogger<StepExecutor> logger)
{
    public async Task RunAsync(JobStepDefinition step, CancellationToken ct)
    {
        var delay = step.StepType switch
        {
            "ExtractData" => TimeSpan.FromSeconds(5),
            "TransformData" => TimeSpan.FromSeconds(8),
            "GenerateReport" => TimeSpan.FromSeconds(4),
            "NotifyUsers" => TimeSpan.FromSeconds(1),
            _ => throw new InvalidOperationException($"Unknown step_type '{step.StepType}'.")
        };

        logger.LogInformation("Running step {StepNo} of type {StepType}.", step.StepNo, step.StepType);
        await Task.Delay(delay, ct);
    }
}
