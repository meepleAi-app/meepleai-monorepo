using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handles user registration with email and password.
/// Creates user and immediately authenticates with session token.
/// </summary>
public class RegisterCommandHandler : ICommandHandler<RegisterCommand, RegisterResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _userRepository = userRepository;
        _sessionRepository = sessionRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<RegisterResponse> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        // Validate and create email
        var email = new Email(command.Email);

        // Check if email already exists
        var existingUser = await _userRepository.GetByEmailAsync(email, cancellationToken);
        if (existingUser != null)
            throw new DomainException("Email is already registered");

        // Determine role (first user is admin, others are user unless specified)
        var hasAnyUsers = await _userRepository.HasAnyUsersAsync(cancellationToken);
        Role role;

        if (!hasAnyUsers)
        {
            // First user becomes admin
            role = string.IsNullOrWhiteSpace(command.Role)
                ? Role.Admin
                : Role.Parse(command.Role);
        }
        else
        {
            // Subsequent users default to User role
            // Only admins can assign elevated roles (enforced at endpoint level)
            role = string.IsNullOrWhiteSpace(command.Role)
                ? Role.User
                : Role.Parse(command.Role);

            // Prevent non-admins from self-assigning elevated roles
            if (role.IsAdmin() || role.IsEditor())
            {
                throw new DomainException("Only administrators can assign elevated roles");
            }
        }

        // Create password hash
        var passwordHash = PasswordHash.Create(command.Password);

        // Create user
        var userId = Guid.NewGuid();
        var user = new User(
            id: userId,
            email: email,
            displayName: command.DisplayName.Trim(),
            passwordHash: passwordHash,
            role: role
        );

        // Create session for immediate authentication
        var sessionId = Guid.NewGuid();
        var sessionToken = SessionToken.Generate();
        var session = new Session(
            id: sessionId,
            userId: userId,
            token: sessionToken,
            ipAddress: command.IpAddress,
            userAgent: command.UserAgent
        );

        // Save user and session
        await _userRepository.AddAsync(user, cancellationToken);
        await _sessionRepository.AddAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Map to DTO
        var userDto = MapToUserDto(user);

        return new RegisterResponse(
            User: userDto,
            SessionToken: sessionToken.Value,
            ExpiresAt: session.ExpiresAt
        );
    }

    private static Api.BoundedContexts.Authentication.Application.DTOs.UserDto MapToUserDto(User user)
    {
        return new Api.BoundedContexts.Authentication.Application.DTOs.UserDto(
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
