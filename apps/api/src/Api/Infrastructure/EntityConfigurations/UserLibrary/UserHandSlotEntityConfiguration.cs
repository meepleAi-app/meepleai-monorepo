using Api.Infrastructure.Entities.UserLibrary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.UserLibrary;

/// <summary>
/// EF Core configuration for UserHandSlotEntity.
/// La Mia Mano: user hand slots (up to 4 per user, one per SlotType).
/// </summary>
internal class UserHandSlotEntityConfiguration : IEntityTypeConfiguration<UserHandSlotEntity>
{
    public void Configure(EntityTypeBuilder<UserHandSlotEntity> builder)
    {
        builder.ToTable("user_hand_slots");

        builder.HasKey(e => e.Id);

        // Properties
        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.SlotType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.EntityId)
            .IsRequired(false);

        builder.Property(e => e.EntityType)
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(e => e.EntityLabel)
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Property(e => e.EntityImageUrl)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(e => e.PinnedAt)
            .IsRequired(false);

        // Indexes
        // Unique: one slot per type per user
        builder.HasIndex(e => new { e.UserId, e.SlotType })
            .IsUnique()
            .HasDatabaseName("IX_user_hand_slots_user_id_slot_type");

        // Relationships
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
