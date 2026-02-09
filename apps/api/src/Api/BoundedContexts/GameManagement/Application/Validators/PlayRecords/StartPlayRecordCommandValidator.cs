using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.PlayRecords;

/// <summary>
/// Validator for StartPlayRecordCommand.
/// Issue #3889: CQRS validation for starting play records.
/// </summary>
internal sealed class StartPlayRecordCommandValidator : AbstractValidator<StartPlayRecordCommand>
{
    public StartPlayRecordCommandValidator()
    {
        RuleFor(x => x.RecordId)
            .NotEmpty()
            .WithMessage("Record ID is required");
    }
}
