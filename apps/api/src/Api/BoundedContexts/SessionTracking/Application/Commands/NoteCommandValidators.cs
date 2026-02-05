using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Validator for SaveNoteCommand.
/// </summary>
public class SaveNoteCommandValidator : AbstractValidator<SaveNoteCommand>
{
    public SaveNoteCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required.");

        RuleFor(x => x.ParticipantId)
            .NotEmpty()
            .WithMessage("Participant ID is required.");

        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("Note content is required.")
            .MaximumLength(10000)
            .WithMessage("Note content cannot exceed 10,000 characters.");

        RuleFor(x => x.ObscuredText)
            .MaximumLength(500)
            .WithMessage("Obscured text cannot exceed 500 characters.")
            .When(x => x.ObscuredText is not null);
    }
}

/// <summary>
/// Validator for RevealNoteCommand.
/// </summary>
public class RevealNoteCommandValidator : AbstractValidator<RevealNoteCommand>
{
    public RevealNoteCommandValidator()
    {
        RuleFor(x => x.NoteId)
            .NotEmpty()
            .WithMessage("Note ID is required.");

        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required.");

        RuleFor(x => x.ParticipantId)
            .NotEmpty()
            .WithMessage("Participant ID is required.");
    }
}

/// <summary>
/// Validator for HideNoteCommand.
/// </summary>
public class HideNoteCommandValidator : AbstractValidator<HideNoteCommand>
{
    public HideNoteCommandValidator()
    {
        RuleFor(x => x.NoteId)
            .NotEmpty()
            .WithMessage("Note ID is required.");

        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required.");

        RuleFor(x => x.ParticipantId)
            .NotEmpty()
            .WithMessage("Participant ID is required.");
    }
}

/// <summary>
/// Validator for DeleteNoteCommand.
/// </summary>
public class DeleteNoteCommandValidator : AbstractValidator<DeleteNoteCommand>
{
    public DeleteNoteCommandValidator()
    {
        RuleFor(x => x.NoteId)
            .NotEmpty()
            .WithMessage("Note ID is required.");

        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required.");

        RuleFor(x => x.ParticipantId)
            .NotEmpty()
            .WithMessage("Participant ID is required.");
    }
}
