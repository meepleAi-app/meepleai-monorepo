using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to create a new prompt template with an initial version.
/// </summary>
public record CreatePromptTemplateCommand(
    string Name,
    string? Description,
    string? Category,
    string InitialContent,
    string? Metadata,
    Guid CreatedByUserId
) : ICommand<PromptTemplateDto>;
