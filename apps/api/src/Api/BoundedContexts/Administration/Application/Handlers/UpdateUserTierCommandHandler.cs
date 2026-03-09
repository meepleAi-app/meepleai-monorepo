using Api.BoundedContexts.Administration.Application.Commands;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for UpdateUserTierCommand.
/// Updates a user's subscription tier (admin-only operation).
/// </summary>
internal class UpdateUserTierCommandHandler : ICommandHandler<UpdateUserTierCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<UpdateUserTierCommandHandler> _logger;

    public UpdateUserTierCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        MeepleAiDbContext dbContext,
        ILogger<UpdateUserTierCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UserDto> Handle(UpdateUserTierCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command);
        // Authorization: While endpoint checks admin role, we verify again for defense in depth
        // and to ensure handler can be safely called from any context
        var requester = await _userRepository.GetByIdAsync(command.RequesterUserId, cancellationToken).ConfigureAwait(false);
        if (requester == null)
            throw new DomainException("Requester not found");

        if (!requester.Role.IsAdmin())
            throw new DomainException("Only administrators can change user tiers");

        // Get target user
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        if (user == null)
            throw new DomainException($"User {command.UserId} not found");

        // Parse tier with explicit error handling
        UserTier newTier;
        try
        {
            newTier = UserTier.Parse(command.NewTier);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Invalid tier value '{Tier}' for user {UserId}", command.NewTier, command.UserId);
            throw new DomainException($"Invalid tier value '{command.NewTier}'. Valid tiers are: free, normal, premium", ex);
        }

        var oldTier = user.Tier;
        user.UpdateTier(newTier, requester.Role);

        // Persist updates - required because repository uses AsNoTracking
        await _userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Admin {AdminId} changed tier for user {UserId} from {OldTier} to {NewTier}",
            command.RequesterUserId,
            command.UserId,
            oldTier.Value,
            newTier.Value);

        // Fetch LastSeenAt from active sessions
        var lastSeenAt = await _dbContext.UserSessions
            .Where(s => s.UserId == command.UserId && s.RevokedAt == null)
            .OrderByDescending(s => s.LastSeenAt ?? s.CreatedAt)
            .Select(s => s.LastSeenAt)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return new UserDto(
            Id: user.Id.ToString(),
            Email: user.Email.Value,
            DisplayName: user.DisplayName,
            Role: user.Role.Value,
            CreatedAt: user.CreatedAt,
            LastSeenAt: lastSeenAt
        );
    }
}
