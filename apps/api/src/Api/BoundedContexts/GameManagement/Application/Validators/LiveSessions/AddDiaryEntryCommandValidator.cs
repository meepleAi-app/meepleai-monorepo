using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.LiveSessions;

/// <summary>
/// Validator for <see cref="AddDiaryEntryCommand"/>.
/// Text cap of 2000 chars mirrors <c>MaxNotesLength</c> in the domain.
/// Issue #2570 SP3 T3.
/// </summary>
internal sealed class AddDiaryEntryCommandValidator : AbstractValidator<AddDiaryEntryCommand>
{
    public AddDiaryEntryCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required");

        RuleFor(x => x.AuthorId)
            .NotEmpty()
            .WithMessage("Author ID is required");

        RuleFor(x => x.Text)
            .NotEmpty()
            .WithMessage("Diary entry text is required")
            .MaximumLength(2000)
            .WithMessage("Diary entry text cannot exceed 2000 characters");
    }
}
