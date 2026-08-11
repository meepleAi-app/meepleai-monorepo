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
/// Scope: this command owns ONLY the LLM configuration (model/temperature/maxTokens).
/// Per-agent KB linking (selected documents) is a separate concern owned by the
/// <c>AgentConfiguration</c> aggregate (#2391) and its <c>updateSelectedDocuments</c> endpoint;
/// it is intentionally NOT part of this contract. Issue #3394 removed the previously
/// accepted-and-discarded <c>SelectedDocumentIds</c> field to eliminate the silent no-op.
/// Returns <c>null</c> when the agent ID is not found (endpoint maps to 404).
/// </remarks>
internal sealed record UpdateAgentConfigurationCommand(
    Guid AgentId,
    string? ModelId = null,
    decimal? Temperature = null,
    int? MaxTokens = null
) : IRequest<AgentConfigurationDto?>;
