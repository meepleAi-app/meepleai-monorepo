using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.GameManagement;

/// <summary>
/// EF Core configuration for <see cref="GameNightGuestEmailOptOutEntity"/> (#4846).
/// </summary>
/// <remarks>
/// <c>IX_game_night_guest_email_opt_outs_email</c> — UNIQUE on Email: it answers «is this address on the list?»
/// before every send to a guest, and it is what makes two taps of the same button one entry.
/// </remarks>
internal class GameNightGuestEmailOptOutEntityConfiguration
    : IEntityTypeConfiguration<GameNightGuestEmailOptOutEntity>
{
    public void Configure(EntityTypeBuilder<GameNightGuestEmailOptOutEntity> builder)
    {
        builder.ToTable("game_night_guest_email_opt_outs");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(o => o.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(o => o.OptedOutAt).HasColumnName("opted_out_at").IsRequired();
        builder.Property(o => o.SourceInvitationId).HasColumnName("source_invitation_id").IsRequired();

        builder.HasIndex(o => o.Email)
            .HasDatabaseName("IX_game_night_guest_email_opt_outs_email")
            .IsUnique();
    }
}
