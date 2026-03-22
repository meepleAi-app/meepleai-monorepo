using Api.BoundedContexts.GameManagement.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators;

/// <summary>
/// Validator for RestoreStateSnapshotCommand.
/// Ensures SessionStateId and SnapshotId are non-empty GUIDs.
/// </summary>
internal sealed class RestoreStateSnapshotCommandValidator : AbstractValidator<RestoreStateSnapshotCommand>
{
    public RestoreStateSnapshotCommandValidator()
    {
        RuleFor(x => x.SessionStateId)
            .NotEmpty().WithMessage("Session state ID is required");

        RuleFor(x => x.SnapshotId)
            .NotEmpty().WithMessage("Snapshot ID is required");
    }
}
