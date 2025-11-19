using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get the version history for a prompt template.
/// </summary>
public record GetPromptVersionHistoryQuery(
    string TemplateId
) : IQuery<PromptVersionHistoryResponse>;
