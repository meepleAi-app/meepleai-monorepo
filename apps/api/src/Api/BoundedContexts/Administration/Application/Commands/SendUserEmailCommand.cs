using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to send custom email to user (Issue #2890).
/// Admin quick action for user communication.
/// </summary>
internal record SendUserEmailCommand(
    Guid UserId,
    string Subject,
    string Body,
    Guid RequesterId
) : ICommand;
