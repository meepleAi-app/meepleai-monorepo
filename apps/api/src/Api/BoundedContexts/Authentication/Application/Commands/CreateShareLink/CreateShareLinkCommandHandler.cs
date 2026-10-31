using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Api.BoundedContexts.Authentication.Application.Commands.CreateShareLink;

/// <summary>
/// Handler for creating shareable chat thread links.
/// </summary>
internal sealed class CreateShareLinkCommandHandler : IRequestHandler<CreateShareLinkCommand, CreateShareLinkResult>
{
    private readonly MeepleAiDbContext _context;
    private readonly IShareLinkRepository _shareLinkRepository;
    private readonly IConfiguration _configuration;
    private readonly IPublicBaseUrlProvider _publicBaseUrl;

    public CreateShareLinkCommandHandler(
        MeepleAiDbContext context,
        IShareLinkRepository shareLinkRepository,
        IConfiguration configuration,
        IPublicBaseUrlProvider publicBaseUrl)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _shareLinkRepository = shareLinkRepository ?? throw new ArgumentNullException(nameof(shareLinkRepository));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _publicBaseUrl = publicBaseUrl ?? throw new ArgumentNullException(nameof(publicBaseUrl));
    }

    public async Task<CreateShareLinkResult> Handle(
        CreateShareLinkCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Verify that the chat thread exists and belongs to the requesting user
        var threadExists = await _context.ChatThreads
            .AnyAsync(
                t => t.Id == request.ThreadId && t.UserId == request.UserId,
                cancellationToken).ConfigureAwait(false);

        if (!threadExists)
        {
            throw new InvalidOperationException(
                $"Chat thread {request.ThreadId} not found or does not belong to user {request.UserId}");
        }

        // #4774: resolved before the link is saved: with no address configured (a deployed host) the call fails
        // here, not after an audit row has been stored for a URL that cannot be written.
        var baseUrl = _publicBaseUrl.GetBaseUrl();

        // Create the share link entity
        var shareLink = ShareLink.Create(
            threadId: request.ThreadId,
            creatorId: request.UserId,
            role: request.Role,
            expiresAt: request.ExpiresAt,
            label: request.Label
        );

        // Save to database for audit trail via repository
        await _shareLinkRepository.AddAsync(shareLink, cancellationToken).ConfigureAwait(false);

        // Generate JWT token
        var secretKey = _configuration["Jwt:ShareLinks:SecretKey"]
            ?? throw new InvalidOperationException("JWT secret key not configured");

        var token = shareLink.GenerateToken(secretKey);

        // Build shareable URL
        var shareableUrl = $"{baseUrl}/shared/chat?token={token.Value}";

        return new CreateShareLinkResult(
            ShareLinkId: shareLink.Id,
            Token: token.Value,
            ExpiresAt: shareLink.ExpiresAt,
            ShareableUrl: shareableUrl
        );
    }
}
