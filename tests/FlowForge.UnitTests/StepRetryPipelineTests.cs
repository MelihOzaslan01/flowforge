using FlowForge.Worker.Steps;

namespace FlowForge.UnitTests;

public sealed class StepRetryPipelineTests
{
    [Fact]
    public async Task Retry_pipeline_retries_twice_then_succeeds_with_exponential_backoff()
    {
        var operationAttempts = new List<int>();
        var failedAttempts = new List<int>();
        var retryDelays = new List<TimeSpan>();

        var result = await StepRetryPipeline.ExecuteAsync(
            previousAttempts: 0,
            maxRetries: 3,
            operation: (attempt, _) =>
            {
                operationAttempts.Add(attempt);
                if (attempt < 3)
                {
                    throw new InvalidOperationException($"boom-{attempt}");
                }

                return ValueTask.CompletedTask;
            },
            onFailedAttempt: (attempt, _) =>
            {
                failedAttempts.Add(attempt.Attempt);
                retryDelays.Add(attempt.RetryDelay);
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(3, result.Attempt);
        Assert.Equal([1, 2, 3], operationAttempts);
        Assert.Equal([1, 2], failedAttempts);
        Assert.Equal([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)], retryDelays);
    }
}
