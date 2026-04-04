using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for ParseAndRecordScoreCommand.
/// </summary>
internal sealed class ParseAndRecordScoreCommandValidator : AbstractValidator<ParseAndRecordScoreCommand>
{
    public ParseAndRecordScoreCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required");

        RuleFor(x => x.Message)
            .NotEmpty()
            .WithMessage("Message is required")
            .MaximumLength(500)
            .WithMessage("Message must not exceed 500 characters");
    }
}
