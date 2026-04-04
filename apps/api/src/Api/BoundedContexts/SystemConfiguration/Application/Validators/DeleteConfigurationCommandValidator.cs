using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

internal sealed class DeleteConfigurationCommandValidator : AbstractValidator<DeleteConfigurationCommand>
{
    public DeleteConfigurationCommandValidator()
    {
        RuleFor(x => x.ConfigId)
            .NotEmpty()
            .WithMessage("ConfigId is required");
    }
}
