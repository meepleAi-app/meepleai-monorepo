using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Patches the LLM configuration of an <see cref="Domain.Entities.AgentDefinition"/>.
/// Issue #658 (Phase δ): exposes the partial-update contract consumed by the frontend
/// <c>agentsClient.updateAgentConfiguration</c> helper at
/// <c>PATCH /api/v1/agents/{id}/configuration</c>.
/// </summary>
/// <remarks>
/// Only non-null fields are applied; missing fields preserve the current value (mirror
/// of the <c>UpdateUserAgentCommand</c> partial-update contract from PR #695).
/// SelectedDocumentIds is accepted on the wire to align with the frontend request shape but
/// not yet persisted (KB linking deferred to a follow-up — same MVP scope as PR #695).
/// Returns <c>null</c> when the agent ID is not found (endpoint maps to 404).
/// </remarks>
internal sealed record UpdateAgentConfigurationCommand(
    Guid AgentId,
    string? ModelId = null,
    decimal? Temperature = null,
    int? MaxTokens = null,
    IReadOnlyList<Guid>? SelectedDocumentIds = null
) : IRequest<AgentConfigurationDto?>;
