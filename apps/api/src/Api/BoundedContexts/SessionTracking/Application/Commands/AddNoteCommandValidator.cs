using MediatR;
using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class AddNoteCommandValidator : AbstractValidator<AddNoteCommand>
{
    public AddNoteCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("SessionId is required");

        RuleFor(x => x.ParticipantId)
            .NotEmpty()
            .WithMessage("ParticipantId is required");

        RuleFor(x => x.NoteType)
            .NotEmpty()
            .Must(t => string.Equals(t, "Private", StringComparison.Ordinal) ||
                      string.Equals(t, "Shared", StringComparison.Ordinal) ||
                      string.Equals(t, "Template", StringComparison.Ordinal))
            .WithMessage("NoteType must be 'Private', 'Shared', or 'Template'");

        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("Content is required")
            .MaximumLength(5000)
            .WithMessage("Content must not exceed 5000 characters");

        RuleFor(x => x.TemplateKey)
            .NotEmpty()
            .When(x => string.Equals(x.NoteType, "Template", StringComparison.Ordinal))
            .WithMessage("TemplateKey required for Template notes");
    }
}
