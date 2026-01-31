using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to retrieve a single prompt template by ID.
/// </summary>
internal record GetPromptTemplateByIdQuery(
    Guid TemplateId
) : IQuery<PromptTemplateDto?>;
