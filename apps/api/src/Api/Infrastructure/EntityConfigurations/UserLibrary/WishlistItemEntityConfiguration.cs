using Api.Infrastructure.Entities.UserLibrary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.UserLibrary;

/// <summary>
/// EF Core configuration for WishlistItemEntity.
/// Issue #3917: Wishlist Management API.
/// </summary>
internal class WishlistItemEntityConfiguration : IEntityTypeConfiguration<WishlistItemEntity>
{
    public void Configure(EntityTypeBuilder<WishlistItemEntity> builder)
    {
        builder.ToTable("wishlist_items");

        builder.HasKey(e => e.Id);

        // Properties
        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.GameId)
            .IsRequired();

        builder.Property(e => e.Priority)
            .IsRequired()
            .HasDefaultValue(1); // Medium

        builder.Property(e => e.TargetPrice)
            .HasColumnType("decimal(10,2)")
            .IsRequired(false);

        builder.Property(e => e.Notes)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(e => e.AddedAt)
            .IsRequired();

        builder.Property(e => e.Visibility)
            .IsRequired()
            .HasDefaultValue(0); // Private

        builder.Property(e => e.UpdatedAt)
            .IsRequired(false);

        // Indexes
        // User's wishlist query
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_WishlistItems_UserId");

        // Unique: one game per user on wishlist
        builder.HasIndex(e => new { e.UserId, e.GameId })
            .IsUnique()
            .HasDatabaseName("IX_WishlistItems_UserId_GameId");

        // Highlights query: user + priority descending
        builder.HasIndex(e => new { e.UserId, e.Priority })
            .HasDatabaseName("IX_WishlistItems_UserId_Priority");

        // Relationships
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.SharedGame)
            .WithMany()
            .HasForeignKey(e => e.GameId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
