using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

public class PromptAuditLogEntityConfiguration : IEntityTypeConfiguration<PromptAuditLogEntity>
{
    public void Configure(EntityTypeBuilder<PromptAuditLogEntity> builder)
    {
        builder.ToTable("prompt_audit_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.TemplateId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.VersionId).HasMaxLength(64);
        builder.Property(e => e.Action).IsRequired().HasMaxLength(64);
        builder.Property(e => e.ChangedByUserId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.ChangedAt).IsRequired();
        builder.Property(e => e.Details).HasMaxLength(2048);
        builder.HasOne(e => e.Template)
            .WithMany(t => t.AuditLogs)
            .HasForeignKey(e => e.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Version)
            .WithMany(v => v.AuditLogs)
            .HasForeignKey(e => e.VersionId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.ChangedBy)
            .WithMany()
            .HasForeignKey(e => e.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.TemplateId);
        builder.HasIndex(e => e.VersionId);
        builder.HasIndex(e => e.ChangedAt);
        builder.HasIndex(e => e.Action);
    }
}
