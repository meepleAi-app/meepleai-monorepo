using Api.BoundedContexts.Administration.Application.Commands.GameWizard;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for RunAgentAutoTestCommand.
/// Ensures game ID and requesting user ID are valid.
/// </summary>
internal sealed class RunAgentAutoTestCommandValidator : AbstractValidator<RunAgentAutoTestCommand>
{
    public RunAgentAutoTestCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");

        RuleFor(x => x.RequestedByUserId)
            .NotEmpty()
            .WithMessage("RequestedByUserId is required");
    }
}
