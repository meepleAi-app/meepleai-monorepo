using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.RagPipeline;

/// <summary>
/// Query to get a single RAG pipeline strategy by ID.
/// Issue #3464: Save/load/export for custom strategies.
/// </summary>
public sealed record GetRagPipelineStrategyByIdQuery(
    Guid Id,
    Guid UserId
) : IRequest<RagPipelineStrategyDetailDto?>;

/// <summary>
/// Detailed DTO for RAG pipeline strategy including full pipeline definition.
/// </summary>
public sealed record RagPipelineStrategyDetailDto(
    Guid Id,
    string Name,
    string Description,
    string Version,
    string NodesJson,
    string EdgesJson,
    bool IsTemplate,
    string? TemplateCategory,
    IReadOnlyList<string> Tags,
    bool IsActive,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
