using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to create a new version for an existing prompt template.
/// </summary>
public record CreatePromptVersionCommand(
    Guid TemplateId,
    string Content,
    string? Metadata,
    Guid CreatedByUserId
) : ICommand<PromptVersionDto>;
