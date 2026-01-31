using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

internal sealed class DeleteQuickQuestionCommandValidator : AbstractValidator<DeleteQuickQuestionCommand>
{
    public DeleteQuickQuestionCommandValidator()
    {
        RuleFor(x => x.QuestionId)
            .NotEqual(Guid.Empty)
            .WithMessage("QuestionId is required");
    }
}
