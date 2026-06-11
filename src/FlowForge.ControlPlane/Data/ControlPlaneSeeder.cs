using System.Text.Json;
using FlowForge.Contracts;
using FlowForge.ControlPlane.Features.Jobs;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.ControlPlane.Data;

public static class ControlPlaneSeeder
{
    public static async Task SeedAsync(ControlPlaneDbContext db, CancellationToken ct)
    {
        await SeedMonthlySalesReportAsync(db, ct);
        await SeedMonthlySalesReportChaosAsync(db, ct);
    }

    private static async Task SeedMonthlySalesReportAsync(ControlPlaneDbContext db, CancellationToken ct)
    {
        if (await db.Jobs.AnyAsync(job => job.Name == "monthly-sales-report", ct))
        {
            return;
        }

        var job = new Job
        {
            Id = Guid.NewGuid(),
            Name = "monthly-sales-report",
            Cron = null,
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            Steps =
            [
                CreateStep(1, "ExtractData"),
                CreateStep(2, "TransformData"),
                CreateStep(3, "GenerateReport"),
                CreateStep(4, "NotifyUsers")
            ]
        };

        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedMonthlySalesReportChaosAsync(ControlPlaneDbContext db, CancellationToken ct)
    {
        if (await db.Jobs.AnyAsync(job => job.Name == "monthly-sales-report-chaos", ct))
        {
            return;
        }

        var job = new Job
        {
            Id = Guid.NewGuid(),
            Name = "monthly-sales-report-chaos",
            Cron = null,
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            Steps =
            [
                CreateStep(1, "ExtractData", durationMs: 2500),
                CreateStep(2, "TransformData", durationMs: 4000),
                CreateStep(3, "GenerateReport", chaosFailRate: 0.3, durationMs: 2000, maxRetries: 0),
                CreateStep(4, "NotifyUsers", durationMs: 500)
            ]
        };

        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);
    }

    private static JobStep CreateStep(
        int stepNo,
        string stepType,
        double chaosFailRate = 0,
        int? durationMs = null,
        int maxRetries = 3)
    {
        return new JobStep
        {
            Id = Guid.NewGuid(),
            StepNo = stepNo,
            StepType = stepType,
            Config = CreateConfig(chaosFailRate, durationMs),
            MaxRetries = maxRetries
        };
    }

    private static JsonDocument CreateConfig(double chaosFailRate, int? durationMs)
    {
        var config = new Dictionary<string, object>
        {
            ["chaos_fail_rate"] = chaosFailRate
        };

        if (durationMs is not null)
        {
            config["duration_ms"] = durationMs.Value;
        }

        return JsonSerializer.SerializeToDocument(config, ContractJson.Options);
    }
}
