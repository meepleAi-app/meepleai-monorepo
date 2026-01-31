using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for UpdateGameFaqCommand.
/// </summary>
internal sealed class UpdateGameFaqCommandValidator : AbstractValidator<UpdateGameFaqCommand>
{
    public UpdateGameFaqCommandValidator()
    {
        RuleFor(x => x.FaqId)
            .NotEqual(Guid.Empty).WithMessage("FaqId is required");

        RuleFor(x => x.Question)
            .NotEmpty().WithMessage("Question is required")
            .MaximumLength(500).WithMessage("Question cannot exceed 500 characters");

        RuleFor(x => x.Answer)
            .NotEmpty().WithMessage("Answer is required");

        RuleFor(x => x.Order)
            .GreaterThanOrEqualTo(0).WithMessage("Order cannot be negative");
    }
}
