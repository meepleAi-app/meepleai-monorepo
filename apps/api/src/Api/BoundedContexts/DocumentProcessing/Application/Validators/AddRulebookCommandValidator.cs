using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for AddRulebookCommand.
/// Ensures GameId, UserId are non-empty and File is a valid PDF.
/// </summary>
internal sealed class AddRulebookCommandValidator : AbstractValidator<AddRulebookCommand>
{
    public AddRulebookCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.File)
            .NotNull()
            .WithMessage("A PDF file is required.");

        When(x => x.File is not null, () =>
        {
            RuleFor(x => x.File.Length)
                .GreaterThan(0)
                .WithMessage("File must not be empty.");

            RuleFor(x => x.File.ContentType)
                .Equal("application/pdf")
                .WithMessage("File must be a PDF.");
        });
    }
}
