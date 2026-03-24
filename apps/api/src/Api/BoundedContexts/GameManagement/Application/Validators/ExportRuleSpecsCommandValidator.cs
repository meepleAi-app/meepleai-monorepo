using Api.BoundedContexts.GameManagement.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators;

/// <summary>
/// Validator for ExportRuleSpecsCommand.
/// Ensures GameIds list is provided and within bounds.
/// </summary>
internal sealed class ExportRuleSpecsCommandValidator : AbstractValidator<ExportRuleSpecsCommand>
{
    public ExportRuleSpecsCommandValidator()
    {
        RuleFor(x => x.GameIds)
            .NotEmpty().WithMessage("At least one game ID is required")
            .Must(ids => ids.Count <= 100).WithMessage("Game IDs list must not exceed 100 entries")
            .When(x => x.GameIds is not null);
    }
}
