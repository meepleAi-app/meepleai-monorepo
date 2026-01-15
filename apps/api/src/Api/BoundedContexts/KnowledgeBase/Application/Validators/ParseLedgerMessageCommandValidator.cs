using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for ParseLedgerMessageCommand.
/// Issue #2405 - Ledger Mode state tracking
/// </summary>
internal sealed class ParseLedgerMessageCommandValidator : AbstractValidator<ParseLedgerMessageCommand>
{
    public ParseLedgerMessageCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required");

        RuleFor(x => x.Message)
            .NotEmpty()
            .WithMessage("Message cannot be empty")
            .MaximumLength(5000)
            .WithMessage("Message cannot exceed 5000 characters");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
    }
}
