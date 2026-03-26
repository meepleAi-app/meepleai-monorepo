using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.LiveSessions;

/// <summary>
/// Validator for GenerateSetupChecklistCommand.
/// Ensures SessionId is provided and PlayerCount is within valid range.
/// </summary>
internal sealed class GenerateSetupChecklistCommandValidator : AbstractValidator<GenerateSetupChecklistCommand>
{
    public GenerateSetupChecklistCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required");

        RuleFor(x => x.PlayerCount)
            .GreaterThan(0)
            .WithMessage("Player count must be greater than 0")
            .LessThanOrEqualTo(20)
            .WithMessage("Player count cannot exceed 20");
    }
}
