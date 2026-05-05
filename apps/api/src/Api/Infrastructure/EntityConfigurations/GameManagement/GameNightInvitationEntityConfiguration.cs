using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.GameManagement;

/// <summary>
/// EF Core configuration for <see cref="GameNightInvitationEntity"/>.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
/// <remarks>
/// <para>
/// Indexes:
/// <list type="bullet">
///   <item><c>IX_game_night_invitations_token</c> — UNIQUE on Token, supports the
///         primary public lookup path (<c>GET /api/v1/invites/{token}</c>).</item>
///   <item><c>IX_game_night_invitations_event_email_status</c> — composite on
///         (GameNightId, Email, Status), supports the duplicate-pending guard
///         in <c>CreateGameNightInvitationByEmailCommand</c>.</item>
///   <item><c>IX_game_night_invitations_event_status</c> — composite on
///         (GameNightId, Status), supports the AcceptedSoFar count.</item>
/// </list>
/// </para>
/// <para>
/// FK: <c>GameNightId → game_night_events.id</c> with cascade delete, so
/// cancelling a parent event purges its invitations atomically.
/// </para>
/// </remarks>
internal class GameNightInvitationEntityConfiguration
    : IEntityTypeConfiguration<GameNightInvitationEntity>
{
    public void Configure(EntityTypeBuilder<GameNightInvitationEntity> builder)
    {
        builder.ToTable("game_night_invitations");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(i => i.Token).HasColumnName("token").HasMaxLength(32).IsRequired();
        builder.Property(i => i.GameNightId).HasColumnName("game_night_id").IsRequired();
        builder.Property(i => i.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(i => i.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(i => i.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(i => i.RespondedAt).HasColumnName("responded_at");
        builder.Property(i => i.RespondedByUserId).HasColumnName("responded_by_user_id");
        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(i => i.CreatedBy).HasColumnName("created_by").IsRequired();

        builder.HasIndex(i => i.Token)
            .HasDatabaseName("IX_game_night_invitations_token")
            .IsUnique();

        builder.HasIndex(i => new { i.GameNightId, i.Email, i.Status })
            .HasDatabaseName("IX_game_night_invitations_event_email_status");

        builder.HasIndex(i => new { i.GameNightId, i.Status })
            .HasDatabaseName("IX_game_night_invitations_event_status");

        builder.HasOne<GameNightEventEntity>()
            .WithMany()
            .HasForeignKey(i => i.GameNightId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
