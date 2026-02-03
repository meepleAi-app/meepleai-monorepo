using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Event handler that creates special notifications for milestone badge achievements.
/// ISSUE-2741: Extra celebration for milestone badges (50, 100 contributions, etc.)
/// </summary>
internal sealed class MilestoneBadgeNotificationHandler : INotificationHandler<BadgeEarnedEvent>
{
    private readonly IBadgeRepository _badgeRepository;
    private readonly IContributorRepository _contributorRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MilestoneBadgeNotificationHandler> _logger;

    // Milestone badge codes that trigger extra celebration
    private static readonly HashSet<string> MilestoneBadgeCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "CONTRIBUTOR_50",
        "CONTRIBUTOR_100",
        "FIRST_CONTRIBUTION",
        "COMMUNITY_CHAMPION"
    };

    public MilestoneBadgeNotificationHandler(
        IBadgeRepository badgeRepository,
        IContributorRepository contributorRepository,
        MeepleAiDbContext dbContext,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<MilestoneBadgeNotificationHandler> logger)
    {
        _badgeRepository = badgeRepository ?? throw new ArgumentNullException(nameof(badgeRepository));
        _contributorRepository = contributorRepository ?? throw new ArgumentNullException(nameof(contributorRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(BadgeEarnedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            // Check if this is a milestone badge
            if (!MilestoneBadgeCodes.Contains(notification.BadgeCode))
            {
                _logger.LogDebug(
                    "Badge {BadgeCode} is not a milestone badge. Skipping milestone notification.",
                    notification.BadgeCode);
                return;
            }

            // Get badge and user details
            var badge = await _badgeRepository.GetByIdAsync(notification.BadgeId, cancellationToken)
                .ConfigureAwait(false);

            if (badge == null)
            {
                _logger.LogWarning(
                    "Badge {BadgeId} not found for milestone notification",
                    notification.BadgeId);
                return;
            }

            var user = await _dbContext.Set<UserEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == notification.UserId, cancellationToken)
                .ConfigureAwait(false);

            if (user == null)
            {
                _logger.LogWarning(
                    "User {UserId} not found for milestone badge notification",
                    notification.UserId);
                return;
            }

            // Get total contribution count for user
            var (_, totalCount) = await _contributorRepository.GetByUserIdAsync(
                notification.UserId,
                pageNumber: 1,
                pageSize: 1,  // We only need the count
                cancellationToken)
                .ConfigureAwait(false);

#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback value
            var baseUrl = _configuration["App:BaseUrl"] ?? "https://meepleai.com";
#pragma warning restore S1075

            // Send special milestone email
            await _emailService.SendMilestoneBadgeEarnedEmailAsync(
                toEmail: user.Email,
                userName: user.DisplayName ?? user.Email,
                badgeName: badge.Name,
                badgeDescription: badge.Description,
                badgeIconUrl: badge.IconUrl,
                badgeTier: badge.Tier.ToString(),
                milestoneMessage: GetMilestoneMessage(notification.BadgeCode),
                totalContributions: totalCount,
                profileUrl: $"{baseUrl}/users/{user.Id}/badges",
                leaderboardUrl: $"{baseUrl}/leaderboard",
                cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Sent milestone badge email for user {UserId} - Badge: {BadgeName} ({BadgeCode}), Total Contributions: {TotalContributions}",
                notification.UserId,
                badge.Name,
                notification.BadgeCode,
                totalCount);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create milestone badge notification for user {UserId}, badge {BadgeCode}",
                notification.UserId,
                notification.BadgeCode);
            // Don't rethrow - event handler failures should not block the main operation
        }
#pragma warning restore CA1031
    }

    private static string GetMilestoneMessage(string badgeCode) => badgeCode.ToUpperInvariant() switch
    {
        "FIRST_CONTRIBUTION" => "Welcome to the MeepleAI community! Your first contribution is the beginning of an amazing journey.",
        "CONTRIBUTOR_50" => "50 contributions is a remarkable achievement! You're in the top tier of contributors.",
        "CONTRIBUTOR_100" => "100 contributions! You're a true legend of the MeepleAI community!",
        "COMMUNITY_CHAMPION" => "You've shown exceptional dedication to the community. Thank you for your contributions!",
        _ => "Congratulations on this amazing milestone!"
    };
}
