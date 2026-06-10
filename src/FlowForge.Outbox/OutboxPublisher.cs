using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowForge.Outbox;

public sealed class OutboxPublisher<TDbContext>(
    IServiceScopeFactory scopes,
    IProducer<string, string> producer,
    IOptions<OutboxPublisherOptions> options,
    ILogger<OutboxPublisher<TDbContext>> logger)
    : BackgroundService
    where TDbContext : DbContext, IOutboxDbContext
{
    private const int BatchSize = 100;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PublishBatchAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Outbox publisher is stopping.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Outbox publisher stopped unexpectedly.");
        }
    }

    private async Task PublishBatchAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
            var batch = await db.OutboxMessages
                .Where(message => message.PublishedAt == null)
                .OrderBy(message => message.OccurredAt)
                .Take(BatchSize)
                .ToListAsync(ct);

            foreach (var message in batch)
            {
                var published = await PublishMessageAsync(message, ct);
                if (!published)
                {
                    break;
                }
            }

            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Outbox publisher batch failed.");
        }
    }

    private async Task<bool> PublishMessageAsync(OutboxMessage message, CancellationToken ct)
    {
        try
        {
            await producer.ProduceAsync(
                options.Value.Topic,
                new Message<string, string>
                {
                    Key = message.AggregateId.ToString(),
                    Value = message.Payload.RootElement.GetRawText()
                },
                ct);

            message.PublishedAt = DateTimeOffset.UtcNow;
            return true;
        }
        catch (ProduceException<string, string> ex)
        {
            message.AttemptCount++;
            logger.LogError(
                ex,
                "Publishing outbox message {MessageId} failed for event {EventType}. Attempt {AttemptCount}.",
                message.Id,
                message.EventType,
                message.AttemptCount);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            message.AttemptCount++;
            logger.LogError(
                ex,
                "Publishing outbox message {MessageId} failed for event {EventType}. Attempt {AttemptCount}.",
                message.Id,
                message.EventType,
                message.AttemptCount);
            return false;
        }
    }
}
