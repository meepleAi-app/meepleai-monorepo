namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// Persistence entity for TypologyPromptTemplate.
/// Issue #3175
/// </summary>
public class TypologyPromptTemplateEntity
{
    public Guid Id { get; set; }
    public Guid TypologyId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int Version { get; set; }
    public bool IsCurrent { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation property
    public AgentTypologyEntity Typology { get; set; } = default!;
}
