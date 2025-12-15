using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

// CONFIG-01: System configurations
internal class SystemConfigurationEntityConfiguration : IEntityTypeConfiguration<SystemConfigurationEntity>
{
    public void Configure(EntityTypeBuilder<SystemConfigurationEntity> builder)
    {
        builder.ToTable("system_configurations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.Key).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Value).IsRequired();
        builder.Property(e => e.ValueType).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.Property(e => e.Category).IsRequired().HasMaxLength(100);
        builder.Property(e => e.IsActive).IsRequired();
        builder.Property(e => e.RequiresRestart).IsRequired();
        builder.Property(e => e.Environment).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Version).IsRequired();
        builder.Property(e => e.PreviousValue);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.UpdatedByUserId).HasMaxLength(64);
        builder.Property(e => e.LastToggledAt);

        // Relationships
        builder.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.UpdatedBy)
            .WithMany()
            .HasForeignKey(e => e.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes for performance
        builder.HasIndex(e => new { e.Key, e.Environment }).IsUnique();
        builder.HasIndex(e => e.Category);
        builder.HasIndex(e => e.IsActive);
        builder.HasIndex(e => e.Environment);
        builder.HasIndex(e => e.UpdatedAt);
    }
}
