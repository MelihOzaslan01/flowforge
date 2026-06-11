using FlowForge.Contracts;

namespace FlowForge.Worker.Steps;

public sealed class StepExecutor(ILogger<StepExecutor> logger)
{
    public async Task RunAsync(JobStepDefinition step, CancellationToken ct)
    {
        var delay = GetDuration(step);
        var chaosFailRate = GetChaosFailRate(step);

        logger.LogInformation("Running step {StepNo} of type {StepType}.", step.StepNo, step.StepType);
        await Task.Delay(delay, ct);

        if (chaosFailRate > 0 && Random.Shared.NextDouble() < chaosFailRate)
        {
            throw new ChaosException(step.StepNo, step.StepType, chaosFailRate);
        }
    }

    public async Task CompensateAsync(JobStepDefinition step, CancellationToken ct)
    {
        var delay = step.StepType switch
        {
            "ExtractData" => TimeSpan.FromSeconds(2),
            "TransformData" => TimeSpan.FromSeconds(2),
            "GenerateReport" => TimeSpan.FromSeconds(1),
            "NotifyUsers" => TimeSpan.FromSeconds(1),
            _ => throw new InvalidOperationException($"Unknown step_type '{step.StepType}'.")
        };

        logger.LogInformation("Compensating step {StepNo} of type {StepType}.", step.StepNo, step.StepType);
        await Task.Delay(delay, ct);
    }

    private static TimeSpan GetDuration(JobStepDefinition step)
    {
        if (step.Config.TryGetProperty("duration_ms", out var durationElement)
            && durationElement.TryGetInt32(out var durationMs)
            && durationMs >= 0)
        {
            return TimeSpan.FromMilliseconds(durationMs);
        }

        return step.StepType switch
        {
            "ExtractData" => TimeSpan.FromSeconds(5),
            "TransformData" => TimeSpan.FromSeconds(8),
            "GenerateReport" => TimeSpan.FromSeconds(4),
            "NotifyUsers" => TimeSpan.FromSeconds(1),
            _ => throw new InvalidOperationException($"Unknown step_type '{step.StepType}'.")
        };
    }

    private static double GetChaosFailRate(JobStepDefinition step)
    {
        if (!step.Config.TryGetProperty("chaos_fail_rate", out var rateElement))
        {
            return 0;
        }

        var rate = rateElement.GetDouble();
        return Math.Clamp(rate, 0, 1);
    }
}

public sealed class ChaosException(int stepNo, string stepType, double failRate)
    : Exception($"Chaos failure for step {stepNo} ({stepType}) at rate {failRate:0.###}.")
{
    public int StepNo { get; } = stepNo;
    public string StepType { get; } = stepType;
    public double FailRate { get; } = failRate;
}
