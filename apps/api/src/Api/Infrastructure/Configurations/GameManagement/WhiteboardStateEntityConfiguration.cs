using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.GameManagement;

/// <summary>
/// Entity Framework configuration for WhiteboardState persistence entity.
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
internal sealed class WhiteboardStateEntityConfiguration : IEntityTypeConfiguration<WhiteboardStateEntity>
{
    public void Configure(EntityTypeBuilder<WhiteboardStateEntity> builder)
    {
        builder.ToTable("whiteboard_states");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(w => w.SessionId)
            .HasColumnName("session_id")
            .IsRequired();

        builder.Property(w => w.StrokesJson)
            .HasColumnName("strokes_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(w => w.StructuredJson)
            .HasColumnName("structured_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(w => w.LastModifiedBy)
            .HasColumnName("last_modified_by")
            .IsRequired();

        builder.Property(w => w.LastModifiedAt)
            .HasColumnName("last_modified_at")
            .IsRequired();

        builder.Property(w => w.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // One whiteboard per session
        builder.HasIndex(w => w.SessionId)
            .HasDatabaseName("ix_whiteboard_states_session_id")
            .IsUnique();
    }
}
