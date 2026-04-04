using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for AcceptCopyrightDisclaimerCommand.
/// Issue #5446: Copyright disclaimer acceptance required before RAG processing.
/// </summary>
internal sealed class AcceptCopyrightDisclaimerCommandValidator : AbstractValidator<AcceptCopyrightDisclaimerCommand>
{
    public AcceptCopyrightDisclaimerCommandValidator()
    {
        RuleFor(x => x.PdfDocumentId)
            .NotEmpty()
            .WithMessage("PDF document ID is required.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required.");
    }
}
