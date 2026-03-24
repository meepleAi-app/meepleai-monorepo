using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

internal sealed class ImportConfigsCommandValidator : AbstractValidator<ImportConfigsCommand>
{
    public ImportConfigsCommandValidator()
    {
        RuleFor(x => x.Configurations)
            .NotEmpty()
            .WithMessage("Configurations is required");

        RuleForEach(x => x.Configurations).ChildRules(item =>
        {
            item.RuleFor(i => i.Key)
                .NotEmpty()
                .WithMessage("Configuration Key is required")
                .MaximumLength(500)
                .WithMessage("Configuration Key must not exceed 500 characters");

            item.RuleFor(i => i.Value)
                .NotEmpty()
                .WithMessage("Configuration Value is required")
                .MaximumLength(10000)
                .WithMessage("Configuration Value must not exceed 10000 characters");

            item.RuleFor(i => i.ValueType)
                .NotEmpty()
                .WithMessage("Configuration ValueType is required")
                .MaximumLength(50)
                .WithMessage("Configuration ValueType must not exceed 50 characters");

            item.RuleFor(i => i.Category)
                .NotEmpty()
                .WithMessage("Configuration Category is required")
                .MaximumLength(100)
                .WithMessage("Configuration Category must not exceed 100 characters");

            item.RuleFor(i => i.Environment)
                .NotEmpty()
                .WithMessage("Configuration Environment is required")
                .MaximumLength(50)
                .WithMessage("Configuration Environment must not exceed 50 characters");
        });

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
