using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators.PrivateGames;

/// <summary>
/// Validator for ProposePrivateGameCommand.
/// Issue #3665: Phase 4 - Proposal System.
/// </summary>
internal sealed class ProposePrivateGameCommandValidator : AbstractValidator<ProposePrivateGameCommand>
{
    public ProposePrivateGameCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.PrivateGameId)
            .NotEmpty()
            .WithMessage("PrivateGameId is required");

        RuleFor(x => x.Notes)
            .MaximumLength(2000)
            .WithMessage("Notes cannot exceed 2000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));

        RuleFor(x => x.AttachedDocumentIds)
            .Must(docs => docs == null || docs.Count <= ShareRequest.MaxDocumentCount)
            .WithMessage($"Cannot attach more than {ShareRequest.MaxDocumentCount} documents")
            .When(x => x.AttachedDocumentIds != null);

        RuleFor(x => x.AttachedDocumentIds)
            .Must(docs => docs == null || docs.All(id => id != Guid.Empty))
            .WithMessage("All document IDs must be valid (non-empty GUIDs)")
            .When(x => x.AttachedDocumentIds != null && x.AttachedDocumentIds.Count > 0);
    }
}
