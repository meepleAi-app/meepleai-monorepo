using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.LiveSessions;

/// <summary>
/// Validators for score-related live session commands.
/// Issue #4749: CQRS validation for live sessions.
/// </summary>
internal sealed class RecordLiveSessionScoreCommandValidator : AbstractValidator<RecordLiveSessionScoreCommand>
{
    public RecordLiveSessionScoreCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("Session ID is required");
        RuleFor(x => x.PlayerId).NotEmpty().WithMessage("Player ID is required");
        RuleFor(x => x.Round).GreaterThan(0).WithMessage("Round must be at least 1");
        RuleFor(x => x.Dimension)
            .NotEmpty()
            .WithMessage("Score dimension is required")
            .MaximumLength(50)
            .WithMessage("Score dimension cannot exceed 50 characters");
        RuleFor(x => x.Unit)
            .MaximumLength(20)
            .When(x => x.Unit != null)
            .WithMessage("Score unit cannot exceed 20 characters");
    }
}

internal sealed class EditLiveSessionScoreCommandValidator : AbstractValidator<EditLiveSessionScoreCommand>
{
    public EditLiveSessionScoreCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("Session ID is required");
        RuleFor(x => x.PlayerId).NotEmpty().WithMessage("Player ID is required");
        RuleFor(x => x.Round).GreaterThan(0).WithMessage("Round must be at least 1");
        RuleFor(x => x.Dimension)
            .NotEmpty()
            .WithMessage("Score dimension is required")
            .MaximumLength(50)
            .WithMessage("Score dimension cannot exceed 50 characters");
        RuleFor(x => x.Unit)
            .MaximumLength(20)
            .When(x => x.Unit != null)
            .WithMessage("Score unit cannot exceed 20 characters");
    }
}

internal sealed class UpdateLiveSessionNotesCommandValidator : AbstractValidator<UpdateLiveSessionNotesCommand>
{
    public UpdateLiveSessionNotesCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("Session ID is required");
        RuleFor(x => x.Notes)
            .MaximumLength(2000)
            .When(x => x.Notes != null)
            .WithMessage("Notes cannot exceed 2000 characters");
    }
}
