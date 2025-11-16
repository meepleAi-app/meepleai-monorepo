using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to retrieve paginated prompt templates with optional category filter.
/// </summary>
public record GetPromptTemplatesQuery(
    int Page = 1,
    int Limit = 50,
    string? Category = null
) : IQuery<(IReadOnlyList<PromptTemplateDto> Templates, int TotalCount)>;
