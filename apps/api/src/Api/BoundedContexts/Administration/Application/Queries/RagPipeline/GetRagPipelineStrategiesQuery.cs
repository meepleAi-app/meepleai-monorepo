using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.RagPipeline;

/// <summary>
/// Query to get RAG pipeline strategies for a user.
/// Issue #3464: Save/load/export for custom strategies.
/// </summary>
public sealed record GetRagPipelineStrategiesQuery(
    Guid UserId,
    string? SearchTerm = null,
    bool IncludeTemplates = true
) : IRequest<IReadOnlyList<RagPipelineStrategyDto>>;

/// <summary>
/// DTO for RAG pipeline strategy list.
/// </summary>
public sealed record RagPipelineStrategyDto(
    Guid Id,
    string Name,
    string Description,
    string Version,
    bool IsTemplate,
    string? TemplateCategory,
    IReadOnlyList<string> Tags,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
