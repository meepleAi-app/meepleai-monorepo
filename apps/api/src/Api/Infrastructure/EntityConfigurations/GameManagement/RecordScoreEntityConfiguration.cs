using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.GameManagement;

/// <summary>
/// EF Core configuration for RecordScoreEntity.
/// Issue #3888: Multi-dimensional scoring for play records.
/// </summary>
internal class RecordScoreEntityConfiguration : IEntityTypeConfiguration<RecordScoreEntity>
{
    public void Configure(EntityTypeBuilder<RecordScoreEntity> builder)
    {
        builder.ToTable("record_scores");
        builder.HasKey(e => e.Id);

        // Properties
        builder.Property(e => e.RecordPlayerId).IsRequired();
        builder.Property(e => e.Dimension).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Value).IsRequired();
        builder.Property(e => e.Unit).HasMaxLength(20);

        // Unique constraint: one score per dimension per player
        builder.HasIndex(e => new { e.RecordPlayerId, e.Dimension })
            .IsUnique()
            .HasDatabaseName("IX_RecordScores_Player_Dimension_Unique");

        // Relationships
        builder.HasOne(e => e.RecordPlayer)
            .WithMany(p => p.Scores)
            .HasForeignKey(e => e.RecordPlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
