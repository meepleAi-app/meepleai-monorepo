using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to list all prompt templates with optional category filter.
/// </summary>
public record ListPromptTemplatesQuery(
    string? Category = null
) : IQuery<PromptTemplateListResponse>;
