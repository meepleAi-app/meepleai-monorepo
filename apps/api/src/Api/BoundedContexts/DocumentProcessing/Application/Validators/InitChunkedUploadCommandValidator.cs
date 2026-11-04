using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for InitChunkedUploadCommand.
/// Ensures UserId is non-empty, FileName is valid, TotalFileSize is positive,
/// and at least one of GameId or PrivateGameId is provided.
/// </summary>
internal sealed class InitChunkedUploadCommandValidator : AbstractValidator<InitChunkedUploadCommand>
{
    public InitChunkedUploadCommandValidator(IOptions<PdfProcessingOptions> pdfOptions)
    {
        ArgumentNullException.ThrowIfNull(pdfOptions);
        // #4449: the single PDF size limit (PdfProcessing:MaxFileSizeBytes), not a number of its own.
        var maxFileSizeBytes = pdfOptions.Value.MaxFileSizeBytes;

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.FileName)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("FileName is required (max 200 chars).");

        RuleFor(x => x.TotalFileSize)
            .GreaterThan(0)
            .WithMessage("TotalFileSize must be greater than zero.")
            .LessThanOrEqualTo(maxFileSizeBytes)
            .WithMessage($"TotalFileSize cannot exceed {PdfSizeLimit.Describe(maxFileSizeBytes)}.");

        RuleFor(x => x)
            .Must(x => x.GameId.HasValue || x.PrivateGameId.HasValue)
            .WithMessage("Either GameId or PrivateGameId must be provided.");
    }
}
