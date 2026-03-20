using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Handler for creating a new library share link.
/// Enforces rate limiting (max 10 per day) and revokes existing active links.
/// </summary>
internal class CreateLibraryShareLinkCommandHandler : ICommandHandler<CreateLibraryShareLinkCommand, LibraryShareLinkDto>
{
    private readonly ILibraryShareLinkRepository _shareLinkRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CreateLibraryShareLinkCommandHandler> _logger;

    private const int MaxShareLinksPerDay = 10;

    public CreateLibraryShareLinkCommandHandler(
        ILibraryShareLinkRepository shareLinkRepository,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<CreateLibraryShareLinkCommandHandler> logger)
    {
        _shareLinkRepository = shareLinkRepository ?? throw new ArgumentNullException(nameof(shareLinkRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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

        // Revoke any existing active share link
        var existingLink = await _shareLinkRepository.GetActiveByUserIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        if (existingLink != null)
        {
            existingLink.Revoke();
            await _shareLinkRepository.UpdateAsync(existingLink, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Revoked existing share link {LinkId} for user {UserId}", existingLink.Id, command.UserId);
        }

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
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created library share link {LinkId} for user {UserId} with privacy {Privacy}",
            shareLink.Id, command.UserId, privacyLevel);

        return MapToDto(shareLink);
    }

    private LibraryShareLinkDto MapToDto(LibraryShareLink link)
    {
        var baseUrl = _configuration["App:BaseUrl"]
            ?? throw new InvalidOperationException("App:BaseUrl configuration is not set");
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
