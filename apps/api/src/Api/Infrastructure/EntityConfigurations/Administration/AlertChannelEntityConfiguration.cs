using Api.Infrastructure.Entities.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.Administration;

/// <summary>
/// Entity configuration for <see cref="AlertChannelEntity"/> (Issue #1840 SP5 F4-C7).
///
/// <para>The natural primary key is the channel <c>type</c> string — there is
/// at most one Email row and one Slack row per environment. No surrogate Guid:
/// the upsert command relies on the type discriminator to address the row.</para>
/// </summary>
internal class AlertChannelEntityConfiguration : IEntityTypeConfiguration<AlertChannelEntity>
{
    public void Configure(EntityTypeBuilder<AlertChannelEntity> builder)
    {
        builder.ToTable("alert_channels");

        builder.HasKey(e => e.Type);

        builder.Property(e => e.Type)
            .HasColumnName("type")
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(e => e.ConfigJson)
            .HasColumnName("config_json")
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(e => e.IsEnabled)
            .HasColumnName("is_enabled")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.LastTestedAt)
            .HasColumnName("last_tested_at");

        builder.Property(e => e.LastTestStatus)
            .HasColumnName("last_test_status")
            .HasMaxLength(16);

        builder.Property(e => e.LastTestMessage)
            .HasColumnName("last_test_message")
            .HasColumnType("text");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(200);

        builder.Property(e => e.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(200);

        // Postgres concurrency token — uses xmin system column under the hood.
        // We map to a bytea column for portability with the EF Core RowVersion
        // convention used elsewhere in the codebase.
        builder.Property(e => e.RowVersion)
            .HasColumnName("row_version")
            .IsRowVersion()
            .IsConcurrencyToken();

        // Issue #1941 / iso-2 Fix 1: dedup key for AlertFiredEvent dispatch per channel.
        builder.Property(e => e.LastDispatchedEventId)
            .HasColumnName("last_dispatched_event_id");

        // Per-spec: enforce the type discriminator at the schema level — only
        // 'email' and 'slack' are valid in #1840 scope.
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_alert_channels_type",
            "type IN ('email', 'slack')"));
    }
}
