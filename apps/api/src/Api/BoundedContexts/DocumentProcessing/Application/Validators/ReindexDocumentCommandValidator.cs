using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for ReindexDocumentCommand. Issue #1673: enforces optional IndexerVersion
/// must be a known, selectable version.
/// </summary>
internal sealed class ReindexDocumentCommandValidator : AbstractValidator<ReindexDocumentCommand>
{
    public ReindexDocumentCommandValidator()
    {
        RuleFor(x => x.PdfId)
            .NotEmpty()
            .WithMessage("PDF ID is required.");

        RuleFor(x => x.IndexerVersion)
            .Cascade(CascadeMode.Stop)
            .Must(BeKnownIfProvided)
            .WithMessage(c => $"Unknown indexer version '{c.IndexerVersion}'.")
            .Must(BeSelectableIfProvided)
            .WithMessage(c => $"Indexer version '{c.IndexerVersion}' is not selectable (legacy marker).");
    }

    private static bool BeKnownIfProvided(string? version) =>
        version is null || IndexerVersionRegistry.TryGet(version, out _);

    private static bool BeSelectableIfProvided(string? version) =>
        version is null || IndexerVersionRegistry.IsSelectable(version);
}
