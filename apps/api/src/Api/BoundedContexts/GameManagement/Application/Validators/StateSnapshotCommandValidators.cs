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

/// <summary>
/// Validator for CreateStateSnapshotCommand.
/// Ensures SessionStateId is non-empty, TurnNumber is valid, and Description is provided.
/// </summary>
internal sealed class CreateStateSnapshotCommandValidator : AbstractValidator<CreateStateSnapshotCommand>
{
    public CreateStateSnapshotCommandValidator()
    {
        RuleFor(x => x.SessionStateId)
            .NotEmpty().WithMessage("Session state ID is required");

        RuleFor(x => x.TurnNumber)
            .GreaterThanOrEqualTo(0).WithMessage("Turn number must be zero or greater");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required")
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters");
    }
}

/// <summary>
/// Validator for InitializeGameStateCommand.
/// Ensures GameSessionId and TemplateId are non-empty GUIDs.
/// </summary>
internal sealed class InitializeGameStateCommandValidator : AbstractValidator<InitializeGameStateCommand>
{
    public InitializeGameStateCommandValidator()
    {
        RuleFor(x => x.GameSessionId)
            .NotEmpty().WithMessage("Game session ID is required");

        RuleFor(x => x.TemplateId)
            .NotEmpty().WithMessage("Template ID is required");
    }
}

/// <summary>
/// Validator for UpdateGameStateCommand.
/// Ensures SessionStateId is non-empty and NewState is provided.
/// </summary>
internal sealed class UpdateGameStateCommandValidator : AbstractValidator<UpdateGameStateCommand>
{
    public UpdateGameStateCommandValidator()
    {
        RuleFor(x => x.SessionStateId)
            .NotEmpty().WithMessage("Session state ID is required");

        RuleFor(x => x.NewState)
            .NotNull().WithMessage("New state is required");
    }
}
