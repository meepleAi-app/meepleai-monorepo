using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Services;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Handler for getting user's active library share link.
/// </summary>
internal class GetLibraryShareLinkQueryHandler : IQueryHandler<GetLibraryShareLinkQuery, LibraryShareLinkDto?>
{
    private readonly ILibraryShareLinkRepository _shareLinkRepository;
    private readonly IPublicBaseUrlProvider _publicBaseUrl;

    public GetLibraryShareLinkQueryHandler(
        ILibraryShareLinkRepository shareLinkRepository,
        IPublicBaseUrlProvider publicBaseUrl)
    {
        _shareLinkRepository = shareLinkRepository ?? throw new ArgumentNullException(nameof(shareLinkRepository));
        _publicBaseUrl = publicBaseUrl ?? throw new ArgumentNullException(nameof(publicBaseUrl));
    }

    public async Task<LibraryShareLinkDto?> Handle(GetLibraryShareLinkQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var shareLink = await _shareLinkRepository.GetActiveByUserIdAsync(query.UserId, cancellationToken).ConfigureAwait(false);

        if (shareLink == null)
        {
            return null;
        }

        return MapToDto(shareLink);
    }

    private LibraryShareLinkDto MapToDto(LibraryShareLink link)
    {
        var baseUrl = _publicBaseUrl.GetBaseUrl();
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
