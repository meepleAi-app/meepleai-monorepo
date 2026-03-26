using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to update (replace-whole) the setup checklist on a live session.
/// Used when the user toggles components or completes steps in the setup wizard.
/// </summary>
internal record UpdateSetupChecklistCommand(
    Guid SessionId,
    SetupChecklistData Checklist
) : ICommand;
