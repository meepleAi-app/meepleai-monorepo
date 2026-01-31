namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// Persistence entity for AgentConfiguration.
/// Issue #2391 Sprint 2
/// </summary>
public class AgentConfigurationEntity
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public int LlmProvider { get; set; } // 0=OpenRouter, 1=Ollama
    public string LlmModel { get; set; } = string.Empty;
    public int AgentMode { get; set; } // 0=Chat, 1=Player, 2=Ledger
    public string? SelectedDocumentIdsJson { get; set; } // JSON array of GUIDs
    public decimal Temperature { get; set; }
    public int MaxTokens { get; set; }
    public string? SystemPromptOverride { get; set; }
    public bool IsCurrent { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }

    // Navigation property
    public AgentEntity Agent { get; set; } = default!;
}
