using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for CompleteChunkedUploadCommand.
/// Validates GUID properties are non-empty.
/// </summary>
internal sealed class CompleteChunkedUploadCommandValidator : AbstractValidator<CompleteChunkedUploadCommand>
{
    public CompleteChunkedUploadCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required.");
    }
}
