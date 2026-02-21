using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Command for any participant (including spectators) to send a chat message.
/// Spectators have view-only session access but can still participate in chat.
/// Issue #4765 - Player Action Endpoints + Host Validation
/// </summary>
public record SendChatActionCommand(
    Guid SessionId,
    Guid SenderId,
    Guid RequesterId,
    string Content,
    int? TurnNumber = null,
    string? MentionsJson = null
) : IRequest<SendChatActionResult>, IRequireSessionRole
{
    /// <summary>Minimum role: Spectator (all participants can chat).</summary>
    public ParticipantRole MinimumRole => ParticipantRole.Spectator;
}

/// <summary>
/// Result of a chat action.
/// </summary>
public record SendChatActionResult(
    Guid MessageId,
    int SequenceNumber
);

/// <summary>
/// FluentValidation validator for SendChatActionCommand.
/// </summary>
public class SendChatActionCommandValidator : AbstractValidator<SendChatActionCommand>
{
    public SendChatActionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("SessionId is required");

        RuleFor(x => x.SenderId)
            .NotEmpty()
            .WithMessage("SenderId is required");

        RuleFor(x => x.RequesterId)
            .NotEmpty()
            .WithMessage("RequesterId is required");

        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("Chat message content is required")
            .MaximumLength(1000)
            .WithMessage("Chat message must be 1000 characters or fewer");
    }
}
