namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// Issue #83: Copyright compliance similarity audit.
/// </summary>
public class SimilarityAuditResultEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PairId { get; set; }
    public string SourceGame { get; set; } = default!;
    public string CheckName { get; set; } = default!;
    public decimal Score { get; set; }
    public bool Passed { get; set; }
    public decimal Threshold { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
