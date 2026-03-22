using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

internal sealed class ToggleConfigurationCommandValidator : AbstractValidator<ToggleConfigurationCommand>
{
    public ToggleConfigurationCommandValidator()
    {
        RuleFor(x => x.ConfigId)
            .NotEmpty()
            .WithMessage("ConfigId is required");
    }
}
