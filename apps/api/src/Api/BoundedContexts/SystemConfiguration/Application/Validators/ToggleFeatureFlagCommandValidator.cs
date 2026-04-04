using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

internal sealed class ToggleFeatureFlagCommandValidator : AbstractValidator<ToggleFeatureFlagCommand>
{
    public ToggleFeatureFlagCommandValidator()
    {
        RuleFor(x => x.FlagId)
            .NotEmpty()
            .WithMessage("FlagId is required");
    }
}
