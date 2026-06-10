using FlowForge.Outbox;
using FlowForge.Worker.Data;
using FlowForge.Worker.Kafka;
using FlowForge.Worker.Steps;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.AddDbContext<WorkerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("WorkerDb")));
builder.Services.AddScoped<StepExecutor>();
builder.Services.AddHostedService<JobEventsConsumer>();
builder.Services.AddOutboxPublisher<WorkerDbContext>(builder.Configuration.GetSection("Kafka"));

var host = builder.Build();

var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
await MigrateAsync(host, lifetime.ApplicationStopping);

host.Run();

static async Task MigrateAsync(IHost host, CancellationToken ct)
{
    var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    while (!ct.IsCancellationRequested)
    {
        try
        {
            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WorkerDbContext>();
            await db.Database.MigrateAsync(ct);
            return;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Worker database is not ready. Retrying in 5 seconds.");
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }
}
