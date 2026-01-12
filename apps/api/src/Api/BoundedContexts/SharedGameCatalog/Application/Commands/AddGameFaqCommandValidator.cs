using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for AddGameFaqCommand.
/// </summary>
internal sealed class AddGameFaqCommandValidator : AbstractValidator<AddGameFaqCommand>
{
    public AddGameFaqCommandValidator()
    {
        RuleFor(x => x.SharedGameId)
            .NotEqual(Guid.Empty).WithMessage("SharedGameId is required");

        RuleFor(x => x.Question)
            .NotEmpty().WithMessage("Question is required")
            .MaximumLength(500).WithMessage("Question cannot exceed 500 characters");

        RuleFor(x => x.Answer)
            .NotEmpty().WithMessage("Answer is required");

        RuleFor(x => x.Order)
            .GreaterThanOrEqualTo(0).WithMessage("Order cannot be negative");
    }
}
