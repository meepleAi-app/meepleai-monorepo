using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for ApproveAgentTypologyCommand.
/// </summary>
internal sealed class ApproveAgentTypologyCommandValidator : AbstractValidator<ApproveAgentTypologyCommand>
{
    public ApproveAgentTypologyCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required");

        RuleFor(x => x.ApprovedBy)
            .NotEmpty()
            .WithMessage("ApprovedBy is required");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .When(x => x.Notes != null)
            .WithMessage("Notes cannot exceed 500 characters");
    }
}
