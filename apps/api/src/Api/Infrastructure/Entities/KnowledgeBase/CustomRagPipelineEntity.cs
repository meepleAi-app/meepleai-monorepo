using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// EF Core entity for custom RAG pipeline persistence.
/// Issue #3453: Visual RAG Strategy Builder - Save/Load/Export functionality.
/// </summary>
[Table("custom_rag_pipelines")]
public class CustomRagPipelineEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("name")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    [Column("pipeline_json", TypeName = "jsonb")]
    public string PipelineJson { get; set; } = string.Empty;

    [Required]
    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("is_published")]
    public bool IsPublished { get; set; }

    [Column("tags", TypeName = "text[]")]
    public string[] Tags { get; set; } = Array.Empty<string>();

    [Column("is_template")]
    public bool IsTemplate { get; set; }

    [Column("access_tier")]
    [MaxLength(50)]
    public string? AccessTier { get; set; }
}
