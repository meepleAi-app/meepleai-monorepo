using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handles session creation for an authenticated user.
/// Used after OAuth callback or 2FA verification.
/// </summary>
public class CreateSessionCommandHandler : ICommandHandler<CreateSessionCommand, CreateSessionResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CreateSessionCommandHandler(
        IUserRepository userRepository,
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<CreateSessionResponse> Handle(CreateSessionCommand command, CancellationToken cancellationToken)
    {
        // Get user by ID
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken);

        if (user == null)
            throw new DomainException($"User with ID {command.UserId} not found");

        // Create session
        var sessionId = Guid.NewGuid();
        var sessionToken = SessionToken.Generate();
        var session = new Session(
            id: sessionId,
            userId: user.Id,
            token: sessionToken,
            ipAddress: command.IpAddress,
            userAgent: command.UserAgent
        );

        await _sessionRepository.AddAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Map to DTO
        var userDto = MapToUserDto(user);

        return new CreateSessionResponse(
            User: userDto,
            SessionToken: sessionToken.Value,
            ExpiresAt: session.ExpiresAt
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
