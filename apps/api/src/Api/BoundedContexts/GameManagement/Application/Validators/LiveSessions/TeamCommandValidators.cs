using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.LiveSessions;

/// <summary>
/// Validators for team-related live session commands.
/// Issue #4749: CQRS validation for live sessions.
/// </summary>
internal sealed class CreateLiveSessionTeamCommandValidator : AbstractValidator<CreateLiveSessionTeamCommand>
{
    public CreateLiveSessionTeamCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("Session ID is required");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Team name is required")
            .MaximumLength(50)
            .WithMessage("Team name cannot exceed 50 characters");

        RuleFor(x => x.Color)
            .NotEmpty()
            .WithMessage("Team color is required")
            .MaximumLength(7)
            .WithMessage("Team color cannot exceed 7 characters");
    }
}

internal sealed class AssignPlayerToTeamCommandValidator : AbstractValidator<AssignPlayerToTeamCommand>
{
    public AssignPlayerToTeamCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("Session ID is required");
        RuleFor(x => x.PlayerId).NotEmpty().WithMessage("Player ID is required");
        RuleFor(x => x.TeamId).NotEmpty().WithMessage("Team ID is required");
    }
}
