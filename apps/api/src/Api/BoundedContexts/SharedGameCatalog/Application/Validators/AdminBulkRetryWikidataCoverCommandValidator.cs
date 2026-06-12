using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Validators;

/// <summary>
/// Validator for <see cref="AdminBulkRetryWikidataCoverCommand"/>. Caps the
/// batch at 50 so a runaway click cannot deluge the M9 rate-limiter (DEC-3e
/// shared limiter is 5 rps shared across SPARQL+Commons; 50 enrichments at
/// worst-case ~6s/game = 5 minutes of inline admin-request work, which is the
/// outer bound of acceptable HTTP timeout). Issue #1823 Phase E F2.
/// </summary>
internal sealed class AdminBulkRetryWikidataCoverCommandValidator
    : AbstractValidator<AdminBulkRetryWikidataCoverCommand>
{
    /// <summary>
    /// Maximum number of attempts that can be re-dispatched in a single
    /// admin click. Larger batches must be split across multiple requests.
    /// </summary>
    public const int MaxBatchSize = 50;

    public AdminBulkRetryWikidataCoverCommandValidator()
    {
        RuleFor(x => x.AttemptIds)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .NotEmpty()
            .WithMessage("AttemptIds must contain at least one id.")
            .Must(ids => ids.Count <= MaxBatchSize)
            .WithMessage($"AttemptIds cannot exceed {MaxBatchSize} per request.")
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("AttemptIds must not contain Guid.Empty.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("AttemptIds must not contain duplicates.");

        RuleFor(x => x.TriggeredByUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("TriggeredByUserId must not be empty.");
    }
}
