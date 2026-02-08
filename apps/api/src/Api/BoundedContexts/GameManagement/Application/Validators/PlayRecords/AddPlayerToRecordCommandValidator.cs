using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.PlayRecords;

/// <summary>
/// Validator for AddPlayerToRecordCommand.
/// Issue #3889: CQRS validation for adding players to play records.
/// </summary>
internal sealed class AddPlayerToRecordCommandValidator : AbstractValidator<AddPlayerToRecordCommand>
{
    public AddPlayerToRecordCommandValidator()
    {
        RuleFor(x => x.RecordId)
            .NotEmpty()
            .WithMessage("Record ID is required");

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .WithMessage("Player display name is required")
            .MaximumLength(255)
            .WithMessage("Display name cannot exceed 255 characters");
    }
}
