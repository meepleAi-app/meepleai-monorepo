using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.GameManagement;

/// <summary>
/// Entity Framework configuration for ToolState persistence entity.
/// Issue #4754: ToolState Entity + Toolkit ↔ Session Integration.
/// </summary>
internal sealed class ToolStateEntityConfiguration : IEntityTypeConfiguration<ToolStateEntity>
{
    public void Configure(EntityTypeBuilder<ToolStateEntity> builder)
    {
        builder.ToTable("tool_states");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(t => t.SessionId)
            .HasColumnName("session_id")
            .IsRequired();

        builder.Property(t => t.ToolkitId)
            .HasColumnName("toolkit_id")
            .IsRequired();

        builder.Property(t => t.ToolName)
            .HasColumnName("tool_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.ToolType)
            .HasColumnName("tool_type")
            .IsRequired();

        builder.Property(t => t.StateDataJson)
            .HasColumnName("state_data_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(t => t.LastUpdatedAt)
            .HasColumnName("last_updated_at")
            .IsRequired();

        // Indexes for query performance
        builder.HasIndex(t => t.SessionId)
            .HasDatabaseName("ix_tool_states_session_id");

        builder.HasIndex(t => new { t.SessionId, t.ToolName })
            .HasDatabaseName("ix_tool_states_session_tool_name")
            .IsUnique();

        builder.HasIndex(t => t.ToolkitId)
            .HasDatabaseName("ix_tool_states_toolkit_id");
    }
}
