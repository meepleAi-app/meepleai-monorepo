using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

internal class PromptTemplateEntityConfiguration : IEntityTypeConfiguration<PromptTemplateEntity>
{
    public void Configure(EntityTypeBuilder<PromptTemplateEntity> builder)
    {
        builder.ToTable("prompt_templates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(128);
        builder.Property(e => e.Description).HasMaxLength(512);
        builder.Property(e => e.Category).HasMaxLength(64);
        builder.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.Name).IsUnique();
        builder.HasIndex(e => e.Category);
        builder.HasIndex(e => e.CreatedAt);
    }
}
