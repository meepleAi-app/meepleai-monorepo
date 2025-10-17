using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities;

/// <summary>
/// PERF-03: Cache statistics for tracking hit rates and popular questions
/// </summary>
[Table("cache_stats")]
public class CacheStatEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("game_id")]
    [StringLength(50)]
    public string GameId { get; set; } = string.Empty;

    [Required]
    [Column("question_hash")]
    [StringLength(64)]
    public string QuestionHash { get; set; } = string.Empty;

    [Column("hit_count")]
    public long HitCount { get; set; }

    [Column("miss_count")]
    public long MissCount { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("last_hit_at")]
    public DateTime LastHitAt { get; set; }
}
