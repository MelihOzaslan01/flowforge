using Polly;
using Polly.Retry;

namespace FlowForge.Worker.Steps;

public sealed record StepRetryAttempt(
    int Attempt,
    TimeSpan RetryDelay,
    Exception Exception);

public sealed record StepRetryResult(int Attempt);

public static class StepRetryPipeline
{
    public static async Task<StepRetryResult> ExecuteAsync(
        int previousAttempts,
        int maxRetries,
        Func<int, CancellationToken, ValueTask> operation,
        Func<StepRetryAttempt, CancellationToken, ValueTask> onFailedAttempt,
        CancellationToken ct)
    {
        var attemptsStarted = 0;

        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = Math.Max(0, maxRetries),
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = false,
                OnRetry = async args =>
                {
                    var attempt = previousAttempts + args.AttemptNumber + 1;
                    await onFailedAttempt(
                        new StepRetryAttempt(attempt, args.RetryDelay, args.Outcome.Exception!),
                        args.Context.CancellationToken);
                }
            })
            .Build();

        try
        {
            await pipeline.ExecuteAsync(async token =>
            {
                attemptsStarted++;
                await operation(previousAttempts + attemptsStarted, token);
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var attempt = previousAttempts + attemptsStarted;
            await onFailedAttempt(new StepRetryAttempt(attempt, TimeSpan.Zero, ex), ct);
            throw;
        }

        return new StepRetryResult(previousAttempts + attemptsStarted);
    }
}
