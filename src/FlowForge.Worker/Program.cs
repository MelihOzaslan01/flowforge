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

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WorkerDbContext>();
    await db.Database.MigrateAsync();
}

host.Run();
