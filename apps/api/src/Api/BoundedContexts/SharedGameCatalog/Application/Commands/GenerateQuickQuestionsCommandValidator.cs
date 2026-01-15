using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

internal sealed class GenerateQuickQuestionsCommandValidator : AbstractValidator<GenerateQuickQuestionsCommand>
{
    public GenerateQuickQuestionsCommandValidator()
    {
        RuleFor(x => x.SharedGameId)
            .NotEqual(Guid.Empty)
            .WithMessage("SharedGameId is required");
    }
}
