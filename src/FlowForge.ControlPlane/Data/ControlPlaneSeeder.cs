using System.Text.Json;
using FlowForge.Contracts;
using FlowForge.ControlPlane.Features.Jobs;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.ControlPlane.Data;

public static class ControlPlaneSeeder
{
    public static async Task SeedAsync(ControlPlaneDbContext db, CancellationToken ct)
    {
        var exists = await db.Jobs.AnyAsync(job => job.Name == "monthly-sales-report", ct);
        if (exists)
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

    private static JobStep CreateStep(int stepNo, string stepType)
    {
        return new JobStep
        {
            Id = Guid.NewGuid(),
            StepNo = stepNo,
            StepType = stepType,
            Config = JsonSerializer.SerializeToDocument(new { chaos_fail_rate = 0 }, ContractJson.Options),
            MaxRetries = 3
        };
    }
}
