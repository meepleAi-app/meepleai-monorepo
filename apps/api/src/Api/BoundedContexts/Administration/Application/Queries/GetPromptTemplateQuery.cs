using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get a specific prompt template by ID.
/// </summary>
internal record GetPromptTemplateQuery(
    string TemplateId
) : IQuery<PromptTemplateDto?>;
