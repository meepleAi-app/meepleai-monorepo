using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validates <see cref="UploadPhotoBatchCommand"/> before processing.
/// Enforces batch size limits, per-photo size cap, and supported source languages.
/// Libro Game AI Assistant MVP Phase 1 — Task 1.3.
/// </summary>
internal sealed class UploadPhotoBatchCommandValidator : AbstractValidator<UploadPhotoBatchCommand>
{
    private static readonly HashSet<string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "en", "it", "de", "fr", "es", "pt", "nl"
    };

    private const int MaxPhotosPerBatch = 200;

    /// <summary>
    /// ~10 MB binary encodes to ~13.3 MB base64; 14 MB gives a small safety margin.
    /// </summary>
    private const long MaxBase64SizeBytes = 14_000_000;

    public UploadPhotoBatchCommandValidator()
    {
        RuleFor(c => c.UserId)
            .NotEmpty()
            .WithMessage("User ID is required.");

        RuleFor(c => c.GameId)
            .NotEmpty()
            .WithMessage("Game ID is required.");

        RuleFor(c => c.SourceLanguage)
            .NotEmpty()
            .WithMessage("Source language is required.")
            .Must(lang => SupportedLanguages.Contains(lang))
            .WithMessage("Source language must be one of: en, it, de, fr, es, pt, nl");

        RuleFor(c => c.Photos)
            .NotEmpty()
            .WithMessage("At least one photo required.")
            .Must(photos => photos.Length <= MaxPhotosPerBatch)
            .WithMessage($"Maximum {MaxPhotosPerBatch} photos per batch.");

        RuleForEach(c => c.Photos).ChildRules(photo =>
        {
            photo.RuleFor(p => p.Filename)
                .NotEmpty()
                .WithMessage("Photo filename is required.");

            photo.RuleFor(p => p.Base64Content)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("Photo base64 content is required.")
                .Must(s => s.Length <= MaxBase64SizeBytes)
                .WithMessage("Photo exceeds maximum size of 10 MB.")
                .Must(BeValidBase64)
                .WithMessage("Invalid base64 content.");
        });
    }

    private static bool BeValidBase64(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        try
        {
            Convert.FromBase64String(s);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
