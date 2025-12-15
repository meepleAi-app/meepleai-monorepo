using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Infrastructure;
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

    public CreateShareLinkCommandHandler(
        MeepleAiDbContext context,
        IShareLinkRepository shareLinkRepository,
        IConfiguration configuration)
    {
        _context = context;
        _shareLinkRepository = shareLinkRepository;
        _configuration = configuration;
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
        var baseUrl = _configuration["App:BaseUrl"]
            ?? throw new InvalidOperationException("App base URL not configured");

        var shareableUrl = $"{baseUrl}/shared/chat?token={token.Value}";

        return new CreateShareLinkResult(
            ShareLinkId: shareLink.Id,
            Token: token.Value,
            ExpiresAt: shareLink.ExpiresAt,
            ShareableUrl: shareableUrl
        );
    }
}
