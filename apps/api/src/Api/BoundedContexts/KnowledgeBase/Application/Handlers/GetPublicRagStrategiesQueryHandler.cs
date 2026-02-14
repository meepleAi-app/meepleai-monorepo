using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetPublicRagStrategiesQuery.
/// Issue #8: Returns all RAG strategies from enum for user/editor selection.
/// Maps RagStrategy enum to public DTO with descriptions and metadata.
/// </summary>
internal class GetPublicRagStrategiesQueryHandler
    : IRequestHandler<GetPublicRagStrategiesQuery, List<RagStrategyDto>>
{
    public Task<List<RagStrategyDto>> Handle(
        GetPublicRagStrategiesQuery request,
        CancellationToken cancellationToken)
    {
        var strategies = Enum.GetValues<RagStrategy>()
            .Select(MapToDto)
            .OrderBy(s => s.Complexity)
            .ToList();

        return Task.FromResult(strategies);
    }

    private static RagStrategyDto MapToDto(RagStrategy strategy)
    {
        return strategy switch
        {
            RagStrategy.Fast => new RagStrategyDto
            {
                Name = "Fast",
                DisplayName = strategy.GetDisplayName(),
                Description = "Fast, simple queries with minimal processing",
                Complexity = strategy.GetComplexityLevel(),
                EstimatedTokens = 1500,
                RequiresAdmin = strategy.RequiresAdmin(),
                UseCase = "Quick lookups, simple Q&A"
            },

            RagStrategy.Balanced => new RagStrategyDto
            {
                Name = "Balanced",
                DisplayName = strategy.GetDisplayName(),
                Description = "Balanced approach with CRAG evaluation",
                Complexity = strategy.GetComplexityLevel(),
                EstimatedTokens = 2800,
                RequiresAdmin = strategy.RequiresAdmin(),
                UseCase = "Standard gameplay questions"
            },

            RagStrategy.Precise => new RagStrategyDto
            {
                Name = "Precise",
                DisplayName = strategy.GetDisplayName(),
                Description = "High-precision multi-agent validation",
                Complexity = strategy.GetComplexityLevel(),
                EstimatedTokens = 22400,
                RequiresAdmin = strategy.RequiresAdmin(),
                UseCase = "Complex rules interpretation"
            },

            RagStrategy.Expert => new RagStrategyDto
            {
                Name = "Expert",
                DisplayName = strategy.GetDisplayName(),
                Description = "Expert mode with web search and multi-hop reasoning",
                Complexity = strategy.GetComplexityLevel(),
                EstimatedTokens = 8500,
                RequiresAdmin = strategy.RequiresAdmin(),
                UseCase = "Research, obscure rules, clarifications"
            },

            RagStrategy.Consensus => new RagStrategyDto
            {
                Name = "Consensus",
                DisplayName = strategy.GetDisplayName(),
                Description = "Multi-model consensus voting",
                Complexity = strategy.GetComplexityLevel(),
                EstimatedTokens = 15000,
                RequiresAdmin = strategy.RequiresAdmin(),
                UseCase = "Critical decisions, disputed interpretations"
            },

            RagStrategy.SentenceWindow => new RagStrategyDto
            {
                Name = "SentenceWindow",
                DisplayName = strategy.GetDisplayName(),
                Description = "Sentence Window strategy with overlapping document windows (+7% accuracy)",
                Complexity = strategy.GetComplexityLevel(),
                EstimatedTokens = 3250,
                RequiresAdmin = strategy.RequiresAdmin(),
                UseCase = "Precise rule citations, context-aware answers"
            },

            RagStrategy.Iterative => new RagStrategyDto
            {
                Name = "Iterative",
                DisplayName = strategy.GetDisplayName(),
                Description = "Iterative retrieval with refinement rounds (+14% accuracy)",
                Complexity = strategy.GetComplexityLevel(),
                EstimatedTokens = 4500,
                RequiresAdmin = strategy.RequiresAdmin(),
                UseCase = "Complex multi-hop questions, deep research"
            },

            RagStrategy.Custom => new RagStrategyDto
            {
                Name = "Custom",
                DisplayName = strategy.GetDisplayName(),
                Description = "Admin-defined custom strategy (configurable phases)",
                Complexity = strategy.GetComplexityLevel(),
                EstimatedTokens = 0, // Variable
                RequiresAdmin = strategy.RequiresAdmin(),
                UseCase = "Specialized workflows, testing (Admin only)"
            },

            RagStrategy.MultiAgent => new RagStrategyDto
            {
                Name = "MultiAgent",
                DisplayName = strategy.GetDisplayName(),
                Description = "Multi-Agent RAG with specialized agents (+20% accuracy)",
                Complexity = strategy.GetComplexityLevel(),
                EstimatedTokens = 12900,
                RequiresAdmin = strategy.RequiresAdmin(),
                UseCase = "Complex strategic queries, multi-perspective analysis"
            },

            RagStrategy.StepBack => new RagStrategyDto
            {
                Name = "StepBack",
                DisplayName = strategy.GetDisplayName(),
                Description = "Step-Back Prompting for higher-level abstraction (+10% accuracy)",
                Complexity = strategy.GetComplexityLevel(),
                EstimatedTokens = 2800,
                RequiresAdmin = strategy.RequiresAdmin(),
                UseCase = "Complex questions benefiting from broader conceptual context"
            },

            RagStrategy.QueryExpansion => new RagStrategyDto
            {
                Name = "QueryExpansion",
                DisplayName = strategy.GetDisplayName(),
                Description = "Query Expansion with synonyms and related terms (+7% accuracy)",
                Complexity = strategy.GetComplexityLevel(),
                EstimatedTokens = 2400,
                RequiresAdmin = strategy.RequiresAdmin(),
                UseCase = "Broad searches, different terminology, improved recall"
            },

            RagStrategy.RagFusion => new RagStrategyDto
            {
                Name = "RagFusion",
                DisplayName = strategy.GetDisplayName(),
                Description = "RAG-Fusion with reciprocal rank fusion (+11% accuracy)",
                Complexity = strategy.GetComplexityLevel(),
                EstimatedTokens = 11550,
                RequiresAdmin = strategy.RequiresAdmin(),
                UseCase = "Complex queries benefiting from multiple perspectives"
            },

            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown RAG strategy")
        };
    }
}
