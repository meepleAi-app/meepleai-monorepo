using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.Onboarding;

/// <summary>
/// Validates DismissOnboardingCommand.
/// Ensures user ID is provided.
/// </summary>
internal sealed class DismissOnboardingCommandValidator : AbstractValidator<DismissOnboardingCommand>
{
    public DismissOnboardingCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
    }
}
