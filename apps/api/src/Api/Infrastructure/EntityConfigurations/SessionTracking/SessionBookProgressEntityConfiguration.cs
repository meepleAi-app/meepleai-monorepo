using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SessionTracking;

/// <summary>
/// EF Core configuration for <see cref="SessionBookProgress"/> (Task C1).
/// One row per (campaign, book) pair, enforced by a unique composite index over
/// <c>(campaign_session_id, game_book_id)</c>. The <c>history_json</c> column
/// stores the ordered list of visited locations as a JSONB array.
/// </summary>
internal class SessionBookProgressEntityConfiguration : IEntityTypeConfiguration<SessionBookProgress>
{
    public void Configure(EntityTypeBuilder<SessionBookProgress> builder)
    {
        builder.ToTable("gamebook_session_book_progress", schema: "session_tracking");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CampaignSessionId).HasColumnName("campaign_session_id").IsRequired();
        builder.Property(e => e.GameBookId).HasColumnName("game_book_id").IsRequired();
        builder.Property(e => e.LastLocation).HasColumnName("last_location").HasMaxLength(40).IsRequired();
        builder.Property(e => e.HistoryJson).HasColumnName("history_json").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.LastVisitedAt).HasColumnName("last_visited_at").IsRequired();
        builder.Property(e => e.NotesJson).HasColumnName("notes_json").HasColumnType("jsonb");

        builder.HasIndex(e => new { e.CampaignSessionId, e.GameBookId })
               .IsUnique()
               .HasDatabaseName("ux_session_book_progress_campaign_book");
    }
}
