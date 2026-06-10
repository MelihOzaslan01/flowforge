using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FlowForge.Worker.Data;

public sealed class WorkerDbContextFactory : IDesignTimeDbContextFactory<WorkerDbContext>
{
    public WorkerDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("WorkerDb")
            ?? throw new InvalidOperationException("Connection string 'WorkerDb' is not configured.");

        var options = new DbContextOptionsBuilder<WorkerDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new WorkerDbContext(options);
    }
}
