namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// RAG Enhancement: RAPTOR hierarchical summary node.
/// Stores multi-level summaries produced by recursive clustering of text chunks.
/// Level 0 = leaf (original chunks), Level 1 = section summaries, Level 2 = overview.
/// </summary>
public class RaptorSummaryEntity
{
    public Guid Id { get; set; }
    public Guid PdfDocumentId { get; set; }
    public Guid GameId { get; set; }
    public int TreeLevel { get; set; }         // 0=leaf (original chunks), 1=section, 2=overview
    public int ClusterIndex { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public int SourceChunkCount { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public PdfDocumentEntity? PdfDocument { get; set; }
    public GameEntity? Game { get; set; }
}
