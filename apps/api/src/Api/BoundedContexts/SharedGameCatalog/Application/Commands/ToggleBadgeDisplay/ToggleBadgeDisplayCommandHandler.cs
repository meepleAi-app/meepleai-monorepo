using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.ToggleBadgeDisplay;

/// <summary>
/// Handler for toggling the display visibility of a user's badge.
/// Issue #2736: API - Badge Endpoints
/// </summary>
internal sealed class ToggleBadgeDisplayCommandHandler : ICommandHandler<ToggleBadgeDisplayCommand>
{
    private readonly MeepleAiDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ToggleBadgeDisplayCommandHandler> _logger;

    public ToggleBadgeDisplayCommandHandler(
        MeepleAiDbContext context,
        IUnitOfWork unitOfWork,
        ILogger<ToggleBadgeDisplayCommandHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        ToggleBadgeDisplayCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Toggling badge display for UserBadge {UserBadgeId} (User: {UserId}, IsDisplayed: {IsDisplayed})",
            command.UserBadgeId,
            command.UserId,
            command.IsDisplayed);

        var userBadge = await _context.Set<UserBadgeEntity>()
            .Where(ub => ub.Id == command.UserBadgeId && ub.UserId == command.UserId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (userBadge is null)
        {
            throw new NotFoundException(
                "UserBadge",
                command.UserBadgeId.ToString());
        }

        if (userBadge.RevokedAt is not null)
        {
            throw new ConflictException(
                $"Cannot modify display settings for revoked badge {command.UserBadgeId}");
        }

        userBadge.IsDisplayed = command.IsDisplayed;

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Badge display toggled successfully for UserBadge {UserBadgeId}",
            command.UserBadgeId);
    }
}
