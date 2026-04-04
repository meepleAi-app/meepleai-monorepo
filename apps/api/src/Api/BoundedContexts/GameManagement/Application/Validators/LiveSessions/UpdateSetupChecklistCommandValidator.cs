using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.LiveSessions;

/// <summary>
/// Validator for UpdateSetupChecklistCommand.
/// Ensures SessionId is provided and Checklist is not null.
/// </summary>
internal sealed class UpdateSetupChecklistCommandValidator : AbstractValidator<UpdateSetupChecklistCommand>
{
    public UpdateSetupChecklistCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required");

        RuleFor(x => x.Checklist)
            .NotNull()
            .WithMessage("Checklist data is required");
    }
}
