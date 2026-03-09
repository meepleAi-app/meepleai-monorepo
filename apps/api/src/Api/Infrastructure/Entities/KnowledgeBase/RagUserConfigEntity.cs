namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// EF Core entity for persisting per-user RAG configuration.
/// Issue #5311: RAG Config backend persistence.
/// </summary>
public class RagUserConfigEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    /// <summary>
    /// Full RagConfigDto serialized as JSON.
    /// </summary>
    public string ConfigJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public UserEntity? User { get; set; }
}
