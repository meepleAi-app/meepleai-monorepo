using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.GameManagement;

/// <summary>
/// EF Core configuration for RecordPlayerEntity.
/// Issue #3888: Play record player management.
/// </summary>
internal class RecordPlayerEntityConfiguration : IEntityTypeConfiguration<RecordPlayerEntity>
{
    public void Configure(EntityTypeBuilder<RecordPlayerEntity> builder)
    {
        builder.ToTable("record_players");
        builder.HasKey(e => e.Id);

        // Properties
        builder.Property(e => e.PlayRecordId).IsRequired();
        builder.Property(e => e.UserId).IsRequired(false);
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.SetNull);  // Guest players remain if user deleted
        builder.Property(e => e.DisplayName).IsRequired().HasMaxLength(255);

        // Indexes
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_RecordPlayers_UserId")
            .HasFilter("\"UserId\" IS NOT NULL");

        builder.HasIndex(e => e.PlayRecordId)
            .HasDatabaseName("IX_RecordPlayers_PlayRecordId");

        // Relationships
        builder.HasOne(e => e.PlayRecord)
            .WithMany(r => r.Players)
            .HasForeignKey(e => e.PlayRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Scores)
            .WithOne(s => s.RecordPlayer)
            .HasForeignKey(s => s.RecordPlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
