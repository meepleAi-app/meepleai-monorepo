using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.GameToolkit.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the Toolkit aggregate.
/// Direct domain-entity mapping — no separate persistence entity.
/// Issue #5145 — Epic B2.
/// </summary>
internal sealed class ToolkitEntityConfiguration : IEntityTypeConfiguration<Toolkit>
{
    public void Configure(EntityTypeBuilder<Toolkit> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("toolkits", schema: "game_toolkit");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(x => x.GameId)
            .HasColumnName("game_id")
            .IsRequired();

        builder.Property(x => x.OwnerUserId)
            .HasColumnName("owner_user_id")
            .IsRequired(false);

        builder.Property(x => x.IsDefault)
            .HasColumnName("is_default")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.DisplayName)
            .HasColumnName("display_name")
            .IsRequired(false)
            .HasMaxLength(200);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Map via backing field (_widgets) since Widgets property is IReadOnlyList
        builder.HasMany(x => x.Widgets)
            .WithOne()
            .HasForeignKey(w => w.ToolkitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Widgets)
            .HasField("_widgets")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // BR-03: unique active toolkit per (game_id, owner_user_id)
        builder.HasIndex(x => new { x.GameId, x.OwnerUserId })
            .IsUnique()
            .HasDatabaseName("uq_toolkits_game_owner");

        builder.HasIndex(x => x.GameId)
            .HasDatabaseName("ix_toolkits_game_id");
    }
}
