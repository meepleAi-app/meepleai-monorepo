using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to create a new prompt template.
/// </summary>
public record CreatePromptTemplateCommand(
    string Name,
    string? Description,
    string? Category,
    Guid CreatedByUserId
) : ICommand<PromptTemplateDto>;
