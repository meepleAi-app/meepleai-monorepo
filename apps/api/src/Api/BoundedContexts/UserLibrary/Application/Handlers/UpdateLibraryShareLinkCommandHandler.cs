using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for updating library share link settings.
/// </summary>
internal class UpdateLibraryShareLinkCommandHandler : ICommandHandler<UpdateLibraryShareLinkCommand, LibraryShareLinkDto>
{
    private readonly ILibraryShareLinkRepository _shareLinkRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UpdateLibraryShareLinkCommandHandler> _logger;

    public UpdateLibraryShareLinkCommandHandler(
        ILibraryShareLinkRepository shareLinkRepository,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<UpdateLibraryShareLinkCommandHandler> logger)
    {
        _shareLinkRepository = shareLinkRepository ?? throw new ArgumentNullException(nameof(shareLinkRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LibraryShareLinkDto> Handle(UpdateLibraryShareLinkCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var shareLink = await _shareLinkRepository.GetByShareTokenAsync(command.ShareToken, cancellationToken).ConfigureAwait(false);

        if (shareLink == null)
        {
            throw new NotFoundException($"Share link with token '{command.ShareToken}' not found");
        }

        // Verify ownership
        if (shareLink.UserId != command.UserId)
        {
            _logger.LogWarning(
                "User {UserId} attempted to update share link owned by {OwnerId}",
                command.UserId, shareLink.UserId);
            throw new NotFoundException($"Share link with token '{command.ShareToken}' not found");
        }

        if (!shareLink.IsValid)
        {
            throw new DomainException("Cannot update revoked or expired share link");
        }

        // Update privacy level if provided
        if (!string.IsNullOrEmpty(command.PrivacyLevel))
        {
            var privacyLevel = command.PrivacyLevel.ToLowerInvariant() switch
            {
                "public" => LibrarySharePrivacyLevel.Public,
                "unlisted" => LibrarySharePrivacyLevel.Unlisted,
                _ => throw new DomainException("Invalid privacy level")
            };
            shareLink.UpdatePrivacyLevel(privacyLevel);
        }

        // Update include notes if provided
        if (command.IncludeNotes.HasValue)
        {
            shareLink.UpdateIncludeNotes(command.IncludeNotes.Value);
        }

        // Update expiration if provided (can be set to null to remove expiration)
        if (command.ExpiresAt.HasValue)
        {
            shareLink.UpdateExpiration(command.ExpiresAt);
        }

        await _shareLinkRepository.UpdateAsync(shareLink, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Updated share link {LinkId} for user {UserId}", shareLink.Id, command.UserId);

        return MapToDto(shareLink);
    }

    private LibraryShareLinkDto MapToDto(Domain.Entities.LibraryShareLink link)
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
