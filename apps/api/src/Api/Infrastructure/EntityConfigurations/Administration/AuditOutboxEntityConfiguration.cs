using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

internal class AuditOutboxEntityConfiguration : IEntityTypeConfiguration<AuditOutboxEntity>
{
    public void Configure(EntityTypeBuilder<AuditOutboxEntity> builder)
    {
        builder.ToTable("audit_outbox");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").IsRequired();
        builder.Property(e => e.RetryCount).HasColumnName("retry_count").IsRequired();
        builder.Property(e => e.LastError).HasColumnName("last_error").HasMaxLength(2048);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ProcessedAt).HasColumnName("processed_at");

        builder.HasIndex(e => new { e.Status, e.CreatedAt })
               .HasDatabaseName("ix_audit_outbox_status_created_at");
    }
}
