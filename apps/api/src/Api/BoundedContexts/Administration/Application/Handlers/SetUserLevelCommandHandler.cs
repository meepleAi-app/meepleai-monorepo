using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for SetUserLevelCommand.
/// Issue #3141: Allows admins to manually set user level.
/// </summary>
internal sealed class SetUserLevelCommandHandler
    : IRequestHandler<SetUserLevelCommand, UserDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<SetUserLevelCommandHandler> _logger;

    public SetUserLevelCommandHandler(
        MeepleAiDbContext dbContext,
        ILogger<SetUserLevelCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UserDto> Handle(
        SetUserLevelCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            throw new NotFoundException($"User {request.UserId} not found");
        }

        var oldLevel = user.Level;
        user.Level = request.Level;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "User {UserId} level changed from {OldLevel} to {NewLevel}",
            user.Id, oldLevel, request.Level);

        return new UserDto(
            Id: user.Id,
            Email: user.Email,
            DisplayName: user.DisplayName ?? string.Empty,
            Role: user.Role,
            Tier: user.Tier,
            CreatedAt: user.CreatedAt,
            IsTwoFactorEnabled: user.IsTwoFactorEnabled,
            TwoFactorEnabledAt: user.TwoFactorEnabledAt,
            Level: user.Level,
            ExperiencePoints: user.ExperiencePoints
        );
    }
}
