using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

internal sealed class ImportGameFromPdfCommandValidator : AbstractValidator<ImportGameFromPdfCommand>
{
    public ImportGameFromPdfCommandValidator()
    {
        RuleFor(x => x.Metadata).NotNull();
        RuleFor(x => x.Metadata.Title)
            .NotEmpty().WithMessage("Game title is required")
            .MaximumLength(300);
        RuleFor(x => x.PdfDocumentId).NotEmpty();
        RuleFor(x => x.RequestedBy).NotEmpty();
        RuleFor(x => x.Metadata.YearPublished)
            .InclusiveBetween(1900, DateTime.UtcNow.Year + 2)
            .When(x => x.Metadata.YearPublished.HasValue);
        RuleFor(x => x.Metadata.MinPlayers)
            .GreaterThan(0).When(x => x.Metadata.MinPlayers.HasValue);
        RuleFor(x => x.Metadata.MaxPlayers)
            .GreaterThanOrEqualTo(x => x.Metadata.MinPlayers ?? 1)
            .When(x => x.Metadata.MaxPlayers.HasValue && x.Metadata.MinPlayers.HasValue);
    }
}
