using Api.BoundedContexts.DocumentProcessing.Application.Commands.BulkUploadPdfs;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for BulkUploadPdfsCommand.
/// Ensures SharedGameId and UserId are non-empty, and Files collection is valid.
/// </summary>
internal sealed class BulkUploadPdfsCommandValidator : AbstractValidator<BulkUploadPdfsCommand>
{
    public BulkUploadPdfsCommandValidator()
    {
        RuleFor(x => x.SharedGameId)
            .NotEmpty()
            .WithMessage("SharedGameId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.Files)
            .NotNull()
            .WithMessage("Files collection is required.");

        When(x => x.Files is not null, () =>
        {
            RuleFor(x => x.Files.Count)
                .GreaterThan(0)
                .WithMessage("At least one file must be provided.")
                .LessThanOrEqualTo(20)
                .WithMessage("Cannot upload more than 20 files at once.");
        });
    }
}
