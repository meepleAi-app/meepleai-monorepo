using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Lists the agent definitions that explicitly consume a given PDF document
/// (i.e. AgentDefinition.KbCardIds contains DocumentId).
/// Returns an empty list when no agent consumes the document.
/// Issue #1651: F3-FU-2 — Used-by tab.
/// </summary>
internal sealed record GetConsumingAgentsByDocumentIdQuery(Guid DocumentId)
    : IQuery<IReadOnlyList<KbDocConsumingAgentDto>>;
