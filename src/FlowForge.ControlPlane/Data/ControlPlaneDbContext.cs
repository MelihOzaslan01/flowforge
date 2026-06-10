using FlowForge.ControlPlane.Features.Jobs;
using FlowForge.ControlPlane.Features.Runs;
using FlowForge.ControlPlane.Inbox;
using FlowForge.ControlPlane.Outbox;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.ControlPlane.Data;

public sealed class ControlPlaneDbContext(DbContextOptions<ControlPlaneDbContext> options) : DbContext(options)
{
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobStep> JobSteps => Set<JobStep>();
    public DbSet<JobRun> JobRuns => Set<JobRun>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Job>(entity =>
        {
            entity.ToTable("jobs");
            entity.HasKey(job => job.Id);
            entity.Property(job => job.Id).HasColumnName("id");
            entity.Property(job => job.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(job => job.Cron).HasColumnName("cron").HasMaxLength(50);
            entity.Property(job => job.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(true);
            entity.Property(job => job.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(job => job.Name).IsUnique();
        });

        modelBuilder.Entity<JobStep>(entity =>
        {
            entity.ToTable("job_steps");
            entity.HasKey(step => step.Id);
            entity.Property(step => step.Id).HasColumnName("id");
            entity.Property(step => step.JobId).HasColumnName("job_id");
            entity.Property(step => step.StepNo).HasColumnName("step_no");
            entity.Property(step => step.StepType).HasColumnName("step_type").HasMaxLength(100).IsRequired();
            entity.Property(step => step.Config).HasColumnName("config").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
            entity.Property(step => step.MaxRetries).HasColumnName("max_retries").HasDefaultValue(3);
            entity.HasIndex(step => new { step.JobId, step.StepNo }).IsUnique();
            entity
                .HasOne(step => step.Job)
                .WithMany(job => job.Steps)
                .HasForeignKey(step => step.JobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobRun>(entity =>
        {
            entity.ToTable("job_runs");
            entity.HasKey(run => run.Id);
            entity.Property(run => run.Id).HasColumnName("id");
            entity.Property(run => run.JobId).HasColumnName("job_id");
            entity.Property(run => run.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
            entity.Property(run => run.RequestedAt).HasColumnName("requested_at").HasDefaultValueSql("now()");
            entity.Property(run => run.FinishedAt).HasColumnName("finished_at");
            entity.Property(run => run.FailedStep).HasColumnName("failed_step");
            entity
                .HasOne(run => run.Job)
                .WithMany(job => job.Runs)
                .HasForeignKey(run => run.JobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Id).HasColumnName("id");
            entity.Property(message => message.AggregateId).HasColumnName("aggregate_id");
            entity.Property(message => message.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
            entity.Property(message => message.Payload).HasColumnName("payload").HasColumnType("jsonb");
            entity.Property(message => message.OccurredAt).HasColumnName("occurred_at").HasDefaultValueSql("now()");
            entity.Property(message => message.PublishedAt).HasColumnName("published_at");
            entity.Property(message => message.AttemptCount).HasColumnName("attempt_count").HasDefaultValue(0);
            entity.HasIndex(message => message.OccurredAt)
                .HasDatabaseName("ix_outbox_unpublished")
                .HasFilter("published_at IS NULL");
        });

        modelBuilder.Entity<ProcessedMessage>(entity =>
        {
            entity.ToTable("processed_messages");
            entity.HasKey(message => message.MessageId);
            entity.Property(message => message.MessageId).HasColumnName("message_id");
            entity.Property(message => message.Consumer).HasColumnName("consumer").HasMaxLength(100).IsRequired();
            entity.Property(message => message.ProcessedAt).HasColumnName("processed_at").HasDefaultValueSql("now()");
        });
    }
}
