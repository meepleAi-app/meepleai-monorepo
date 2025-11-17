using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to activate a specific version of a prompt template.
/// Deactivates all other versions of the same template.
/// Creates audit log entries for activation and deactivation.
/// </summary>
public record ActivatePromptVersionCommand(
    Guid TemplateId,
    Guid VersionId,
    Guid ActivatedByUserId,
    string? Reason = null
) : ICommand<PromptVersionDto>;
