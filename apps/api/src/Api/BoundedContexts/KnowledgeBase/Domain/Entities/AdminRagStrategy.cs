namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Persisted RAG strategy entity for admin CRUD.
/// Stores step-based strategy definitions (retrieval, reranking, generation, etc.).
/// Issue #5314: Replace placeholder Admin Strategy endpoints with real CQRS handlers.
/// </summary>
public sealed class AdminRagStrategy
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string StepsJson { get; private set; } = "[]";
    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    private AdminRagStrategy() { }

    public static AdminRagStrategy Create(
        string name,
        string description,
        string stepsJson,
        Guid createdBy)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Strategy name is required.", nameof(name));

        return new AdminRagStrategy
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            StepsJson = stepsJson ?? "[]",
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }

    public void Update(string name, string description, string stepsJson)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Strategy name is required.", nameof(name));

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        StepsJson = stepsJson ?? "[]";
        UpdatedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
