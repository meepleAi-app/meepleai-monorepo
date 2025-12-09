using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handles user login with email and password.
/// Returns session token or temp token if 2FA is required.
/// </summary>
public class LoginCommandHandler : ICommandHandler<LoginCommand, LoginResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly ITempSessionService _tempSessionService;
    private readonly IUnitOfWork _unitOfWork;

    public LoginCommandHandler(
        IUserRepository userRepository,
        ISessionRepository sessionRepository,
        ITempSessionService tempSessionService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _tempSessionService = tempSessionService ?? throw new ArgumentNullException(nameof(tempSessionService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<LoginResponse> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        // Find user by email
        var email = new Email(command.Email);
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);

        if (user == null)
            throw new DomainException("Invalid email or password");

        // Verify password
        if (!user.VerifyPassword(command.Password))
            throw new DomainException("Invalid email or password");

        // Check if 2FA is required
        if (user.RequiresTwoFactor())
        {
            // Create temp session for 2FA verification (5-min TTL, single-use)
            var tempSessionToken = await _tempSessionService.CreateTempSessionAsync(
                user.Id,
                command.IpAddress
            );

            return new LoginResponse(
                RequiresTwoFactor: true,
                TempSessionToken: tempSessionToken,
                User: null,
                SessionToken: null
            );
        }

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

        await _sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Map to DTO
        var userDto = MapToUserDto(user);

        return new LoginResponse(
            RequiresTwoFactor: false,
            TempSessionToken: null,
            User: userDto,
            SessionToken: sessionToken.Value
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
