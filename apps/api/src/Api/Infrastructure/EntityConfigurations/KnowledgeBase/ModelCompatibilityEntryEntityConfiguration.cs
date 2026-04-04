using System.Text.Json;
using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Api.Infrastructure.EntityConfigurations.KnowledgeBase;

/// <summary>
/// EF Core configuration for ModelCompatibilityEntryEntity.
/// Issue #5496: Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
internal class ModelCompatibilityEntryEntityConfiguration : IEntityTypeConfiguration<ModelCompatibilityEntryEntity>
{
    public void Configure(EntityTypeBuilder<ModelCompatibilityEntryEntity> builder)
    {
        builder.ToTable("model_compatibility_entries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.ModelId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Provider)
            .IsRequired()
            .HasMaxLength(50);

        // Fix: Npgsql cannot write string[] to jsonb directly — use JSON ValueConverter
        var stringArrayConverter = new ValueConverter<string[], string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<string>()
        );

        builder.Property(e => e.Alternatives)
            .HasColumnType("jsonb")
            .HasConversion(stringArrayConverter);

        builder.Property(e => e.ContextWindow)
            .IsRequired();

        builder.Property(e => e.Strengths)
            .HasColumnType("jsonb")
            .HasConversion(stringArrayConverter);

        builder.Property(e => e.IsCurrentlyAvailable)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.IsDeprecated)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.LastVerifiedAt);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .IsRequired();

        // Unique constraint on ModelId
        builder.HasIndex(e => e.ModelId)
            .IsUnique();

        // Indexes for query performance
        builder.HasIndex(e => e.Provider);
        builder.HasIndex(e => e.IsCurrentlyAvailable);
        builder.HasIndex(e => e.IsDeprecated);
    }
}
