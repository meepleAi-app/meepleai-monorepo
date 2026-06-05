using Api.Infrastructure.Entities.BusinessSimulations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.BusinessSimulations;

/// <summary>
/// Entity configuration for <see cref="AppBudgetEntity"/> (Issue #1838 SP5 F4-C5).
///
/// <para>Singleton table — the primary key is a surrogate Guid and uniqueness
/// of the singleton is enforced by the repository (not via a DB constraint),
/// so the schema stays portable for future multi-tenant scoping.</para>
/// </summary>
internal class AppBudgetEntityConfiguration : IEntityTypeConfiguration<AppBudgetEntity>
{
    public void Configure(EntityTypeBuilder<AppBudgetEntity> builder)
    {
        builder.ToTable("app_budgets");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.MonthlyLimitAmount)
            .HasColumnName("monthly_limit_amount")
            .HasPrecision(12, 4)
            .IsRequired();

        builder.Property(e => e.MonthlyLimitCurrency)
            .HasColumnName("monthly_limit_currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(e => e.AlertThresholdPct)
            .HasColumnName("alert_threshold_pct")
            .IsRequired()
            .HasDefaultValue(80);

        builder.Property(e => e.CriticalThresholdPct)
            .HasColumnName("critical_threshold_pct")
            .IsRequired()
            .HasDefaultValue(95);

        builder.Property(e => e.IsEnabled)
            .HasColumnName("is_enabled")
            .IsRequired()
            .HasDefaultValue(true);

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
        // convention used elsewhere in the codebase (mirrors AlertChannelEntity).
        builder.Property(e => e.RowVersion)
            .HasColumnName("row_version")
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_app_budgets_monthly_limit_positive",
            "monthly_limit_amount > 0"));

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_app_budgets_alert_below_critical",
            "alert_threshold_pct < critical_threshold_pct"));
    }
}
