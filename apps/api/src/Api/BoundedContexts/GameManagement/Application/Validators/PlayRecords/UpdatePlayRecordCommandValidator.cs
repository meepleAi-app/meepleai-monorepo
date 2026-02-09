using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.PlayRecords;

/// <summary>
/// Validator for UpdatePlayRecordCommand.
/// Issue #3889: CQRS validation for updating play record details.
/// </summary>
internal sealed class UpdatePlayRecordCommandValidator : AbstractValidator<UpdatePlayRecordCommand>
{
    public UpdatePlayRecordCommandValidator()
    {
        RuleFor(x => x.RecordId)
            .NotEmpty()
            .WithMessage("Record ID is required");

        RuleFor(x => x.SessionDate)
            .LessThanOrEqualTo(DateTime.UtcNow)
            .When(x => x.SessionDate.HasValue)
            .WithMessage("Session date cannot be in the future");

        RuleFor(x => x.Notes)
            .MaximumLength(2000)
            .When(x => x.Notes != null)
            .WithMessage("Notes cannot exceed 2000 characters");

        RuleFor(x => x.Location)
            .MaximumLength(255)
            .When(x => x.Location != null)
            .WithMessage("Location cannot exceed 255 characters");
    }
}
