using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Validators;

/// <summary>
/// Validator for <see cref="AdminBulkAcknowledgeWikidataCoverCommand"/>. Caps
/// the batch at <see cref="MaxBatchSize"/> (DEC-3e shared rate-limiter budget,
/// mirror of F2 bulk-retry) and the optional note at <see cref="MaxNoteLength"/>
/// (DEC-F-4 log-only payload). Issue #1823 Phase F F5.
/// </summary>
internal sealed class AdminBulkAcknowledgeWikidataCoverCommandValidator
    : AbstractValidator<AdminBulkAcknowledgeWikidataCoverCommand>
{
    /// <summary>
    /// Maximum number of attempts that can be acknowledged in a single admin
    /// click. Larger batches must be split across multiple requests. Mirrors
    /// <c>AdminBulkRetryWikidataCoverCommandValidator.MaxBatchSize</c>.
    /// </summary>
    public const int MaxBatchSize = 50;

    /// <summary>
    /// Maximum length of the optional acknowledgement note. The note is
    /// log-only (not persisted on the attempt row) but we still cap it so the
    /// audit-log payload stays small.
    /// </summary>
    public const int MaxNoteLength = 500;

    public AdminBulkAcknowledgeWikidataCoverCommandValidator()
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

        RuleFor(x => x.Note)
            .MaximumLength(MaxNoteLength)
            .When(x => x.Note is not null)
            .WithMessage($"Note must be {MaxNoteLength} characters or fewer.");

        RuleFor(x => x.TriggeredByUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("TriggeredByUserId must not be empty.");
    }
}
