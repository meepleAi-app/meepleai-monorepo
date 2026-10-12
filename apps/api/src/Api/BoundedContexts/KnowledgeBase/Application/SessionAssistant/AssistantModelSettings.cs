using Api.Services;

namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>Modello, riserva e limiti di un turno dell'assistente in partita (spec §3.2).</summary>
internal sealed record AssistantModelSettings(
    string PrimaryModelId,
    string? FallbackModelId,
    double Temperature,
    int MaxTokens,
    int MaxToolIterations,
    Guid? ProfileVersionId,
    RequestSource Source);
