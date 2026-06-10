using FlowForge.ControlPlane.Data;
using FlowForge.ControlPlane.Features.Jobs;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddDbContext<ControlPlaneDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ControlDb")));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
    await db.Database.MigrateAsync();
    await ControlPlaneSeeder.SeedAsync(db, CancellationToken.None);
}

app.MapJobEndpoints();
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/readyz");

app.Run();
