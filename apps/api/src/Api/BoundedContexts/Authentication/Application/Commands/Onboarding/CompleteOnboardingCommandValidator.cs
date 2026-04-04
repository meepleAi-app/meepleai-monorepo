using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.Onboarding;

/// <summary>
/// Validates CompleteOnboardingCommand.
/// Issue #323: Onboarding completion tracking.
/// </summary>
internal sealed class CompleteOnboardingCommandValidator : AbstractValidator<CompleteOnboardingCommand>
{
    public CompleteOnboardingCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
