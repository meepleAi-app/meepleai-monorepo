using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

internal sealed class AddManualQuickQuestionCommandValidator : AbstractValidator<AddManualQuickQuestionCommand>
{
    public AddManualQuickQuestionCommandValidator()
    {
        RuleFor(x => x.SharedGameId)
            .NotEqual(Guid.Empty)
            .WithMessage("SharedGameId is required");

        RuleFor(x => x.Text)
            .NotEmpty()
            .WithMessage("Question text is required")
            .MaximumLength(200)
            .WithMessage("Question text cannot exceed 200 characters");

        RuleFor(x => x.Emoji)
            .NotEmpty()
            .WithMessage("Emoji is required")
            .MaximumLength(2)
            .WithMessage("Emoji cannot exceed 2 characters");

        RuleFor(x => x.Category)
            .IsInEnum()
            .WithMessage("Invalid question category");

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Display order cannot be negative");
    }
}
