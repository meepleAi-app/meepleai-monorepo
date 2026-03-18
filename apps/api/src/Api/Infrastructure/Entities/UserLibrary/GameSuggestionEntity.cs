using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.UserLibrary;

/// <summary>
/// Infrastructure entity for game suggestions.
/// Admin Invitation Flow: stores recommended games for invited users.
/// </summary>
[Table("game_suggestions")]
public sealed class GameSuggestionEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required]
    [Column("game_id")]
    public Guid GameId { get; set; }

    [Required]
    [Column("suggested_by_user_id")]
    public Guid SuggestedByUserId { get; set; }

    [MaxLength(50)]
    [Column("source")]
    public string? Source { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Required]
    [Column("is_dismissed")]
    public bool IsDismissed { get; set; }

    [Required]
    [Column("is_accepted")]
    public bool IsAccepted { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public UserEntity? User { get; set; }

    [ForeignKey(nameof(SuggestedByUserId))]
    public UserEntity? SuggestedByUser { get; set; }
}
