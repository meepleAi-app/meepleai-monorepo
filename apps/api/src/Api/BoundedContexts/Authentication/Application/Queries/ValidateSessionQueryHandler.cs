using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Handles session validation and returns user information if valid.
/// </summary>
public class ValidateSessionQueryHandler : IQueryHandler<ValidateSessionQuery, SessionStatusDto>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUserRepository _userRepository;
    private readonly TimeProvider _timeProvider;

    public ValidateSessionQueryHandler(
        ISessionRepository sessionRepository,
        IUserRepository userRepository,
        TimeProvider timeProvider)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<SessionStatusDto> Handle(ValidateSessionQuery query, CancellationToken cancellationToken)
    {
        // Parse and hash the token
        var sessionToken = SessionToken.FromStored(query.SessionToken);
        var tokenHash = sessionToken.ComputeHash();

        // Find session by token hash
        var session = await _sessionRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (session == null)
        {
            return new SessionStatusDto(
                IsValid: false,
                User: null,
                ExpiresAt: null,
                LastSeenAt: null
            );
        }

        // Check if session is valid
        if (!session.IsValid(_timeProvider))
        {
            return new SessionStatusDto(
                IsValid: false,
                User: null,
                ExpiresAt: session.ExpiresAt,
                LastSeenAt: session.LastSeenAt
            );
        }

        // Update last seen timestamp
        session.UpdateLastSeen(_timeProvider);
        var lastSeenAt = session.LastSeenAt ?? _timeProvider.GetUtcNow().UtcDateTime;
        await _sessionRepository.UpdateLastSeenAsync(session.Id, lastSeenAt, cancellationToken);

        // Get user information
        var user = await _userRepository.GetByIdAsync(session.UserId, cancellationToken);

        if (user == null)
        {
            return new SessionStatusDto(
                IsValid: false,
                User: null,
                ExpiresAt: null,
                LastSeenAt: null
            );
        }

        var userDto = MapToUserDto(user);

        return new SessionStatusDto(
            IsValid: true,
            User: userDto,
            ExpiresAt: session.ExpiresAt,
            LastSeenAt: session.LastSeenAt
        );
    }

    private static UserDto MapToUserDto(User user)
    {
        return new UserDto(
            Id: user.Id,
            Email: user.Email.Value,
            DisplayName: user.DisplayName,
            Role: user.Role.Value,
            CreatedAt: user.CreatedAt,
            IsTwoFactorEnabled: user.IsTwoFactorEnabled,
            TwoFactorEnabledAt: user.TwoFactorEnabledAt
        );
    }
}