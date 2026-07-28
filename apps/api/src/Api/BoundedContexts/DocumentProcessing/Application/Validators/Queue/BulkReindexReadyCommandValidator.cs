using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators.Queue;

/// <summary>
/// Validator for BulkReindexReadyCommand. Issue #3269: an invalid TargetVersion must 422 up
/// front (before the selector + fan-out run) instead of leaking out of the loop as a
/// ValidationException from the first ReindexDocumentCommand and aborting the whole batch.
/// The TargetVersion rules mirror <c>ReindexDocumentCommandValidator</c>.
/// </summary>
internal sealed class BulkReindexReadyCommandValidator : AbstractValidator<BulkReindexReadyCommand>
{
    public BulkReindexReadyCommandValidator()
    {
        RuleFor(x => x.RequestedBy)
            .NotEmpty()
            .WithMessage("RequestedBy is required.");

        RuleFor(x => x.TargetVersion)
            .Cascade(CascadeMode.Stop)
            .Must(BeKnownIfProvided)
            .WithMessage(c => $"Unknown indexer version '{c.TargetVersion}'.")
            .Must(BeSelectableIfProvided)
            .WithMessage(c => $"Indexer version '{c.TargetVersion}' is not selectable (legacy marker).");
    }

    private static bool BeKnownIfProvided(string? version) =>
        version is null || IndexerVersionRegistry.TryGet(version, out _);

    private static bool BeSelectableIfProvided(string? version) =>
        version is null || IndexerVersionRegistry.IsSelectable(version);
}
