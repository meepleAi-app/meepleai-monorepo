using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

internal class AuditLogEntityConfiguration : IEntityTypeConfiguration<AuditLogEntity>
{
    public void Configure(EntityTypeBuilder<AuditLogEntity> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.UserId).HasMaxLength(64);
        // #5156: the writers cut their values to these lengths (AuditLogColumns.FitToColumns).
        builder.Property(e => e.Action).IsRequired().HasMaxLength(AuditLogColumns.ActionLength);
        builder.Property(e => e.Resource).IsRequired().HasMaxLength(AuditLogColumns.ResourceLength);
        builder.Property(e => e.ResourceId).HasMaxLength(AuditLogColumns.ResourceIdLength);
        builder.Property(e => e.Result).IsRequired().HasMaxLength(AuditLogColumns.ResultLength);
        builder.Property(e => e.Details).HasMaxLength(AuditLogColumns.DetailsLength);
        builder.Property(e => e.IpAddress).HasMaxLength(AuditLogColumns.IpAddressLength);
        builder.Property(e => e.UserAgent).HasMaxLength(AuditLogColumns.UserAgentLength);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.UserId);

        // SP5 Admin Security S1: snapshot + traceability columns
        builder.Property(e => e.BeforeJson).HasColumnName("before_json").HasColumnType("jsonb");
        builder.Property(e => e.AfterJson).HasColumnName("after_json").HasColumnType("jsonb");
        builder.Property(e => e.ImpersonatedUserId).HasColumnName("impersonated_user_id");
        builder.Property(e => e.StepUpTokenId).HasColumnName("step_up_token_id");

        builder.HasIndex(e => e.ImpersonatedUserId)
            .HasDatabaseName("ix_audit_logs_impersonated_user_id")
            .HasFilter(@"""impersonated_user_id"" IS NOT NULL");
    }
}
