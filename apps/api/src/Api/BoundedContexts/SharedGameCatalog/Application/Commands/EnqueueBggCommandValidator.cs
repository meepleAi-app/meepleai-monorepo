using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for EnqueueBggCommand
/// </summary>
internal class EnqueueBggCommandValidator : AbstractValidator<EnqueueBggCommand>
{
    public EnqueueBggCommandValidator()
    {
        RuleFor(x => x.BggId)
            .GreaterThan(0).WithMessage("BGG ID must be a positive integer");

        RuleFor(x => x.GameName)
            .MaximumLength(500).WithMessage("Game name must not exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.GameName));
    }
}
