using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

internal sealed class UpdateFeatureFlagCommandValidator : AbstractValidator<UpdateFeatureFlagCommand>
{
    public UpdateFeatureFlagCommandValidator()
    {
        RuleFor(x => x.FeatureName)
            .NotEmpty()
            .WithMessage("FeatureName is required")
            .MaximumLength(200)
            .WithMessage("FeatureName must not exceed 200 characters");

        RuleFor(x => x.Role)
            .IsInEnum()
            .WithMessage("Role must be a valid UserRole value")
            .When(x => x.Role.HasValue);

        RuleFor(x => x.UserId)
            .MaximumLength(200)
            .WithMessage("UserId must not exceed 200 characters")
            .When(x => x.UserId is not null);
    }
}
