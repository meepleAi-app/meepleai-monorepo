using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetMyActiveReviews;

/// <summary>
/// Handler for GetMyActiveReviewsQuery.
/// Retrieves all share requests currently being reviewed by the specified admin.
/// </summary>
internal sealed class GetMyActiveReviewsQueryHandler
    : IRequestHandler<GetMyActiveReviewsQuery, IReadOnlyCollection<ActiveReviewDto>>
{
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<GetMyActiveReviewsQueryHandler> _logger;

    public GetMyActiveReviewsQueryHandler(
        MeepleAiDbContext context,
        ILogger<GetMyActiveReviewsQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyCollection<ActiveReviewDto>> Handle(
        GetMyActiveReviewsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Getting active reviews for admin: {AdminId}",
            query.AdminId);

        // Query share requests being reviewed by this admin with eager loading
        var shareRequests = await _context.Set<ShareRequestEntity>()
            .AsNoTracking()
            .Include(sr => sr.SourceGame)
            .Where(sr => sr.ReviewingAdminId == query.AdminId
                && sr.Status == (int)ShareRequestStatus.InReview)
            .OrderByDescending(sr => sr.ReviewStartedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Get user IDs for contributors
        var userIds = shareRequests.Select(sr => sr.UserId).Distinct().ToList();

        // Fetch user data
        var users = await _context.Set<UserEntity>()
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken)
            .ConfigureAwait(false);

        // Map to DTOs
        var results = shareRequests
            .Select(sr => new ActiveReviewDto(
                sr.Id,
                sr.SourceGameId,
                sr.SourceGame?.Title ?? "Unknown Game",
                sr.UserId,
                users.TryGetValue(sr.UserId, out var user)
                    ? (user.DisplayName ?? user.Email ?? "Unknown User")
                    : "Unknown User",
                sr.ReviewStartedAt!.Value,
                sr.ReviewLockExpiresAt!.Value,
                (ShareRequestStatus)sr.Status))
            .ToList();

        _logger.LogInformation(
            "Retrieved {Count} active reviews for admin {AdminId}",
            results.Count, query.AdminId);

        return results;
    }
}
