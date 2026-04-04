using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.Onboarding;

/// <summary>
/// Validates MarkOnboardingWizardSeenCommand.
/// Ensures user ID is provided.
/// </summary>
internal sealed class MarkOnboardingWizardSeenCommandValidator : AbstractValidator<MarkOnboardingWizardSeenCommand>
{
    public MarkOnboardingWizardSeenCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
    }
}
