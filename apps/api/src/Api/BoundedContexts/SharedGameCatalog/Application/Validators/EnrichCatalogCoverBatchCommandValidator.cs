using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCoverBatch;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Validators;

/// <summary>
/// Validator for <see cref="EnrichCatalogCoverBatchCommand"/>. Enforces the
/// minimal shape invariants and a defensive cap on batch size. Issue #2123.
/// </summary>
/// <remarks>
/// <para>
/// <c>MaxBatchSize</c>: 200. Rationale: the M8 single-entry handler enforces a
/// 1 req/sec Wikimedia SPARQL cap; at the worst case a single batch therefore
/// takes ~200 seconds (3-4 minutes). Anything larger should be split by the
/// admin to keep a single transaction under a reasonable HTTP timeout, and to
/// keep the per-game error rate visible per batch.
/// </para>
/// <para>
/// Duplicate game IDs are rejected at 400: re-dispatching the same id twice in
/// one batch is almost certainly a bug on the caller side (M8 is idempotent
/// but doing it consecutively wastes Wikimedia quota).
/// </para>
/// </remarks>
internal sealed class EnrichCatalogCoverBatchCommandValidator
    : AbstractValidator<EnrichCatalogCoverBatchCommand>
{
    public const int MaxBatchSize = 200;

    public EnrichCatalogCoverBatchCommandValidator()
    {
        RuleFor(x => x.GameIds)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("GameIds must not be null.")
            .NotEmpty().WithMessage("GameIds must contain at least one entry.")
            .Must(ids => ids.Count <= MaxBatchSize)
                .WithMessage($"GameIds must not exceed {MaxBatchSize} entries per batch.")
            .Must(ids => !ids.Contains(Guid.Empty))
                .WithMessage("GameIds must not contain Guid.Empty.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
                .WithMessage("GameIds must not contain duplicates.");
    }
}
