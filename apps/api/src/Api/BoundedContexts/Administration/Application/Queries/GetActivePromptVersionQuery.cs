using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get the currently active version of a prompt template by template name.
/// </summary>
public record GetActivePromptVersionQuery(
    string TemplateName
) : IQuery<PromptVersionDto?>;
