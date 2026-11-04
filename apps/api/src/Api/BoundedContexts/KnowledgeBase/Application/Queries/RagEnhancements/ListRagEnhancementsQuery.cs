using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.RagEnhancements;

/// <summary>Lists every RAG enhancement with its global state, in the order of the <c>RagEnhancement</c> enum.</summary>
internal sealed record ListRagEnhancementsQuery : IQuery<IReadOnlyList<RagEnhancementListItemDto>>;
