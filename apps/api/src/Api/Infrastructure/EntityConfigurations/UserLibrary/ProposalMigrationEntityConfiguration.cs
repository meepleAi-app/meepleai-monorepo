using Api.Infrastructure.Entities.UserLibrary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.UserLibrary;

/// <summary>
/// EF Core configuration for ProposalMigrationEntity.
/// Issue #3666: Phase 5 - Migration Choice Flow.
/// </summary>
public class ProposalMigrationEntityConfiguration : IEntityTypeConfiguration<ProposalMigrationEntity>
{
    public void Configure(EntityTypeBuilder<ProposalMigrationEntity> builder)
    {
        builder.ToTable("ProposalMigrations");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ShareRequestId)
            .IsRequired();

        builder.Property(e => e.PrivateGameId)
            .IsRequired();

        builder.Property(e => e.SharedGameId)
            .IsRequired();

        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.Choice)
            .IsRequired()
            .HasComment("0 = Pending, 1 = LinkToCatalog, 2 = KeepPrivate");

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(e => e.ChoiceAt)
            .IsRequired(false);

        builder.Property(e => e.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate();

        // Indexes
        builder.HasIndex(e => e.ShareRequestId)
            .IsUnique()
            .HasDatabaseName("IX_ProposalMigrations_ShareRequestId");

        builder.HasIndex(e => new { e.UserId, e.Choice })
            .HasDatabaseName("IX_ProposalMigrations_UserId_Choice");

        builder.HasIndex(e => e.PrivateGameId)
            .HasDatabaseName("IX_ProposalMigrations_PrivateGameId");

        builder.HasIndex(e => e.SharedGameId)
            .HasDatabaseName("IX_ProposalMigrations_SharedGameId");

        // Navigation
        builder.HasOne(e => e.PrivateGame)
            .WithMany()
            .HasForeignKey(e => e.PrivateGameId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
