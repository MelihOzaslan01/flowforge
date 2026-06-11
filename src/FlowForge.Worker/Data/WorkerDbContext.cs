using FlowForge.Outbox;
using FlowForge.Worker.Steps;
using Microsoft.EntityFrameworkCore;

namespace FlowForge.Worker.Data;

public sealed class WorkerDbContext(DbContextOptions<WorkerDbContext> options)
    : DbContext(options), IOutboxDbContext
{
    public DbSet<JobStepRun> JobStepRuns => Set<JobStepRun>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobStepRun>(entity =>
        {
            entity.ToTable("job_step_runs");
            entity.HasKey(run => run.Id);
            entity.Property(run => run.Id).HasColumnName("id");
            entity.Property(run => run.RunId).HasColumnName("run_id");
            entity.Property(run => run.SourceMessageId).HasColumnName("source_message_id");
            entity.Property(run => run.StepNo).HasColumnName("step_no");
            entity.Property(run => run.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
            entity.Property(run => run.WorkerId).HasColumnName("worker_id").HasMaxLength(100).IsRequired();
            entity.Property(run => run.AttemptCount).HasColumnName("attempt_count").HasDefaultValue(1);
            entity.Property(run => run.StartedAt).HasColumnName("started_at").HasDefaultValueSql("now()");
            entity.Property(run => run.FinishedAt).HasColumnName("finished_at");
            entity.Property(run => run.LastHeartbeatAt).HasColumnName("last_heartbeat_at").HasDefaultValueSql("now()");
            entity.Property(run => run.Error).HasColumnName("error");
            entity.Property(run => run.Steps).HasColumnName("steps").HasColumnType("jsonb");
            entity.HasIndex(run => new { run.RunId, run.StepNo, run.AttemptCount }).IsUnique();
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Id).HasColumnName("id");
            entity.Property(message => message.AggregateId).HasColumnName("aggregate_id");
            entity.Property(message => message.Topic).HasColumnName("topic").HasMaxLength(200);
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
