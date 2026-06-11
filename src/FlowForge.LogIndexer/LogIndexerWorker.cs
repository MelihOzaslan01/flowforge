using System.Text.Json;
using Confluent.Kafka;
using FlowForge.Contracts;
using Microsoft.Extensions.Options;

namespace FlowForge.LogIndexer;

public sealed class LogIndexerWorker(
    IOptions<KafkaOptions> kafkaOptions,
    ElasticsearchLogClient elasticsearch,
    ILogger<LogIndexerWorker> logger)
    : BackgroundService
{
    private const int MaxBatchSize = 500;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        await elasticsearch.PutIndexTemplateWithRetryAsync(stoppingToken);

        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaOptions.Value.BootstrapServers,
            GroupId = "log-indexer",
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            SessionTimeoutMs = 10000,
            MaxPollIntervalMs = 300000,
            PartitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky
        };

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetPartitionsAssignedHandler((_, partitions) =>
            {
                logger.LogInformation(
                    "LogIndexer assigned Kafka partitions: {Partitions}",
                    FormatPartitions(partitions));
            })
            .SetPartitionsRevokedHandler((_, partitions) =>
            {
                logger.LogInformation(
                    "LogIndexer revoked Kafka partitions: {Partitions}",
                    FormatPartitions(partitions));
            })
            .Build();

        consumer.Subscribe(KafkaTopics.JobLogs);

        var batch = new List<BufferedLogMessage>(MaxBatchSize);
        var nextFlushAt = DateTimeOffset.UtcNow.Add(FlushInterval);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (batch.Count < MaxBatchSize)
                {
                    var result = consumer.Consume(TimeSpan.FromMilliseconds(250));
                    if (result is not null)
                    {
                        if (TryCreateBufferedMessage(result, out var message))
                        {
                            batch.Add(message);
                        }
                        else
                        {
                            logger.LogWarning(
                                "Invalid job log message at {TopicPartitionOffset}; committing offset and skipping.",
                                result.TopicPartitionOffset);
                            consumer.Commit(result);
                        }
                    }
                }

                if (batch.Count == 0)
                {
                    nextFlushAt = DateTimeOffset.UtcNow.Add(FlushInterval);
                    continue;
                }

                if (batch.Count >= MaxBatchSize || DateTimeOffset.UtcNow >= nextFlushAt)
                {
                    if (await TryFlushAsync(consumer, batch, stoppingToken))
                    {
                        batch.Clear();
                    }

                    nextFlushAt = DateTimeOffset.UtcNow.Add(FlushInterval);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("LogIndexer is stopping.");
        }
        finally
        {
            if (batch.Count > 0 && !stoppingToken.IsCancellationRequested)
            {
                await FlushAsync(consumer, batch, CancellationToken.None);
            }

            consumer.Close();
        }
    }

    private async Task FlushAsync(
        IConsumer<string, string> consumer,
        IReadOnlyList<BufferedLogMessage> batch,
        CancellationToken ct)
    {
        await elasticsearch.BulkIndexAsync(batch, ct);

        var offsets = batch
            .GroupBy(message => message.Result.TopicPartition)
            .Select(group =>
            {
                var last = group.MaxBy(message => message.Result.Offset.Value)!.Result;
                return new TopicPartitionOffset(last.TopicPartition, last.Offset + 1);
            })
            .ToArray();

        consumer.Commit(offsets);
        logger.LogInformation("Indexed {Count} log messages and committed {OffsetCount} partition offsets.", batch.Count, offsets.Length);
    }

    private async Task<bool> TryFlushAsync(
        IConsumer<string, string> consumer,
        IReadOnlyList<BufferedLogMessage> batch,
        CancellationToken ct)
    {
        try
        {
            await FlushAsync(consumer, batch, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Log indexing bulk request failed; offsets were not committed and the batch will be retried.");
            return false;
        }
    }

    private static bool TryCreateBufferedMessage(
        ConsumeResult<string, string> result,
        out BufferedLogMessage message)
    {
        message = default!;

        try
        {
            using var document = JsonDocument.Parse(result.Message.Value);
            var root = document.RootElement;
            var timestamp = root.GetProperty("timestamp").GetDateTimeOffset();
            var runId = root.GetProperty("runId").GetGuid();
            var stepNo = root.GetProperty("stepNo").GetInt32();
            var attempt = root.GetProperty("attempt").GetInt32();
            var id = LogDocumentId.Create(runId, stepNo, "WorkerStepLog", attempt, timestamp);
            var indexName = $"flowforge-logs-{timestamp:yyyy.MM}";

            message = new BufferedLogMessage(result, indexName, id, result.Message.Value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string FormatPartitions(IEnumerable<TopicPartition> partitions)
    {
        return string.Join(", ", partitions.Select(partition =>
            $"{partition.Topic}[{partition.Partition.Value}]"));
    }

    private static string FormatPartitions(IEnumerable<TopicPartitionOffset> partitions)
    {
        return string.Join(", ", partitions.Select(partition =>
            $"{partition.Topic}[{partition.Partition.Value}]@{partition.Offset.Value}"));
    }
}
