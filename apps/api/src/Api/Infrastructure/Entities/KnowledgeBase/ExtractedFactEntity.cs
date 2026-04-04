namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// Issue #82: Zero-copyright training data extraction.
/// </summary>
public class ExtractedFactEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public string FactType { get; set; } = default!;
    public string FactData { get; set; } = "{}";
    public Guid? SourceDocumentId { get; set; }
    public string? ModelUsed { get; set; }
    public string? Reviewer { get; set; }
    public bool IsReviewed { get; set; }
    public DateTime ExtractionDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
