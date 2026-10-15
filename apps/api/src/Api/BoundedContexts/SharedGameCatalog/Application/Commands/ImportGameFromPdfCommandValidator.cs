using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

internal sealed class ImportGameFromPdfCommandValidator : AbstractValidator<ImportGameFromPdfCommand>
{
    public ImportGameFromPdfCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.Metadata).NotNull();
        RuleFor(x => x.PdfDocumentId).NotEmpty();
        RuleFor(x => x.RequestedBy).NotEmpty();

        // #3847 — le regole sui campi di Metadata vivono in catene separate da
        // RuleFor(x => x.Metadata).NotNull(), quindi giravano comunque con Metadata assente e
        // sollevavano NullReferenceException invece di segnalare il campo mancante. Il When di
        // gruppo salta l'intero blocco: il messaggio che serve lo da' gia' il NotNull sopra.
        When(x => x.Metadata is not null, () =>
        {
            RuleFor(x => x.Metadata.Title)
                .NotEmpty().WithMessage("Game title is required")
                .MaximumLength(300);
            RuleFor(x => x.Metadata.YearPublished)
                .InclusiveBetween(1900, DateTime.UtcNow.Year + 2)
                .When(x => x.Metadata.YearPublished.HasValue);
            RuleFor(x => x.Metadata.MinPlayers)
                .GreaterThan(0).When(x => x.Metadata.MinPlayers.HasValue);
            // #4401: the game's description is required (domain and update validator); without this
            // rule the wizard learnt it only after the update had been attempted.
            RuleFor(x => x.Metadata.Description)
                .NotEmpty().WithMessage("Game description is required");
            RuleFor(x => x.Metadata.MaxPlayers)
                .GreaterThanOrEqualTo(x => x.Metadata.MinPlayers ?? 1)
                .When(x => x.Metadata.MaxPlayers.HasValue && x.Metadata.MinPlayers.HasValue);
        });
    }
}
