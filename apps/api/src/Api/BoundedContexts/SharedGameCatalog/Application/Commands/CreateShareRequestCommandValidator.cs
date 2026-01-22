using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.Services;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for CreateShareRequestCommand.
/// Validates business rules including ownership, rate limits, and document ownership.
/// </summary>
internal sealed class CreateShareRequestCommandValidator : AbstractValidator<CreateShareRequestCommand>
{
    private readonly IUserLibraryRepository _userLibraryRepository;
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly IPdfDocumentRepository _documentRepository;
    private readonly IRateLimitEvaluator _rateLimitEvaluator;

    public CreateShareRequestCommandValidator(
        IUserLibraryRepository userLibraryRepository,
        IShareRequestRepository shareRequestRepository,
        IPdfDocumentRepository documentRepository,
        IRateLimitEvaluator rateLimitEvaluator)
    {
        _userLibraryRepository = userLibraryRepository ?? throw new ArgumentNullException(nameof(userLibraryRepository));
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _rateLimitEvaluator = rateLimitEvaluator ?? throw new ArgumentNullException(nameof(rateLimitEvaluator));

        ConfigureRules();
    }

    private void ConfigureRules()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.SourceGameId)
            .NotEmpty()
            .WithMessage("SourceGameId is required");

        // User must own the game in their library
        RuleFor(x => x)
            .MustAsync(UserOwnsGameAsync)
            .WithMessage("You can only share games from your library")
            .When(x => x.UserId != Guid.Empty && x.SourceGameId != Guid.Empty);

        // No duplicate pending request for the same game
        RuleFor(x => x)
            .MustAsync(NoPendingRequestExistsAsync)
            .WithMessage("You already have a pending share request for this game")
            .When(x => x.UserId != Guid.Empty && x.SourceGameId != Guid.Empty);

        // Rate limit check
        RuleFor(x => x)
            .MustAsync(RateLimitNotExceededAsync)
            .WithMessage("Rate limit exceeded. Please wait before submitting new requests.")
            .When(x => x.UserId != Guid.Empty);

        // Notes length validation
        RuleFor(x => x.Notes)
            .MaximumLength(2000)
            .WithMessage("Notes cannot exceed 2000 characters")
            .When(x => x.Notes != null);

        // Document ownership validation
        RuleFor(x => x.AttachedDocumentIds)
            .MustAsync(UserOwnsAllDocumentsAsync!)
            .WithMessage("You can only attach documents you own")
            .When(x => x.AttachedDocumentIds != null && x.AttachedDocumentIds.Count > 0);

        // Maximum document count
        RuleFor(x => x.AttachedDocumentIds)
            .Must(x => x == null || x.Count <= 10)
            .WithMessage("Cannot attach more than 10 documents");
    }

    private async Task<bool> UserOwnsGameAsync(CreateShareRequestCommand command, CancellationToken cancellationToken)
    {
        return await _userLibraryRepository.IsGameInLibraryAsync(
            command.UserId,
            command.SourceGameId,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> NoPendingRequestExistsAsync(CreateShareRequestCommand command, CancellationToken cancellationToken)
    {
        var hasPending = await _shareRequestRepository.HasPendingRequestAsync(
            command.UserId,
            command.SourceGameId,
            cancellationToken).ConfigureAwait(false);
        return !hasPending;
    }

    private async Task<bool> RateLimitNotExceededAsync(CreateShareRequestCommand command, CancellationToken cancellationToken)
    {
        return await _rateLimitEvaluator.CanUserCreateRequestAsync(command.UserId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> UserOwnsAllDocumentsAsync(
        CreateShareRequestCommand command,
        List<Guid> documentIds,
        CancellationToken cancellationToken)
    {
        if (documentIds == null || documentIds.Count == 0)
            return true;

        var documents = await _documentRepository.GetByIdsAsync(documentIds, cancellationToken).ConfigureAwait(false);

        // All requested documents must exist and be owned by the user
        if (documents.Count != documentIds.Count)
            return false;

        return documents.All(d => d.UploadedByUserId == command.UserId);
    }
}
