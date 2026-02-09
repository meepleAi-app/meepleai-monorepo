using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.PlayRecords;

/// <summary>
/// Validator for CompletePlayRecordCommand.
/// Issue #3889: CQRS validation for completing play records.
/// </summary>
internal sealed class CompletePlayRecordCommandValidator : AbstractValidator<CompletePlayRecordCommand>
{
    public CompletePlayRecordCommandValidator()
    {
        RuleFor(x => x.RecordId)
            .NotEmpty()
            .WithMessage("Record ID is required");

        RuleFor(x => x.ManualDuration)
            .Must(d => d == null || d.Value >= TimeSpan.Zero)
            .WithMessage("Duration cannot be negative")
            .Must(d => d == null || d.Value <= TimeSpan.FromDays(30))
            .WithMessage("Duration cannot exceed 30 days");
    }
}
