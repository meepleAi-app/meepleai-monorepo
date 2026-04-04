using Api.BoundedContexts.GameManagement.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators;

/// <summary>
/// Validator for PauseGameSessionCommand.
/// Ensures SessionId is a non-empty GUID.
/// </summary>
internal sealed class PauseGameSessionCommandValidator : AbstractValidator<PauseGameSessionCommand>
{
    public PauseGameSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session ID is required");
    }
}

/// <summary>
/// Validator for ResumeGameSessionCommand.
/// Ensures SessionId is a non-empty GUID.
/// </summary>
internal sealed class ResumeGameSessionCommandValidator : AbstractValidator<ResumeGameSessionCommand>
{
    public ResumeGameSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session ID is required");
    }
}

/// <summary>
/// Validator for DeleteSessionAttachmentCommand.
/// Ensures all GUID properties are non-empty.
/// </summary>
internal sealed class DeleteSessionAttachmentCommandValidator : AbstractValidator<DeleteSessionAttachmentCommand>
{
    public DeleteSessionAttachmentCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session ID is required");

        RuleFor(x => x.AttachmentId)
            .NotEmpty().WithMessage("Attachment ID is required");

        RuleFor(x => x.RequestingPlayerId)
            .NotEmpty().WithMessage("Requesting player ID is required");
    }
}
