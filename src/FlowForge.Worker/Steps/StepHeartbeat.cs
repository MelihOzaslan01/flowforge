using FlowForge.Worker.Data;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.Worker.Steps;

public sealed class StepHeartbeat : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop;
    private readonly Task _loop;

    private StepHeartbeat(CancellationTokenSource stop, Task loop)
    {
        _stop = stop;
        _loop = loop;
    }

    public static Task<StepHeartbeat> StartAsync(
        IServiceScopeFactory scopes,
        Guid runId,
        int stepNo,
        int attempt,
        TimeSpan interval,
        ILogger logger,
        CancellationToken ct)
    {
        var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var loop = RunAsync(scopes, runId, stepNo, attempt, interval, logger, stop.Token);
        return Task.FromResult(new StepHeartbeat(stop, loop));
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();

        try
        {
            await _loop;
        }
        catch (OperationCanceledException)
        {
        }

        _stop.Dispose();
    }

    private static async Task RunAsync(
        IServiceScopeFactory scopes,
        Guid runId,
        int stepNo,
        int attempt,
        TimeSpan interval,
        ILogger logger,
        CancellationToken ct)
    {
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<WorkerDbContext>();
                var stepRun = await db.JobStepRuns.SingleOrDefaultAsync(
                    run => run.RunId == runId && run.StepNo == stepNo && run.AttemptCount == attempt,
                    ct);

                if (stepRun is null || stepRun.Status != "Running")
                {
                    return;
                }

                stepRun.LastHeartbeatAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Heartbeat update failed for run {RunId}, step {StepNo}, attempt {Attempt}.",
                    runId,
                    stepNo,
                    attempt);
            }
        }
    }
}
