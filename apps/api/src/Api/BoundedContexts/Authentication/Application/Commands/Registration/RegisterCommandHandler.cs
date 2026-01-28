using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Guards;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handles user registration with email and password.
/// Creates user and immediately authenticates with session token.
/// </summary>
internal class RegisterCommandHandler : ICommandHandler<RegisterCommand, RegisterResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        IEmailVerificationService emailVerificationService,
        ILogger<RegisterCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _emailVerificationService = emailVerificationService ?? throw new ArgumentNullException(nameof(emailVerificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RegisterResponse> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Validate input before domain operations
        Guard.AgainstNullOrWhiteSpace(command.DisplayName, nameof(command.DisplayName));
        Guard.AgainstNullOrWhiteSpace(command.Password, nameof(command.Password));
        Guard.AgainstTooShort(command.Password, nameof(command.Password), 8);

        // Validate and create email
        var email = new Email(command.Email);

        // Check if email already exists
        var existingUser = await _userRepository.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (existingUser != null)
            throw new DomainException("Email is already registered");

        // Determine role (first user is admin, others are user unless specified)
        var hasAnyUsers = await _userRepository.HasAnyUsersAsync(cancellationToken).ConfigureAwait(false);
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
        await _userRepository.AddAsync(user, cancellationToken).ConfigureAwait(false);
        await _sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // ISSUE-3071: Send verification email after successful registration
        // This is fire-and-forget - email failures should not fail registration
        try
        {
            await _emailVerificationService.SendVerificationEmailAsync(
                userId,
                email.Value,
                command.DisplayName.Trim(),
                cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // Log but don't fail registration - user can request resend
            _logger.LogWarning(ex, "Failed to send verification email for user {UserId}", userId);
        }
#pragma warning restore CA1031

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
            Tier: user.Tier.Value,
            CreatedAt: user.CreatedAt,
            IsTwoFactorEnabled: user.IsTwoFactorEnabled,
            TwoFactorEnabledAt: user.TwoFactorEnabledAt
        );
    }
}
