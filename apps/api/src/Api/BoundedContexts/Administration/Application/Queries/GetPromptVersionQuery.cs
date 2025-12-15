using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get a specific version of a prompt template by template ID and version number.
/// </summary>
internal record GetPromptVersionQuery(
    string TemplateId,
    int VersionNumber
) : IQuery<PromptVersionDto?>;
