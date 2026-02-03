using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.KnowledgeBase;

/// <summary>
/// EF Core configuration for TypologyPromptTemplateEntity.
/// Issue #3175
/// </summary>
internal class TypologyPromptTemplateEntityConfiguration : IEntityTypeConfiguration<TypologyPromptTemplateEntity>
{
    public void Configure(EntityTypeBuilder<TypologyPromptTemplateEntity> builder)
    {
        builder.ToTable("typology_prompt_templates", t =>
        {
            t.HasCheckConstraint(
                "ck_typology_prompt_templates_version",
                "version >= 1");
        });

        // Primary key
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        // Foreign key to AgentTypology
        builder.Property(e => e.TypologyId)
            .HasColumnName("typology_id")
            .IsRequired();

        // Properties
        builder.Property(e => e.Content)
            .HasColumnName("content")
            .IsRequired()
            .HasMaxLength(10000);

        builder.Property(e => e.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(e => e.IsCurrent)
            .HasColumnName("is_current")
            .IsRequired()
            .HasDefaultValue(false);

        // Audit
        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(e => e.TypologyId)
            .HasDatabaseName("ix_typology_prompt_templates_typology_id");

        builder.HasIndex(e => new { e.TypologyId, e.Version })
            .HasDatabaseName("ix_typology_prompt_templates_typology_version")
            .IsUnique();

        // Only one current template per typology
        builder.HasIndex(e => new { e.TypologyId, e.IsCurrent })
            .HasDatabaseName("ix_typology_prompt_templates_current")
            .HasFilter("is_current = true")
            .IsUnique();

        // Relationship to AgentTypology (already configured in AgentTypologyEntityConfiguration)
        builder.HasOne(e => e.Typology)
            .WithMany(t => t.TypologyPromptTemplates)
            .HasForeignKey(e => e.TypologyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
