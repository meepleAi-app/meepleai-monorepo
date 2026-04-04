using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for UploadChunkCommand.
/// Ensures SessionId and UserId are non-empty, ChunkIndex is non-negative, and ChunkData is present.
/// </summary>
internal sealed class UploadChunkCommandValidator : AbstractValidator<UploadChunkCommand>
{
    public UploadChunkCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("SessionId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.ChunkIndex)
            .GreaterThanOrEqualTo(0)
            .WithMessage("ChunkIndex cannot be negative.");

        RuleFor(x => x.ChunkData)
            .NotNull()
            .WithMessage("ChunkData is required.");

        When(x => x.ChunkData is not null, () =>
        {
            RuleFor(x => x.ChunkData.Length)
                .GreaterThan(0)
                .WithMessage("ChunkData must not be empty.");
        });
    }
}
