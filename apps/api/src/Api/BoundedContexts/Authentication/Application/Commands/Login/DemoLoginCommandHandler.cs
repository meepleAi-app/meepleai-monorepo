using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handles demo user login without password validation.
/// Only works for users marked as demo accounts (IsDemoAccount = true).
/// Security: Demo sessions are limited to 1 hour (vs 30 days for regular sessions).
/// </summary>
public class DemoLoginCommandHandler : ICommandHandler<DemoLoginCommand, LoginResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DemoLoginCommandHandler(
        IUserRepository userRepository,
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<LoginResponse> Handle(DemoLoginCommand command, CancellationToken cancellationToken)
    {
        // Find user by email
        var email = new Email(command.Email);
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (user == null)
            throw new DomainException("Demo account not found");

        // Security: Only allow demo login for accounts marked as demo
        if (!user.IsDemoAccount)
            throw new DomainException("This account is not a demo account. Please use regular login.");

        // Note: 2FA is intentionally bypassed for demo accounts to simplify demonstration
        // In production, consider whether demo accounts should support 2FA

        // Create session with 1-hour lifetime (demo sessions are short-lived for security)
        var sessionId = Guid.NewGuid();
        var sessionToken = SessionToken.Generate();
        var demoSessionLifetime = TimeSpan.FromHours(1);
        var session = new Session(
            id: sessionId,
            userId: user.Id,
            token: sessionToken,
            lifetime: demoSessionLifetime,
            ipAddress: command.IpAddress,
            userAgent: command.UserAgent
        );

        await _sessionRepository.AddAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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