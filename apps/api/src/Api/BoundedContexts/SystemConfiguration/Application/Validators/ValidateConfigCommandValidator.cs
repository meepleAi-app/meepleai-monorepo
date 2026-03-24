using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

internal sealed class ValidateConfigCommandValidator : AbstractValidator<ValidateConfigCommand>
{
    public ValidateConfigCommandValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty()
            .WithMessage("Key is required")
            .MaximumLength(500)
            .WithMessage("Key must not exceed 500 characters");

        RuleFor(x => x.Value)
            .NotEmpty()
            .WithMessage("Value is required")
            .MaximumLength(10000)
            .WithMessage("Value must not exceed 10000 characters");

        RuleFor(x => x.ValueType)
            .NotEmpty()
            .WithMessage("ValueType is required")
            .MaximumLength(50)
            .WithMessage("ValueType must not exceed 50 characters");
    }
}
