using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for CreateSharedGameFromPdfCommand.
/// Ensures PDF document exists, title is valid, and manual overrides are within acceptable ranges.
/// Issue #4138: Backend - Commands and DTOs - PDF Wizard
/// </summary>
internal sealed class CreateSharedGameFromPdfCommandValidator : AbstractValidator<CreateSharedGameFromPdfCommand>
{
    public CreateSharedGameFromPdfCommandValidator()
    {
        RuleFor(x => x.PdfDocumentId)
            .NotEqual(Guid.Empty)
            .WithMessage("PDF Document ID is required");

        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .WithMessage("User ID is required");

        RuleFor(x => x.ExtractedTitle)
            .NotEmpty()
            .WithMessage("Extracted title is required")
            .MaximumLength(200)
            .WithMessage("Title cannot exceed 200 characters");

        RuleFor(x => x.MinPlayers)
            .InclusiveBetween(1, 100)
            .When(x => x.MinPlayers.HasValue)
            .WithMessage("Minimum players must be between 1 and 100");

        RuleFor(x => x.MaxPlayers)
            .InclusiveBetween(1, 100)
            .When(x => x.MaxPlayers.HasValue)
            .WithMessage("Maximum players must be between 1 and 100");

        RuleFor(x => x)
            .Must(x => !x.MinPlayers.HasValue || !x.MaxPlayers.HasValue || x.MaxPlayers >= x.MinPlayers)
            .WithMessage("Maximum players must be greater than or equal to minimum players")
            .When(x => x.MinPlayers.HasValue && x.MaxPlayers.HasValue);

        RuleFor(x => x.PlayingTimeMinutes)
            .InclusiveBetween(1, 1440)
            .When(x => x.PlayingTimeMinutes.HasValue)
            .WithMessage("Playing time must be between 1 and 1440 minutes (24 hours)");

        RuleFor(x => x.MinAge)
            .InclusiveBetween(1, 99)
            .When(x => x.MinAge.HasValue)
            .WithMessage("Minimum age must be between 1 and 99");

        RuleFor(x => x.SelectedBggId)
            .GreaterThan(0)
            .When(x => x.SelectedBggId.HasValue)
            .WithMessage("BGG ID must be a positive integer");
    }
}
