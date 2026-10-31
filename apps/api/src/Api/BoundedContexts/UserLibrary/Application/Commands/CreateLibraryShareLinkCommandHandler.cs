using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.SharedKernel.Services;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Handler for creating a new library share link.
/// Enforces rate limiting (max 10 per day) and revokes existing active links.
/// </summary>
internal class CreateLibraryShareLinkCommandHandler : ICommandHandler<CreateLibraryShareLinkCommand, LibraryShareLinkDto>
{
    private readonly ILibraryShareLinkRepository _shareLinkRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublicBaseUrlProvider _publicBaseUrl;
    private readonly ILogger<CreateLibraryShareLinkCommandHandler> _logger;

    private const int MaxShareLinksPerDay = 10;

    // Tentativi di salvataggio quando il link da revocare è già stato revocato da un'altra richiesta (#4558).
    // Ogni tentativo rilegge il link attivo: ne serve uno in più per ogni richiesta concorrente.
    private const int MaxSaveAttempts = 3;

    public CreateLibraryShareLinkCommandHandler(
        ILibraryShareLinkRepository shareLinkRepository,
        IUnitOfWork unitOfWork,
        IPublicBaseUrlProvider publicBaseUrl,
        ILogger<CreateLibraryShareLinkCommandHandler> logger)
    {
        _shareLinkRepository = shareLinkRepository ?? throw new ArgumentNullException(nameof(shareLinkRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _publicBaseUrl = publicBaseUrl ?? throw new ArgumentNullException(nameof(publicBaseUrl));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LibraryShareLinkDto> Handle(CreateLibraryShareLinkCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Check rate limit
        var recentCount = await _shareLinkRepository.CountRecentByUserIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        if (recentCount >= MaxShareLinksPerDay)
        {
            _logger.LogWarning("Rate limit exceeded for user {UserId}: {Count} share links in last 24h", command.UserId, recentCount);
            throw new DomainException($"Maximum {MaxShareLinksPerDay} share links per day allowed");
        }

        // #4774: resolved before the existing link is revoked and the new one saved: with no address configured
        // (a deployed host) the call fails here, not after the person's working link has been revoked for a new
        // one whose URL cannot be written.
        var baseUrl = _publicBaseUrl.GetBaseUrl();

        // Revoke any existing active share link
        await RevokeActiveLinkAsync(command.UserId, cancellationToken).ConfigureAwait(false);

        // Parse privacy level
        var privacyLevel = command.PrivacyLevel.ToLowerInvariant() switch
        {
            "public" => LibrarySharePrivacyLevel.Public,
            "unlisted" => LibrarySharePrivacyLevel.Unlisted,
            _ => LibrarySharePrivacyLevel.Unlisted
        };

        // Create new share link
        var shareLink = LibraryShareLink.Create(
            userId: command.UserId,
            privacyLevel: privacyLevel,
            includeNotes: command.IncludeNotes,
            expiresAt: command.ExpiresAt
        );

        await _shareLinkRepository.AddAsync(shareLink, cancellationToken).ConfigureAwait(false);
        await SaveRevokingTheActiveLinkAsync(command.UserId, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created library share link {LinkId} for user {UserId} with privacy {Privacy}",
            shareLink.Id, command.UserId, privacyLevel);

        return MapToDto(shareLink, baseUrl);
    }

    private async Task RevokeActiveLinkAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existingLink = await _shareLinkRepository.GetActiveByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (existingLink == null)
        {
            return;
        }

        existingLink.Revoke();
        await _shareLinkRepository.UpdateAsync(existingLink, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Revoked existing share link {LinkId} for user {UserId}", existingLink.Id, userId);
    }

    /// <summary>
    /// Salva il link nuovo e la revoca del vecchio, anche se un'altra richiesta ha revocato il vecchio nel frattempo.
    /// </summary>
    /// <remarks>
    /// <c>UpdateAsync</c> scrive la revoca solo su un link ancora non revocato (#4558). Se un'altra richiesta l'ha
    /// revocato dopo la nostra lettura (una revoca, o un'altra creazione come in un doppio click), l'UPDATE tocca
    /// 0 righe e <c>SaveChangesAsync</c> fallisce, INSERT del link nuovo compreso. La revoca che volevamo c'è già:
    /// si stacca la scrittura fallita, si rilegge il link attivo (può essere quello appena creato dall'altra
    /// richiesta) e si riprova. L'esito è quello delle due richieste in fila: un solo link attivo, l'ultimo creato.
    /// </remarks>
    private async Task SaveRevokingTheActiveLinkAsync(Guid userId, CancellationToken cancellationToken)
    {
        var attempt = 1;
        while (true)
        {
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < MaxSaveAttempts)
            {
                _logger.LogInformation(
                    ex,
                    "The share link to revoke for user {UserId} was revoked by a concurrent request (attempt {Attempt})",
                    userId, attempt);
                attempt++;

                // Solo l'UPDATE della revoca può toccare 0 righe: l'INSERT del link nuovo resta in coda.
                foreach (var entry in ex.Entries)
                {
                    entry.State = EntityState.Detached;
                }

                await RevokeActiveLinkAsync(userId, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static LibraryShareLinkDto MapToDto(LibraryShareLink link, string baseUrl)
    {
        var shareUrl = $"{baseUrl}/library/shared/{link.ShareToken}";

        return new LibraryShareLinkDto(
            Id: link.Id,
            UserId: link.UserId,
            ShareToken: link.ShareToken,
            ShareUrl: shareUrl,
            PrivacyLevel: link.PrivacyLevel.ToString().ToLowerInvariant(),
            IncludeNotes: link.IncludeNotes,
            CreatedAt: link.CreatedAt,
            ExpiresAt: link.ExpiresAt,
            RevokedAt: link.RevokedAt,
            ViewCount: link.ViewCount,
            LastAccessedAt: link.LastAccessedAt,
            IsActive: link.IsValid
        );
    }
}
