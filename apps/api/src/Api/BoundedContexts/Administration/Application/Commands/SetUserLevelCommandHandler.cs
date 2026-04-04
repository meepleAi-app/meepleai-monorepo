using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Handler for SetUserLevelCommand.
/// Issue #3141: Allows admins to manually set user level.
/// </summary>
internal sealed class SetUserLevelCommandHandler
    : IRequestHandler<SetUserLevelCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<SetUserLevelCommandHandler> _logger;

    public SetUserLevelCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        MeepleAiDbContext dbContext,
        ILogger<SetUserLevelCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UserDto> Handle(
        SetUserLevelCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Get user from repository (returns domain entity)
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            throw new NotFoundException("User", request.UserId.ToString());
        }

        var oldLevel = user.Level;

        // Call domain method which validates and emits domain event
        user.SetLevel(request.Level);

        // Persist updates
        await _userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "User {UserId} level changed from {OldLevel} to {NewLevel}",
            user.Id, oldLevel, request.Level);

        // Fetch additional data for DTO
        var userEntity = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            .ConfigureAwait(false);

        return new UserDto(
            Id: request.UserId,
            Email: user.Email.Value,
            DisplayName: user.DisplayName ?? string.Empty,
            Role: user.Role.Value,
            Tier: user.Tier.Value,
            CreatedAt: user.CreatedAt,
            IsTwoFactorEnabled: userEntity?.IsTwoFactorEnabled ?? false,
            TwoFactorEnabledAt: userEntity?.TwoFactorEnabledAt,
            Level: user.Level,
            ExperiencePoints: user.ExperiencePoints
        );
    }
}
