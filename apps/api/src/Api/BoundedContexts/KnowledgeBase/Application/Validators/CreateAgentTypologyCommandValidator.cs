using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for CreateAgentTypologyCommand.
/// Issue #3176: AGT-002 Typology CRUD Commands.
/// </summary>
internal sealed class CreateAgentTypologyCommandValidator : AbstractValidator<CreateAgentTypologyCommand>
{
    private const int NameMinLength = 3;
    private const int NameMaxLength = 100;
    private const int BasePromptMaxLength = 5000;

    public CreateAgentTypologyCommandValidator(IAgentTypologyRepository repository)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MinimumLength(NameMinLength).WithMessage($"Name must be at least {NameMinLength} characters")
            .MaximumLength(NameMaxLength).WithMessage($"Name cannot exceed {NameMaxLength} characters")
            .MustAsync(async (name, cancellationToken) =>
                !await repository.ExistsAsync(name, cancellationToken).ConfigureAwait(false))
            .WithMessage("A typology with this name already exists");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required");

        RuleFor(x => x.BasePrompt)
            .NotEmpty().WithMessage("BasePrompt is required")
            .MaximumLength(BasePromptMaxLength).WithMessage($"BasePrompt cannot exceed {BasePromptMaxLength} characters");

        RuleFor(x => x.DefaultStrategyName)
            .NotEmpty().WithMessage("DefaultStrategyName is required");

        RuleFor(x => x.DefaultStrategyParameters)
            .NotNull().WithMessage("DefaultStrategyParameters cannot be null");

        RuleFor(x => x.CreatedBy)
            .NotEqual(Guid.Empty).WithMessage("CreatedBy is required");
    }
}
