using Api.BoundedContexts.KbQuality.Domain.Budget;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.KbQuality;

/// <summary>
/// EF Core configuration for <see cref="KbQualityBudgetCounter"/> (#1675).
/// Composite PK on (TenantId, YearMonth); single secondary index on YearMonth
/// for the monthly-reset job.
/// </summary>
internal sealed class KbQualityBudgetCounterEntityConfiguration
    : IEntityTypeConfiguration<KbQualityBudgetCounter>
{
    public void Configure(EntityTypeBuilder<KbQualityBudgetCounter> builder)
    {
        builder.ToTable("kb_quality_budget_counters");
        builder.HasKey(e => new { e.TenantId, e.YearMonth });
        builder.Property(e => e.YearMonth).HasMaxLength(7).IsRequired();  // "yyyy-MM" = 7 chars
        builder.Property(e => e.SpentUsd).HasPrecision(10, 4).IsRequired();
        builder.HasIndex(e => e.YearMonth);  // for monthly reset job

        // Issue #1675: optimistic concurrency via Postgres xmin. Nullable byte[] mirrors
        // the convention adopted for PdfDocumentEntity.RowVersion (#1802 landmine workaround).
        // #3651 lotto 9 — pattern xmin di ADR-060. `IsRowVersion()` su un `byte[]` NON mappa alla
        // colonna di sistema su Npgsql, malgrado il commento originale dicesse il contrario: serve
        // la configurazione esplicita. Finché è mancata, il retry loop di IncrementSpentAsync non
        // ha mai visto un conflitto da gestire.
        builder.Property(e => e.Xmin)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
