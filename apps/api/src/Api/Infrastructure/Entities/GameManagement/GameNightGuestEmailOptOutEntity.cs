using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// EF Core persistence entity for the addresses that opted out of game night emails (#4846).
/// </summary>
/// <remarks>
/// One row per address (unique index on <see cref="Email"/>, which is stored trimmed and in lower case).
/// There is no foreign key to the invitation it came through: the entry must outlive that invitation, and
/// the night it belonged to, which can be deleted.
/// </remarks>
[Table("game_night_guest_email_opt_outs")]
public class GameNightGuestEmailOptOutEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("email")]
    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Column("opted_out_at")]
    public DateTimeOffset OptedOutAt { get; set; }

    [Required]
    [Column("source_invitation_id")]
    public Guid SourceInvitationId { get; set; }
}
