using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands.GameWizard;

/// <summary>
/// Validator for CreateGameFromWizardCommand.
/// </summary>
internal sealed class CreateGameFromWizardCommandValidator : AbstractValidator<CreateGameFromWizardCommand>
{
    public CreateGameFromWizardCommandValidator()
    {
        RuleFor(x => x.BggId)
            .GreaterThan(0)
            .WithMessage("A valid BGG ID is required");

        RuleFor(x => x.CreatedByUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("CreatedByUserId is required");
    }
}
