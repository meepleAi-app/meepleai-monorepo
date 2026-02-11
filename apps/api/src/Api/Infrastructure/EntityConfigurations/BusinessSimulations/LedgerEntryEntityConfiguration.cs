using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.BusinessSimulations;

/// <summary>
/// EF Core configuration for LedgerEntry entity.
/// Issue #3720: Financial Ledger Data Model
/// </summary>
internal class LedgerEntryEntityConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("ledger_entries");
        builder.HasKey(e => e.Id);

        // Transaction date
        builder.Property(e => e.Date)
            .IsRequired();

        // Enum: Income/Expense stored as integer
        builder.Property(e => e.Type)
            .IsRequired()
            .HasConversion<int>();

        // Enum: Category stored as integer
        builder.Property(e => e.Category)
            .IsRequired()
            .HasConversion<int>();

        // Value Object: Money (owned entity)
        builder.OwnsOne(e => e.Amount, money =>
        {
            money.Property(m => m.Amount)
                .IsRequired()
                .HasPrecision(18, 2)
                .HasColumnName("Amount");

            money.Property(m => m.Currency)
                .IsRequired()
                .HasMaxLength(3)
                .HasColumnName("Currency");
        });

        // Enum: Source stored as integer
        builder.Property(e => e.Source)
            .IsRequired()
            .HasConversion<int>();

        // Optional description
        builder.Property(e => e.Description)
            .HasMaxLength(500);

        // Optional JSON metadata
        builder.Property(e => e.Metadata)
            .HasColumnType("jsonb");

        // Optional user reference (null for Auto entries)
        builder.Property(e => e.CreatedByUserId)
            .IsRequired(false);

        // Audit timestamps
        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .IsRequired(false);

        // Indexes for common query patterns
        builder.HasIndex(e => e.Date)
            .HasDatabaseName("IX_LedgerEntries_Date")
            .IsDescending();

        builder.HasIndex(e => e.Type)
            .HasDatabaseName("IX_LedgerEntries_Type");

        builder.HasIndex(e => e.Category)
            .HasDatabaseName("IX_LedgerEntries_Category");

        builder.HasIndex(e => e.Source)
            .HasDatabaseName("IX_LedgerEntries_Source");

        builder.HasIndex(e => e.CreatedByUserId)
            .HasDatabaseName("IX_LedgerEntries_CreatedByUserId")
            .HasFilter("\"CreatedByUserId\" IS NOT NULL");

        // Composite index for date range + type queries
        builder.HasIndex(e => new { e.Date, e.Type })
            .HasDatabaseName("IX_LedgerEntries_Date_Type");
    }
}
