using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.Authentication;

/// <summary>
/// Infrastructure entity for the public Alpha-program waitlist.
/// Maps to <see cref="Api.BoundedContexts.Authentication.Domain.Entities.WaitlistEntry"/> domain aggregate.
/// Spec §3.5 (2026-04-27-v2-migration-wave-a-2-join.md).
/// </summary>
[Table("waitlist_entries")]
public sealed class WaitlistEntryEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(254)]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [MaxLength(80)]
    [Column("name")]
    public string? Name { get; set; }

    [Required]
    [MaxLength(40)]
    [Column("game_preference_id")]
    public string GamePreferenceId { get; set; } = string.Empty;

    [MaxLength(80)]
    [Column("game_preference_other")]
    public string? GamePreferenceOther { get; set; }

    [Required]
    [Column("newsletter_opt_in")]
    public bool NewsletterOptIn { get; set; }

    [Required]
    [Column("position")]
    public int Position { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("contacted_at")]
    public DateTime? ContactedAt { get; set; }
}
