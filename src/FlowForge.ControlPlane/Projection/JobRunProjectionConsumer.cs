using System.Text.Json;
using Confluent.Kafka;
using FlowForge.Contracts;
using FlowForge.ControlPlane.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FlowForge.ControlPlane.Projection;

public sealed class JobRunProjectionConsumer(
    IServiceScopeFactory scopes,
    IOptions<ProjectionKafkaOptions> options,
    ILogger<JobRunProjectionConsumer> logger)
    : BackgroundService
{
    private static readonly string[] TerminalStatuses = ["Completed", "Failed", "Compensated"];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            GroupId = "controlplane-projection",
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            SessionTimeoutMs = 10000,
            MaxPollIntervalMs = 300000,
            PartitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(KafkaTopics.JobEvents);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    await ProjectAsync(result, stoppingToken);
                    consumer.Commit(result);
                }
                catch (ConsumeException ex)
                {
                    logger.LogError(ex, "ControlPlane projection consume failed.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "ControlPlane projection message processing failed.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("ControlPlane projection consumer is stopping.");
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task ProjectAsync(ConsumeResult<string, string> result, CancellationToken ct)
    {
        EventEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<EventEnvelope>(result.Message.Value, ContractJson.Options)
                ?? throw new JsonException("Event envelope payload is empty.");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Invalid projection event envelope at {TopicPartitionOffset}.", result.TopicPartitionOffset);
            return;
        }

        if (envelope.EventType == nameof(JobRunCompleted))
        {
            var completed = envelope.DeserializePayload<JobRunCompleted>();
            await MarkCompletedAsync(completed.RunId, envelope.OccurredAt, ct);
            return;
        }

        if (envelope.EventType == nameof(JobRunFailed))
        {
            var failed = envelope.DeserializePayload<JobRunFailed>();
            await MarkFailedAsync(failed.RunId, failed.FailedStep, envelope.OccurredAt, ct);
        }
    }

    private async Task MarkCompletedAsync(Guid runId, DateTimeOffset finishedAt, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
        var run = await db.JobRuns.SingleOrDefaultAsync(item => item.Id == runId, ct);
        if (run is null || TerminalStatuses.Contains(run.Status))
        {
            return;
        }

        run.Status = "Completed";
        run.FinishedAt = finishedAt;
        await db.SaveChangesAsync(ct);
    }

    private async Task MarkFailedAsync(Guid runId, int failedStep, DateTimeOffset finishedAt, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
        var run = await db.JobRuns.SingleOrDefaultAsync(item => item.Id == runId, ct);
        if (run is null || TerminalStatuses.Contains(run.Status))
        {
            return;
        }

        run.Status = "Failed";
        run.FailedStep = failedStep;
        run.FinishedAt = finishedAt;
        await db.SaveChangesAsync(ct);
    }
}
