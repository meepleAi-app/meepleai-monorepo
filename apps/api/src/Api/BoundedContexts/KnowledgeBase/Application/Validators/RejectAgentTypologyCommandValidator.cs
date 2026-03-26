using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for RejectAgentTypologyCommand.
/// </summary>
internal sealed class RejectAgentTypologyCommandValidator : AbstractValidator<RejectAgentTypologyCommand>
{
    public RejectAgentTypologyCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required");

        RuleFor(x => x.RejectedBy)
            .NotEmpty()
            .WithMessage("RejectedBy is required");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Reason is required")
            .MaximumLength(500)
            .WithMessage("Reason cannot exceed 500 characters");
    }
}
