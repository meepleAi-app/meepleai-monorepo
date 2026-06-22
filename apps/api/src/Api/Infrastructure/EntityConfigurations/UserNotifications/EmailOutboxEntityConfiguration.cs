using Api.BoundedContexts.UserNotifications.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.UserNotifications;

/// <summary>
/// EF Core configuration for EmailOutboxEntity (auth security hotfix 2026-05-06, I5 prep).
/// Maps to <c>email_outbox</c> table; idempotency key is unique to make caller-supplied retries safe.
/// </summary>
internal class EmailOutboxEntityConfiguration : IEntityTypeConfiguration<EmailOutboxEntity>
{
    public void Configure(EntityTypeBuilder<EmailOutboxEntity> builder)
    {
        builder.ToTable("email_outbox");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.ToEmail).IsRequired().HasMaxLength(320);
        builder.Property(e => e.Subject).IsRequired().HasMaxLength(500);
        builder.Property(e => e.BodyHtml).IsRequired();
        builder.Property(e => e.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.Property(e => e.ScheduledAt).IsRequired();
        builder.Property(e => e.SentAt);
        builder.Property(e => e.AttemptCount).IsRequired().HasDefaultValue(0);
        builder.Property(e => e.LastError).HasMaxLength(2000);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.Status).IsRequired().HasMaxLength(32).HasDefaultValue("Pending");

        // Outbox processor pickup pattern: WHERE status='Pending' AND scheduled_at <= now ORDER BY scheduled_at
        builder.HasIndex(e => new { e.Status, e.ScheduledAt })
            .HasDatabaseName("IX_email_outbox_status_scheduled_at");

        // Idempotency: caller-supplied keys must be unique so duplicate inserts fail fast.
        builder.HasIndex(e => e.IdempotencyKey)
            .HasDatabaseName("IX_email_outbox_idempotency_key")
            .IsUnique();
    }
}
