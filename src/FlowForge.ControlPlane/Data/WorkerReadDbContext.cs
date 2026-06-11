using Microsoft.EntityFrameworkCore;

namespace FlowForge.ControlPlane.Data;

public sealed class WorkerReadDbContext(DbContextOptions<WorkerReadDbContext> options)
    : DbContext(options)
{
    public DbSet<WorkerStepRunReadModel> JobStepRuns => Set<WorkerStepRunReadModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkerStepRunReadModel>(entity =>
        {
            entity.ToTable("job_step_runs");
            entity.HasKey(run => run.Id);
            entity.Property(run => run.Id).HasColumnName("id");
            entity.Property(run => run.RunId).HasColumnName("run_id");
            entity.Property(run => run.StepNo).HasColumnName("step_no");
            entity.Property(run => run.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
            entity.Property(run => run.WorkerId).HasColumnName("worker_id").HasMaxLength(100).IsRequired();
            entity.Property(run => run.AttemptCount).HasColumnName("attempt_count");
            entity.Property(run => run.StartedAt).HasColumnName("started_at");
            entity.Property(run => run.FinishedAt).HasColumnName("finished_at");
            entity.Property(run => run.Error).HasColumnName("error");
        });
    }
}
