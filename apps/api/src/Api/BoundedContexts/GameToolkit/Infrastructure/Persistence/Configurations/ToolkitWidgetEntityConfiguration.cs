using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.GameToolkit.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ToolkitWidget child entity.
/// Issue #5145 — Epic B2.
/// </summary>
internal sealed class ToolkitWidgetEntityConfiguration : IEntityTypeConfiguration<ToolkitWidget>
{
    public void Configure(EntityTypeBuilder<ToolkitWidget> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("toolkit_widgets", schema: "game_toolkit");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(x => x.ToolkitId)
            .HasColumnName("toolkit_id")
            .IsRequired();

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.IsEnabled)
            .HasColumnName("is_enabled")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.DisplayOrder)
            .HasColumnName("display_order")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.Config)
            .HasColumnName("config")
            .IsRequired()
            .HasDefaultValue("{}")
            .HasColumnType("jsonb");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // One widget per type per toolkit
        builder.HasIndex(x => new { x.ToolkitId, x.Type })
            .IsUnique()
            .HasDatabaseName("uq_toolkit_widgets_toolkit_type");
    }
}
