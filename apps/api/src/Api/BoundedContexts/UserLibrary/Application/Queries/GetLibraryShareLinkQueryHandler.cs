using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Handler for getting user's active library share link.
/// </summary>
internal class GetLibraryShareLinkQueryHandler : IQueryHandler<GetLibraryShareLinkQuery, LibraryShareLinkDto?>
{
    private readonly ILibraryShareLinkRepository _shareLinkRepository;
    private readonly IConfiguration _configuration;

    public GetLibraryShareLinkQueryHandler(
        ILibraryShareLinkRepository shareLinkRepository,
        IConfiguration configuration)
    {
        _shareLinkRepository = shareLinkRepository ?? throw new ArgumentNullException(nameof(shareLinkRepository));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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
