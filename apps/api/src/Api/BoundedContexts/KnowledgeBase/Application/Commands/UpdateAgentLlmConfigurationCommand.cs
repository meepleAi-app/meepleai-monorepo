using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Patches an agent's LLM configuration (model, temperature, maxTokens, documents).
/// Only non-null fields are applied. Handler enforces ownership and tier validation.
/// </summary>
internal record UpdateAgentLlmConfigurationCommand(
    Guid AgentId,
    Guid UserId,
    string UserTier,
    string UserRole,
    string? ModelId,
    decimal? Temperature,
    int? MaxTokens,
    IReadOnlyList<Guid>? SelectedDocumentIds
) : IRequest<AgentConfigurationDto>;
