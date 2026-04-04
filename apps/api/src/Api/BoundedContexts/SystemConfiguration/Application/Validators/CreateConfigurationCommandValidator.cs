using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

internal sealed class CreateConfigurationCommandValidator : AbstractValidator<CreateConfigurationCommand>
{
    public CreateConfigurationCommandValidator()
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

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description must not exceed 1000 characters")
            .When(x => x.Description is not null);

        RuleFor(x => x.Category)
            .NotEmpty()
            .WithMessage("Category is required")
            .MaximumLength(100)
            .WithMessage("Category must not exceed 100 characters");

        RuleFor(x => x.Environment)
            .NotEmpty()
            .WithMessage("Environment is required")
            .MaximumLength(50)
            .WithMessage("Environment must not exceed 50 characters");

        RuleFor(x => x.CreatedByUserId)
            .NotEmpty()
            .WithMessage("CreatedByUserId is required");
    }
}
