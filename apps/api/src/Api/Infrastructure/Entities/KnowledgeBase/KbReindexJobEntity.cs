namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// EF Core persistence entity for <see cref="Api.BoundedContexts.KnowledgeBase.Domain.Entities.KbReindexJob"/>.
/// Issue #941 / ADR-057.
/// </summary>
public class KbReindexJobEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Guid UserId { get; set; }
    public string Status { get; set; } = "queued";
    public int TotalPdfs { get; set; }
    public int ProcessedPdfs { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? FailureReason { get; set; }
}
