using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.PlayRecords;

/// <summary>
/// Validator for RecordScoreCommand.
/// Issue #3889: CQRS validation for recording scores.
/// </summary>
internal sealed class RecordScoreCommandValidator : AbstractValidator<RecordScoreCommand>
{
    public RecordScoreCommandValidator()
    {
        RuleFor(x => x.RecordId)
            .NotEmpty()
            .WithMessage("Record ID is required");

        RuleFor(x => x.PlayerId)
            .NotEmpty()
            .WithMessage("Player ID is required");

        RuleFor(x => x.Dimension)
            .NotEmpty()
            .WithMessage("Score dimension is required")
            .MaximumLength(50)
            .WithMessage("Dimension cannot exceed 50 characters");

        RuleFor(x => x.Value)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Score value cannot be negative");

        RuleFor(x => x.Unit)
            .MaximumLength(20)
            .When(x => x.Unit != null)
            .WithMessage("Unit cannot exceed 20 characters");
    }
}
