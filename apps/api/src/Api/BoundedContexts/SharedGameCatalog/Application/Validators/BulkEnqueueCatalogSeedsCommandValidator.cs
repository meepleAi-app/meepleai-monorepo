using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Validators;

/// <summary>
/// Validator for <see cref="BulkEnqueueCatalogSeedsCommand"/>.
/// Enforces the 100-id-per-batch cap to prevent runaway provider fetch storms.
/// Spec: 2026-06-04-admin-catalog-seed-design.md §6.
/// Issue #1903 — M4.3.
/// </summary>
internal sealed class BulkEnqueueCatalogSeedsCommandValidator : AbstractValidator<BulkEnqueueCatalogSeedsCommand>
{
    public const int MaxBatchSize = 100;

    public BulkEnqueueCatalogSeedsCommandValidator()
    {
        RuleFor(x => x.BggIds)
            .NotEmpty()
            .WithMessage("BggIds must not be empty.");

        RuleFor(x => x.BggIds)
            .Must(ids => ids != null && ids.Count <= MaxBatchSize)
            .WithMessage($"Max {MaxBatchSize} BGG IDs per batch.");

        RuleForEach(x => x.BggIds)
            .GreaterThan(0)
            .WithMessage("Each BggId must be a positive integer.");

        RuleFor(x => x.CreatedByUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("CreatedByUserId must not be empty.");
    }
}
