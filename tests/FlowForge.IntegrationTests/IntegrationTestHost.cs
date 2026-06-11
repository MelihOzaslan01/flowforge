using FlowForge.Outbox;
using FlowForge.Worker.Data;
using FlowForge.Worker.Kafka;
using FlowForge.Worker.Steps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FlowForge.IntegrationTests;

public static class IntegrationTestHost
{
    public static IHost CreateWorkerHost(FlowForgeFixture fixture)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kafka:BootstrapServers"] = fixture.BootstrapServers,
                ["Kafka:Topic"] = FlowForge.Contracts.KafkaTopics.JobEvents
            })
            .Build();

        return Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Warning);
            })
            .ConfigureServices(services =>
            {
                services.AddDbContext<WorkerDbContext>(options =>
                    options.UseNpgsql(fixture.WorkerConnectionString));
                services.Configure<KafkaOptions>(configuration.GetSection("Kafka"));
                services.AddScoped<StepExecutor>();
                services.AddSingleton<JobLogPublisher>();
                services.AddHostedService<JobEventsConsumer>();
                services.AddHostedService<ZombieStepCleaner>();
                services.AddOutboxPublisher<WorkerDbContext>(configuration.GetSection("Kafka"));
            })
            .Build();
    }

    public static IHost CreateOutboxPublisherHost(FlowForgeFixture fixture)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kafka:BootstrapServers"] = fixture.BootstrapServers,
                ["Kafka:Topic"] = FlowForge.Contracts.KafkaTopics.JobEvents
            })
            .Build();

        return Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Warning);
            })
            .ConfigureServices(services =>
            {
                services.AddDbContext<WorkerDbContext>(options =>
                    options.UseNpgsql(fixture.WorkerConnectionString));
                services.AddOutboxPublisher<WorkerDbContext>(configuration.GetSection("Kafka"));
            })
            .Build();
    }
}
