using Confluent.Kafka;
using Confluent.Kafka.Admin;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FlowForge.Contracts;
using FlowForge.Worker.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FlowForge.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class FlowForgeCollection : ICollectionFixture<FlowForgeFixture>
{
    public const string Name = "flowforge-integration";
}

public sealed class FlowForgeFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;
    private readonly IContainer _kafka;

    public FlowForgeFixture()
    {
        if (OperatingSystem.IsWindows() && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST")))
        {
            Environment.SetEnvironmentVariable("DOCKER_HOST", "npipe://./pipe/dockerDesktopLinuxEngine");
        }

        _postgres = new PostgreSqlBuilder("postgres:17")
            .WithDatabase("worker_db")
            .WithUsername("postgres")
            .WithPassword("flowforge")
            .Build();

        _kafka = new ContainerBuilder("apache/kafka:3.9.0")
            .WithPortBinding(19093, 9092)
            .WithEnvironment("KAFKA_NODE_ID", "1")
            .WithEnvironment("KAFKA_PROCESS_ROLES", "broker,controller")
            .WithEnvironment("KAFKA_CONTROLLER_QUORUM_VOTERS", "1@localhost:9093")
            .WithEnvironment("KAFKA_LISTENERS", "PLAINTEXT://:9092,CONTROLLER://:9093")
            .WithEnvironment("KAFKA_ADVERTISED_LISTENERS", "PLAINTEXT://localhost:19093")
            .WithEnvironment("KAFKA_CONTROLLER_LISTENER_NAMES", "CONTROLLER")
            .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", "1")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged(".*Kafka Server started.*"))
            .Build();
    }

    public string WorkerConnectionString => _postgres.GetConnectionString();

    public string BootstrapServers => "localhost:19093";

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _kafka.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _kafka.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public async Task ResetAsync()
    {
        await using (var db = CreateDbContext())
        {
            await db.Database.MigrateAsync();
            await db.Database.ExecuteSqlRawAsync(
                "truncate table job_step_runs, outbox_messages, processed_messages restart identity cascade;");
        }

        await ResetTopicsAsync();
    }

    public WorkerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WorkerDbContext>()
            .UseNpgsql(WorkerConnectionString)
            .Options;

        return new WorkerDbContext(options);
    }

    public IProducer<string, string> CreateProducer()
    {
        return new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        }).Build();
    }

    public IConsumer<string, string> CreateConsumer(string groupId)
    {
        return new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = BootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        }).Build();
    }

    public async Task StopKafkaAsync()
    {
        await _kafka.StopAsync();
    }

    public async Task StartKafkaAsync()
    {
        await _kafka.StartAsync();
    }

    private async Task ResetTopicsAsync()
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = BootstrapServers
        }).Build();

        try
        {
            await admin.DeleteTopicsAsync([KafkaTopics.JobEvents, KafkaTopics.JobEventsDlq, KafkaTopics.JobLogs]);
        }
        catch (DeleteTopicsException ex) when (ex.Results.All(result =>
            result.Error.Code == ErrorCode.UnknownTopicOrPart))
        {
        }

        var topics = new[]
        {
            new TopicSpecification { Name = KafkaTopics.JobEvents, NumPartitions = 6, ReplicationFactor = 1 },
            new TopicSpecification { Name = KafkaTopics.JobEventsDlq, NumPartitions = 3, ReplicationFactor = 1 },
            new TopicSpecification { Name = KafkaTopics.JobLogs, NumPartitions = 6, ReplicationFactor = 1 }
        };

        for (var attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                await admin.CreateTopicsAsync(topics);
                return;
            }
            catch (CreateTopicsException ex) when (ex.Results.All(result =>
                result.Error.Code == ErrorCode.TopicAlreadyExists))
            {
                return;
            }
            catch (CreateTopicsException ex) when (ex.Results.Any(result =>
                result.Error.Code == ErrorCode.TopicAlreadyExists
                || result.Error.Code == ErrorCode.Local_TimedOut))
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        await admin.CreateTopicsAsync(topics);
    }
}
