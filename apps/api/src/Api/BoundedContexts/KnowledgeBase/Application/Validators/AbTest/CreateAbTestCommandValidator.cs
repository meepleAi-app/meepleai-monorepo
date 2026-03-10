using Api.BoundedContexts.KnowledgeBase.Application.Commands.AbTest;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators.AbTest;

/// <summary>
/// Validator for CreateAbTestCommand.
/// Issue #5494: A/B Test CQRS commands and queries.
/// </summary>
internal sealed class CreateAbTestCommandValidator : AbstractValidator<CreateAbTestCommand>
{
    public CreateAbTestCommandValidator()
    {
        RuleFor(x => x.CreatedBy)
            .NotEmpty().WithMessage("CreatedBy is required");

        RuleFor(x => x.Query)
            .NotEmpty().WithMessage("Query is required")
            .MaximumLength(2000).WithMessage("Query must not exceed 2000 characters");

        RuleFor(x => x.ModelIds)
            .NotEmpty().WithMessage("At least one model is required")
            .Must(m => m.Count >= 2).WithMessage("At least 2 models are required for comparison")
            .Must(m => m.Count <= 4).WithMessage("Maximum 4 models per test session");

        RuleForEach(x => x.ModelIds)
            .NotEmpty().WithMessage("ModelId cannot be empty")
            .MaximumLength(200).WithMessage("ModelId must not exceed 200 characters");

        RuleFor(x => x.ModelIds)
            .Must(m => m.Distinct(StringComparer.OrdinalIgnoreCase).Count() == m.Count)
            .WithMessage("Duplicate model IDs are not allowed")
            .When(x => x.ModelIds is { Count: > 0 });
    }
}
