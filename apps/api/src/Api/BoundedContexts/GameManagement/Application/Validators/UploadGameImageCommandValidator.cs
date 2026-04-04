using Api.BoundedContexts.GameManagement.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators;

/// <summary>
/// Validator for UploadGameImageCommand.
/// Ensures GameId and FileName are provided and FileStream is not null.
/// </summary>
internal sealed class UploadGameImageCommandValidator : AbstractValidator<UploadGameImageCommand>
{
    public UploadGameImageCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty().WithMessage("Game ID is required");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("File name is required")
            .MaximumLength(500).WithMessage("File name must not exceed 500 characters");

        RuleFor(x => x.FileStream)
            .NotNull().WithMessage("File stream is required");

        RuleFor(x => x.ImageType)
            .IsInEnum().WithMessage("Image type must be a valid value");
    }
}
