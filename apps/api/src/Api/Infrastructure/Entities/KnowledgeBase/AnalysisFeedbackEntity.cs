namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// Issue #77: User feedback on analysis quality.
/// </summary>
public class AnalysisFeedbackEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid AnalysisId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public bool IsReviewed { get; set; }
    public bool IsExported { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
