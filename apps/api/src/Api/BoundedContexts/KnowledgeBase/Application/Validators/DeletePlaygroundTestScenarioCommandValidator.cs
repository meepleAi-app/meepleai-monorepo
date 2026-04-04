using Api.BoundedContexts.KnowledgeBase.Application.Commands.PlaygroundTestScenario;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for DeletePlaygroundTestScenarioCommand.
/// </summary>
internal sealed class DeletePlaygroundTestScenarioCommandValidator : AbstractValidator<DeletePlaygroundTestScenarioCommand>
{
    public DeletePlaygroundTestScenarioCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required");
    }
}
