using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// Entity Framework configuration for ContributionRecord entity.
/// Issue #2726: Application - Query per Dashboard Utente
/// </summary>
internal sealed class ContributionRecordEntityConfiguration : IEntityTypeConfiguration<ContributionRecordEntity>
{
    public void Configure(EntityTypeBuilder<ContributionRecordEntity> builder)
    {
        builder.ToTable("contribution_records");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.ContributorId)
            .HasColumnName("contributor_id")
            .IsRequired();

        builder.Property(e => e.Type)
            .HasColumnName("type")
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(e => e.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(e => e.ContributedAt)
            .HasColumnName("contributed_at")
            .IsRequired();

        builder.Property(e => e.ShareRequestId)
            .HasColumnName("share_request_id");

        builder.Property(e => e.DocumentIdsJson)
            .HasColumnName("document_ids")
            .HasColumnType("jsonb");

        builder.Property(e => e.IncludesGameData)
            .HasColumnName("includes_game_data")
            .IsRequired();

        builder.Property(e => e.IncludesMetadata)
            .HasColumnName("includes_metadata")
            .IsRequired();

        // Indexes
        builder.HasIndex(e => e.ContributorId)
            .HasDatabaseName("ix_contribution_records_contributor_id");

        builder.HasIndex(e => e.ShareRequestId)
            .HasDatabaseName("ix_contribution_records_share_request_id")
            .HasFilter("share_request_id IS NOT NULL");

        builder.HasIndex(e => new { e.ContributorId, e.Version })
            .HasDatabaseName("ix_contribution_records_contributor_version");
    }
}
