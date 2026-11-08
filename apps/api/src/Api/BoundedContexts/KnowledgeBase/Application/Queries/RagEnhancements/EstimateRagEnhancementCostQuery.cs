using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.RagEnhancements;

/// <summary>The RAG enhancements active for <paramref name="Tier"/> and their extra cost per query.</summary>
internal sealed record EstimateRagEnhancementCostQuery(UserTier Tier) : IQuery<RagEnhancementCostEstimateDto>;
