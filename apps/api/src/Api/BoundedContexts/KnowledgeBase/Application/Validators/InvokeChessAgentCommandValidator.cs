using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for InvokeChessAgentCommand.
/// </summary>
internal sealed class InvokeChessAgentCommandValidator : AbstractValidator<InvokeChessAgentCommand>
{
    public InvokeChessAgentCommandValidator()
    {
        RuleFor(x => x.Question)
            .NotEmpty()
            .WithMessage("Question is required")
            .MaximumLength(2000)
            .WithMessage("Question cannot exceed 2000 characters");

        RuleFor(x => x.FenPosition)
            .MaximumLength(200)
            .When(x => x.FenPosition != null)
            .WithMessage("FenPosition cannot exceed 200 characters");
    }
}
