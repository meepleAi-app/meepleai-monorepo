namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// Persistence entity for AgentTypology.
/// Issue #3175
/// </summary>
public class AgentTypologyEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string BasePrompt { get; set; } = string.Empty;
    public string DefaultStrategyJson { get; set; } = string.Empty; // AgentStrategy serialized as JSON
    public int Status { get; set; } // 0=Draft, 1=Pending, 2=Approved, 3=Rejected
    public Guid CreatedBy { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Navigation properties
    public ICollection<TypologyPromptTemplateEntity> TypologyPromptTemplates { get; set; } = new List<TypologyPromptTemplateEntity>();
}
