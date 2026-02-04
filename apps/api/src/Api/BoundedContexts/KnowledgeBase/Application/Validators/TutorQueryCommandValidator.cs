using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// ISSUE-3499: Validator for TutorQueryCommand.
/// Ensures query is not empty and IDs are valid.
/// </summary>
internal class TutorQueryCommandValidator : AbstractValidator<TutorQueryCommand>
{
    public TutorQueryCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");

        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("SessionId is required");

        RuleFor(x => x.Query)
            .NotEmpty()
            .WithMessage("Query is required")
            .MaximumLength(2000)
            .WithMessage("Query must not exceed 2000 characters");
    }
}
