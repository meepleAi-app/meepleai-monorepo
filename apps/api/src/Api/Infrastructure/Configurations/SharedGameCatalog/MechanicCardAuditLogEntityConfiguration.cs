using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// EF configuration for <see cref="MechanicCardAuditLogEntity"/> (ADR-051 audit trail, #527).
/// Append-only; FK to the card configured on the <see cref="MechanicCardEntityConfiguration"/> side
/// (ON DELETE CASCADE).
/// </summary>
internal sealed class MechanicCardAuditLogEntityConfiguration : IEntityTypeConfiguration<MechanicCardAuditLogEntity>
{
    public void Configure(EntityTypeBuilder<MechanicCardAuditLogEntity> builder)
    {
        builder.ToTable("mechanic_card_audit_log", t =>
        {
            t.HasCheckConstraint(
                "ck_mechanic_card_audit_log_action",
                "action IN ('published', 'suppressed', 'unsuppressed', 'revised')");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id").IsRequired();
        builder.Property(a => a.CardId).HasColumnName("card_id").IsRequired();
        builder.Property(a => a.Action).HasColumnName("action").HasMaxLength(32).IsRequired();
        builder.Property(a => a.ActorId).HasColumnName("actor_id").IsRequired();
        builder.Property(a => a.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(a => a.Metadata).HasColumnName("metadata").HasColumnType("jsonb");

        builder.HasIndex(a => a.CardId).HasDatabaseName("ix_mechanic_card_audit_log_card_id");
    }
}
