using FlowForge.ControlPlane.Data;
using FlowForge.ControlPlane.Dashboard;
using FlowForge.ControlPlane.Features.Jobs;
using FlowForge.ControlPlane.Components;
using FlowForge.ControlPlane.Projection;
using FlowForge.Outbox;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddMudServices();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.Configure<ProjectionKafkaOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.AddDbContext<ControlPlaneDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ControlDb")));
builder.Services.AddDbContextFactory<ControlPlaneDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ControlDb")));
builder.Services.AddDbContextFactory<WorkerReadDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("WorkerDb")));
builder.Services.AddScoped<JobRunStarter>();
builder.Services.AddScoped<DashboardQueries>();
builder.Services.AddOutboxPublisher<ControlPlaneDbContext>(builder.Configuration.GetSection("Kafka"));
builder.Services.AddHostedService<JobRunProjectionConsumer>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseStaticFiles();
app.UseAntiforgery();

await MigrateAndSeedAsync(app, app.Lifetime.ApplicationStopping);

app.MapJobEndpoints();
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/readyz");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static async Task MigrateAndSeedAsync(WebApplication app, CancellationToken ct)
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    while (!ct.IsCancellationRequested)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
            await db.Database.MigrateAsync(ct);
            await ControlPlaneSeeder.SeedAsync(db, ct);
            return;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "ControlPlane database is not ready. Retrying in 5 seconds.");
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }
}
