using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.UpdateKbDocMetadata;

/// <summary>
/// FluentValidation rules for <see cref="UpdateKbDocMetadataCommand"/> (Issue #1687 Task 5).
///
/// Conditional gating per field via <c>.When(x => x.Field is not null)</c> implements
/// the partial-update contract (D-4). Failures propagate as
/// <see cref="FluentValidation.ValidationException"/> which the
/// <c>ApiExceptionHandlerMiddleware</c> maps to HTTP 422.
/// </summary>
internal sealed class UpdateKbDocMetadataCommandValidator : AbstractValidator<UpdateKbDocMetadataCommand>
{
    private const int TitleMaxLength = 200;
    private const int MaxTagCount = 20;
    private const int MaxTagLength = 50;

    public UpdateKbDocMetadataCommandValidator()
    {
        RuleFor(x => x.DocId)
            .NotEmpty()
            .WithMessage("DocId must not be empty.");

        RuleFor(x => x.EditorUserId)
            .NotEmpty()
            .WithMessage("EditorUserId must not be empty.");

        RuleFor(x => x.Title!)
            .MaximumLength(TitleMaxLength)
                .WithMessage($"Title must not exceed {TitleMaxLength} characters.")
            .When(x => x.Title is not null);

        RuleFor(x => x.DocumentType!)
            .Must(BeValidDocumentCategory)
                .WithMessage("DocumentType must be one of: Rulebook, Expansion, Errata, QuickStart, Reference, PlayerAid, Other (case-insensitive).")
            .When(x => x.DocumentType is not null);

        RuleFor(x => x.Language!)
            .Must(LanguageCode.IsSupported)
                .WithMessage("Language must be one of the supported ISO 639-1 codes (en, it, de, fr, es, pt, pl, nl, ja, zh).")
            .When(x => x.Language is not null);

        RuleFor(x => x.Tags)
            .Must(t => t is null || t.Count <= MaxTagCount)
                .WithMessage($"Tags must contain at most {MaxTagCount} items.");

        RuleForEach(x => x.Tags!)
            .Must(tag => !string.IsNullOrWhiteSpace(tag) && tag.Trim().Length <= MaxTagLength)
                .WithMessage($"Each tag must be 1-{MaxTagLength} characters and non-empty.")
            .When(x => x.Tags is not null);
    }

    private static bool BeValidDocumentCategory(string value)
        => Enum.TryParse<DocumentCategory>(value, ignoreCase: true, out _);
}
