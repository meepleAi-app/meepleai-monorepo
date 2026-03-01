using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ToolkitSessionState.
/// Uses direct domain entity mapping (no separate persistence entity).
/// Issue #5148 — Epic B5.
/// </summary>
internal sealed class ToolkitSessionStateConfiguration : IEntityTypeConfiguration<ToolkitSessionState>
{
    public void Configure(EntityTypeBuilder<ToolkitSessionState> builder)
    {
        builder.ToTable("toolkit_session_states", schema: "session_tracking");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.SessionId)
            .HasColumnName("session_id")
            .IsRequired();

        builder.Property(e => e.ToolkitId)
            .HasColumnName("toolkit_id")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // WidgetStates stored as JSONB via backing field
        builder.Ignore(e => e.WidgetStates);
        builder.Property<string>("_widgetStatesJson")
            .HasColumnName("widget_states")
            .HasColumnType("jsonb")
            .HasDefaultValue("{}")
            .IsRequired();

        // Indexes
        builder.HasIndex(e => e.SessionId)
            .HasDatabaseName("ix_toolkit_session_states_session_id");

        builder.HasIndex(e => new { e.SessionId, e.ToolkitId })
            .IsUnique()
            .HasDatabaseName("uq_toolkit_session_states_session_toolkit");
    }
}
