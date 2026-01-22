using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// Entity Framework configuration for Contributor entity.
/// Issue #2726: Application - Query per Dashboard Utente
/// </summary>
internal sealed class ContributorEntityConfiguration : IEntityTypeConfiguration<ContributorEntity>
{
    public void Configure(EntityTypeBuilder<ContributorEntity> builder)
    {
        builder.ToTable("contributors");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(e => e.SharedGameId)
            .HasColumnName("shared_game_id")
            .IsRequired();

        builder.Property(e => e.IsPrimaryContributor)
            .HasColumnName("is_primary_contributor")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.ModifiedAt)
            .HasColumnName("modified_at");

        // Indexes
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("ix_contributors_user_id");

        builder.HasIndex(e => e.SharedGameId)
            .HasDatabaseName("ix_contributors_shared_game_id");

        builder.HasIndex(e => new { e.UserId, e.SharedGameId })
            .IsUnique()
            .HasDatabaseName("ix_contributors_user_shared_game_unique");

        // Relationships
        builder.HasOne(e => e.SharedGame)
            .WithMany(g => g.Contributors)
            .HasForeignKey(e => e.SharedGameId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Contributions)
            .WithOne(c => c.Contributor)
            .HasForeignKey(c => c.ContributorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
