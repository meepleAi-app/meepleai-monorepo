using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for ReopenThreadCommand.
/// </summary>
internal sealed class ReopenThreadCommandValidator : AbstractValidator<ReopenThreadCommand>
{
    public ReopenThreadCommandValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty()
            .WithMessage("ThreadId is required");
    }
}
