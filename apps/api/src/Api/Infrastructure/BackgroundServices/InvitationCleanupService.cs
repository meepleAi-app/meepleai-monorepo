using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that periodically cleans up expired pending invitations.
/// Runs every hour: finds users with Status=Pending whose InvitationExpiresAt has passed,
/// marks the associated InvitationToken as expired, and hard-deletes the pending user.
/// </summary>
internal sealed class InvitationCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<InvitationCleanupService> _logger;

    public InvitationCleanupService(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<InvitationCleanupService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "InvitationCleanupService started. Cleanup interval: {Interval} hour(s)",
            Interval.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);

#pragma warning disable CA1031 // Do not catch general exception types
            // BACKGROUND SERVICE: Generic catch prevents service from crashing the host process.
            try
            {
                await CleanupExpiredInvitationsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unexpected error during invitation cleanup cycle");
            }
#pragma warning restore CA1031
        }

        _logger.LogInformation("InvitationCleanupService stopped");
    }

    internal async Task CleanupExpiredInvitationsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var invitationRepo = scope.ServiceProvider.GetRequiredService<IInvitationTokenRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Find pending users whose invitation has expired via EF Core entities
        var expiredPendingUsers = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Status == "Pending" && u.InvitationExpiresAt != null && u.InvitationExpiresAt < now)
            .Select(u => new { u.Id, u.Email })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (expiredPendingUsers.Count == 0)
        {
            _logger.LogDebug("Invitation cleanup: no expired pending users found");
            return;
        }

        _logger.LogInformation(
            "Invitation cleanup starting for {Count} expired pending user(s)",
            expiredPendingUsers.Count);

        var cleanedCount = 0;

        foreach (var expiredUser in expiredPendingUsers)
        {
#pragma warning disable CA1031 // Do not catch general exception types
            // BACKGROUND SERVICE: Failure for one user must not block cleanup for others.
            try
            {
                // Find associated invitation token via PendingUserId
                var invitationEntity = await dbContext.InvitationTokens
                    .FirstOrDefaultAsync(t => t.PendingUserId == expiredUser.Id && t.Status == "Pending", ct)
                    .ConfigureAwait(false);

                if (invitationEntity is not null)
                {
                    // Load domain entity and mark expired
                    var invitation = await invitationRepo
                        .GetByIdAsync(invitationEntity.Id, ct)
                        .ConfigureAwait(false);

                    if (invitation is not null)
                    {
                        invitation.MarkExpired();
                        await invitationRepo.UpdateAsync(invitation, ct).ConfigureAwait(false);
                    }
                }

                // Hard delete the pending user
                var user = await userRepo.GetByIdAsync(expiredUser.Id, ct).ConfigureAwait(false);
                if (user is not null)
                {
                    await userRepo.DeleteAsync(user, ct).ConfigureAwait(false);
                }

                await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
                cleanedCount++;

                _logger.LogInformation(
                    "Cleaned up expired pending user {UserId} ({Email})",
                    expiredUser.Id, expiredUser.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Invitation cleanup failed for user {UserId}. Continuing with remaining users",
                    expiredUser.Id);
            }
#pragma warning restore CA1031
        }

        _logger.LogInformation(
            "Invitation cleanup completed: {CleanedCount}/{TotalCount} users cleaned up",
            cleanedCount, expiredPendingUsers.Count);
    }
}
