using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for InitChunkedUploadCommand.
/// Ensures UserId is non-empty, FileName is valid, TotalFileSize is positive,
/// and at least one of GameId or PrivateGameId is provided.
/// </summary>
internal sealed class InitChunkedUploadCommandValidator : AbstractValidator<InitChunkedUploadCommand>
{
    private const long MaxFileSizeBytes = 200 * 1024 * 1024; // 200 MB

    public InitChunkedUploadCommandValidator()
    {
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
            .LessThanOrEqualTo(MaxFileSizeBytes)
            .WithMessage("TotalFileSize cannot exceed 200 MB.");

        RuleFor(x => x)
            .Must(x => x.GameId.HasValue || x.PrivateGameId.HasValue)
            .WithMessage("Either GameId or PrivateGameId must be provided.");
    }
}
