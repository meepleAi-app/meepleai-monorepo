using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;

namespace Api.Infrastructure.Entities.Authentication;

/// <summary>
/// Infrastructure entity for shareable chat thread links.
/// Maps to ShareLink domain aggregate.
/// </summary>
[Table("share_links")]
internal sealed class ShareLinkEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("thread_id")]
    public Guid ThreadId { get; set; }

    [Required]
    [Column("creator_id")]
    public Guid CreatorId { get; set; }

    [Required]
    [Column("role")]
    public ShareLinkRole Role { get; set; }

    [Required]
    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("revoked_at")]
    public DateTime? RevokedAt { get; set; }

    [Column("label")]
    [MaxLength(200)]
    public string? Label { get; set; }

    [Column("access_count")]
    public int AccessCount { get; set; }

    [Column("last_accessed_at")]
    public DateTime? LastAccessedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(ThreadId))]
    public ChatThreadEntity? ChatThread { get; set; }

    [ForeignKey(nameof(CreatorId))]
    public UserEntity? Creator { get; set; }
}
