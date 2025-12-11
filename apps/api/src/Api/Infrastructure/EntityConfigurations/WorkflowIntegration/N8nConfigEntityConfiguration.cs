using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

public class N8NConfigEntityConfiguration : IEntityTypeConfiguration<N8NConfigEntity>
{
    public void Configure(EntityTypeBuilder<N8NConfigEntity> builder)
    {
        builder.ToTable("n8n_configs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(128);
        builder.Property(e => e.BaseUrl).IsRequired().HasMaxLength(512);
        builder.Property(e => e.ApiKeyEncrypted).IsRequired().HasMaxLength(512);
        builder.Property(e => e.WebhookUrl).HasMaxLength(512);
        builder.Property(e => e.IsActive).IsRequired();
        builder.Property(e => e.LastTestedAt);
        builder.Property(e => e.LastTestResult).HasMaxLength(512);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(64);
        builder.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.Name).IsUnique();
    }
}
