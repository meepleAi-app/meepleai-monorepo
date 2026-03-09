using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.GameManagement;

/// <summary>
/// EF Core configuration for GameNightPlaylist persistence entity.
/// Issue #5582: Game Night Playlist backend CRUD with sharing.
/// </summary>
internal sealed class GameNightPlaylistEntityConfiguration : IEntityTypeConfiguration<GameNightPlaylistEntity>
{
    public void Configure(EntityTypeBuilder<GameNightPlaylistEntity> builder)
    {
        builder.ToTable("game_night_playlists");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.ScheduledDate)
            .HasColumnName("scheduled_date");

        builder.Property(p => p.CreatorUserId)
            .HasColumnName("creator_user_id")
            .IsRequired();

        builder.Property(p => p.ShareToken)
            .HasColumnName("share_token")
            .HasMaxLength(50);

        builder.Property(p => p.IsShared)
            .HasColumnName("is_shared")
            .IsRequired();

        builder.Property(p => p.GamesJson)
            .HasColumnName("games_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(p => p.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired();

        builder.Property(p => p.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(p => p.RowVersion)
            .HasColumnName("row_version")
            .IsRowVersion();

        // Soft-delete query filter
        builder.HasQueryFilter(p => !p.IsDeleted);

        // Indexes
        builder.HasIndex(p => p.CreatorUserId)
            .HasDatabaseName("ix_game_night_playlists_creator_user_id");

        builder.HasIndex(p => p.ShareToken)
            .HasDatabaseName("ix_game_night_playlists_share_token")
            .IsUnique()
            .HasFilter("share_token IS NOT NULL");
    }
}
