using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to retrieve all versions for a prompt template.
/// </summary>
public record GetPromptVersionsQuery(
    Guid TemplateId
) : IQuery<IReadOnlyList<PromptVersionDto>>;
