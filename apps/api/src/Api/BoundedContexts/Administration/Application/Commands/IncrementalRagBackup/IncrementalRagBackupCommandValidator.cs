using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands.IncrementalRagBackup;

/// <summary>
/// Validator for <see cref="IncrementalRagBackupCommand"/>.
/// Ensures PdfDocumentId is a valid non-empty GUID.
/// </summary>
internal sealed class IncrementalRagBackupCommandValidator : AbstractValidator<IncrementalRagBackupCommand>
{
    public IncrementalRagBackupCommandValidator()
    {
        RuleFor(x => x.PdfDocumentId)
            .NotEmpty()
            .WithMessage("PdfDocumentId must not be empty.");
    }
}
