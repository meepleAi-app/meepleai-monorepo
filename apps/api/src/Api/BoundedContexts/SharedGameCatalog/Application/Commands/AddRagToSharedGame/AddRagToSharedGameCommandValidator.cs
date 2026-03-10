using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;

internal sealed class AddRagToSharedGameCommandValidator : AbstractValidator<AddRagToSharedGameCommand>
{
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50MB

    public AddRagToSharedGameCommandValidator()
    {
        RuleFor(x => x.SharedGameId)
            .NotEqual(Guid.Empty).WithMessage("SharedGameId is required");

        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty).WithMessage("UserId is required");

        RuleFor(x => x.File)
            .NotNull().WithMessage("PDF file is required");

        RuleFor(x => x.File)
            .Must(f => f == null || f.Length <= MaxFileSizeBytes)
            .WithMessage("File size must not exceed 50MB");

        RuleFor(x => x.DocumentType)
            .IsInEnum().WithMessage("DocumentType must be valid (Rulebook, Errata, or Homerule)");

        RuleFor(x => x.Version)
            .NotEmpty().WithMessage("Version is required")
            .Matches(@"^\d+\.\d+$").WithMessage("Version must be in format MAJOR.MINOR (e.g., 1.0, 2.1)")
            .MaximumLength(20).WithMessage("Version cannot exceed 20 characters");

        RuleFor(x => x.Tags)
            .Must(tags => tags == null || tags.Count <= 10)
            .WithMessage("Cannot have more than 10 tags")
            .Must(tags => tags == null || tags.All(t => !string.IsNullOrWhiteSpace(t) && t.Length <= 50))
            .WithMessage("Each tag must be non-empty and not exceed 50 characters");

        RuleFor(x => x)
            .Must(x => x.DocumentType == SharedGameDocumentType.Homerule || x.Tags == null || x.Tags.Count == 0)
            .WithMessage("Tags are only allowed for Homerule documents");
    }
}
