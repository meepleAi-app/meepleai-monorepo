using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for TestAgentTypologyCommand.
/// Issue #3177: AGT-003 Editor Proposal Commands.
/// </summary>
internal sealed class TestAgentTypologyCommandValidator : AbstractValidator<TestAgentTypologyCommand>
{
    public TestAgentTypologyCommandValidator()
    {
        RuleFor(x => x.TypologyId)
            .NotEmpty().WithMessage("Typology ID is required");

        RuleFor(x => x.TestQuery)
            .NotEmpty().WithMessage("Test query is required")
            .MinimumLength(5).WithMessage("Test query must be at least 5 characters")
            .MaximumLength(1000).WithMessage("Test query must not exceed 1000 characters");

        RuleFor(x => x.RequestedBy)
            .NotEmpty().WithMessage("RequestedBy user ID is required");
    }
}
