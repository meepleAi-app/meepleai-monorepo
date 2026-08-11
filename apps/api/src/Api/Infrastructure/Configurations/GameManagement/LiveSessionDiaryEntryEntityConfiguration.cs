using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.GameManagement;

/// <summary>
/// EF Core configuration for LiveSessionDiaryEntryEntity.
/// #2570 SP3 T2: Table schema and indexes for per-session diary entries.
/// The FK cascade from live_game_sessions is configured in
/// <see cref="LiveGameSessionEntityConfiguration"/>.
/// </summary>
internal sealed class LiveSessionDiaryEntryEntityConfiguration : IEntityTypeConfiguration<LiveSessionDiaryEntryEntity>
{
    public void Configure(EntityTypeBuilder<LiveSessionDiaryEntryEntity> builder)
    {
        builder.ToTable("live_session_diary_entries");

        builder.HasKey(e => e.Id);

        // --- Scalar Properties ---

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.LiveGameSessionId)
            .HasColumnName("live_game_session_id")
            .IsRequired();

        builder.Property(e => e.AuthorId)
            .HasColumnName("author_id")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.Text)
            .HasColumnName("text")
            .HasMaxLength(2000)
            .IsRequired();

        // --- Indexes ---

        builder.HasIndex(e => e.LiveGameSessionId)
            .HasDatabaseName("ix_live_session_diary_entries_session_id");

        builder.HasIndex(e => new { e.LiveGameSessionId, e.CreatedAt })
            .HasDatabaseName("ix_live_session_diary_entries_session_created_at");

        // FK cascade from LiveGameSession → configured in LiveGameSessionEntityConfiguration
    }
}
