using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for DeleteAgentTypologyCommand.
/// </summary>
internal sealed class DeleteAgentTypologyCommandValidator : AbstractValidator<DeleteAgentTypologyCommand>
{
    public DeleteAgentTypologyCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required");
    }
}
