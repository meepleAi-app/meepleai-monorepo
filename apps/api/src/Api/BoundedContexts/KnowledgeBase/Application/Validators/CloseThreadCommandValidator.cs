using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for CloseThreadCommand.
/// </summary>
internal sealed class CloseThreadCommandValidator : AbstractValidator<CloseThreadCommand>
{
    public CloseThreadCommandValidator()
    {
        RuleFor(x => x.ThreadId)
            .NotEmpty()
            .WithMessage("ThreadId is required");
    }
}
