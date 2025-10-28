using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

public class PromptVersionEntityConfiguration : IEntityTypeConfiguration<PromptVersionEntity>
{
    public void Configure(EntityTypeBuilder<PromptVersionEntity> builder)
    {
        builder.ToTable("prompt_versions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.TemplateId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.VersionNumber).IsRequired();
        builder.Property(e => e.Content).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();
        builder.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.Metadata).HasMaxLength(4096);
        builder.HasOne(e => e.Template)
            .WithMany(t => t.Versions)
            .HasForeignKey(e => e.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.TemplateId, e.VersionNumber }).IsUnique();
        builder.HasIndex(e => new { e.TemplateId, e.IsActive });
        builder.HasIndex(e => e.CreatedAt);
    }
}
