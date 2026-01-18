using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for AnalyzeRulebookCommand.
/// Issue #2402: Rulebook Analysis Service
/// </summary>
internal sealed class AnalyzeRulebookCommandValidator : AbstractValidator<AnalyzeRulebookCommand>
{
    public AnalyzeRulebookCommandValidator()
    {
        RuleFor(x => x.PdfDocumentId)
            .NotEmpty()
            .WithMessage("PDF document ID is required");

        RuleFor(x => x.SharedGameId)
            .NotEmpty()
            .WithMessage("Shared game ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
    }
}
