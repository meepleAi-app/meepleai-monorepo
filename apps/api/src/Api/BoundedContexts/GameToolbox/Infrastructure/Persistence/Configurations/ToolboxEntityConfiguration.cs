using Api.BoundedContexts.GameToolbox.Domain.Entities;
using Api.BoundedContexts.GameToolbox.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.GameToolbox.Infrastructure.Persistence.Configurations;

internal sealed class ToolboxEntityConfiguration : IEntityTypeConfiguration<Toolbox>
{
    public void Configure(EntityTypeBuilder<Toolbox> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("toolboxes", schema: "game_toolbox");
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(x => x.GameId).HasColumnName("game_id");
        builder.Property(x => x.TemplateId).HasColumnName("template_id");
        builder.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
        builder.Property(x => x.Mode).HasColumnName("mode").IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.SharedContext).HasColumnName("shared_context").HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<SharedContext>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new SharedContext());
        builder.Property(x => x.CurrentPhaseId).HasColumnName("current_phase_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted").IsRequired().HasDefaultValue(false);
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        builder.HasMany(x => x.Tools).WithOne().HasForeignKey(t => t.ToolboxId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Phases).WithOne().HasForeignKey(p => p.ToolboxId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.GameId).HasDatabaseName("ix_toolboxes_game_id");
        builder.HasIndex(x => x.IsDeleted).HasDatabaseName("ix_toolboxes_is_deleted");
    }
}

internal sealed class ToolboxToolEntityConfiguration : IEntityTypeConfiguration<ToolboxTool>
{
    public void Configure(EntityTypeBuilder<ToolboxTool> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("toolbox_tools", schema: "game_toolbox");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(x => x.ToolboxId).HasColumnName("toolbox_id").IsRequired();
        builder.Property(x => x.Type).HasColumnName("type").IsRequired().HasMaxLength(50);
        builder.Property(x => x.Config).HasColumnName("config").HasColumnType("jsonb").HasDefaultValue("{}");
        builder.Property(x => x.State).HasColumnName("state").HasColumnType("jsonb").HasDefaultValue("{}");
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled").IsRequired().HasDefaultValue(true);
        builder.Property(x => x.Order).HasColumnName("sort_order").IsRequired();

        builder.HasIndex(x => x.ToolboxId).HasDatabaseName("ix_toolbox_tools_toolbox_id");
    }
}

internal sealed class PhaseEntityConfiguration : IEntityTypeConfiguration<Phase>
{
    public void Configure(EntityTypeBuilder<Phase> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("toolbox_phases", schema: "game_toolbox");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(x => x.ToolboxId).HasColumnName("toolbox_id").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(100);
        builder.Property(x => x.Order).HasColumnName("sort_order").IsRequired();
        builder.Property(x => x.ActiveToolIds).HasColumnName("active_tool_ids").HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<Guid>());

        builder.HasIndex(x => x.ToolboxId).HasDatabaseName("ix_toolbox_phases_toolbox_id");
    }
}

internal sealed class ToolboxTemplateEntityConfiguration : IEntityTypeConfiguration<ToolboxTemplate>
{
    public void Configure(EntityTypeBuilder<ToolboxTemplate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("toolbox_templates", schema: "game_toolbox");
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
        builder.Property(x => x.GameId).HasColumnName("game_id");
        builder.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(200);
        builder.Property(x => x.Mode).HasColumnName("mode").IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Source).HasColumnName("source").IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ToolsJson).HasColumnName("tools_json").HasColumnType("jsonb").HasDefaultValue("[]");
        builder.Property(x => x.PhasesJson).HasColumnName("phases_json").HasColumnType("jsonb").HasDefaultValue("[]");
        builder.Property(x => x.SharedContextDefaultsJson).HasColumnName("shared_context_defaults_json").HasColumnType("jsonb").HasDefaultValue("{}");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted").IsRequired().HasDefaultValue(false);
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(x => x.GameId).HasDatabaseName("ix_toolbox_templates_game_id");
    }
}
