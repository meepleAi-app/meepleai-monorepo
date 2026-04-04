using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.KnowledgeBase;

[Table("rag_quality_logs")]
public class RagQualityLogEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("thread_id")]
    public Guid? ThreadId { get; set; }

    [Column("game_id")]
    public Guid? GameId { get; set; }

    [Column("query_length")]
    public int QueryLength { get; set; }

    [Column("chunks_retrieved")]
    public int ChunksRetrieved { get; set; }

    [Column("chunks_used")]
    public int ChunksUsed { get; set; }

    [Column("context_precision")]
    public decimal? ContextPrecision { get; set; }

    [Column("citations_count")]
    public int CitationsCount { get; set; }

    [Required]
    [Column("strategy")]
    [MaxLength(50)]
    public string Strategy { get; set; } = string.Empty;

    [Required]
    [Column("model_used")]
    [MaxLength(100)]
    public string ModelUsed { get; set; } = string.Empty;

    [Column("input_tokens")]
    public int? InputTokens { get; set; }

    [Column("output_tokens")]
    public int? OutputTokens { get; set; }

    [Column("latency_ms")]
    public int LatencyMs { get; set; }

    [Column("cache_hit")]
    public bool CacheHit { get; set; }

    [Column("no_relevant_context")]
    public bool NoRelevantContext { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
