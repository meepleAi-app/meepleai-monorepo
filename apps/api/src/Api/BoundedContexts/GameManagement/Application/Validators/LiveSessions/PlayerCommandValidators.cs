using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.LiveSessions;

/// <summary>
/// Validators for player-related live session commands.
/// Issue #4749: CQRS validation for live sessions.
/// </summary>
internal sealed class AddPlayerToLiveSessionCommandValidator : AbstractValidator<AddPlayerToLiveSessionCommand>
{
    public AddPlayerToLiveSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required");

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .WithMessage("Display name is required")
            .MaximumLength(100)
            .WithMessage("Display name cannot exceed 100 characters");

        RuleFor(x => x.Color)
            .IsInEnum()
            .WithMessage("Invalid player color");

        RuleFor(x => x.Role)
            .IsInEnum()
            .When(x => x.Role.HasValue)
            .WithMessage("Invalid player role");

        RuleFor(x => x.AvatarUrl)
            .MaximumLength(500)
            .When(x => x.AvatarUrl != null)
            .WithMessage("Avatar URL cannot exceed 500 characters");
    }
}

internal sealed class RemovePlayerFromLiveSessionCommandValidator : AbstractValidator<RemovePlayerFromLiveSessionCommand>
{
    public RemovePlayerFromLiveSessionCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("Session ID is required");
        RuleFor(x => x.PlayerId).NotEmpty().WithMessage("Player ID is required");
    }
}

internal sealed class UpdatePlayerOrderCommandValidator : AbstractValidator<UpdatePlayerOrderCommand>
{
    public UpdatePlayerOrderCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("Session ID is required");
        RuleFor(x => x.PlayerIds)
            .NotNull()
            .WithMessage("Player IDs list is required")
            .Must(ids => ids != null && ids.Count > 0)
            .WithMessage("At least one player ID is required");
    }
}
