using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

/// <summary>
/// Handles acceptance of an invitation token.
/// Creates user account, marks invitation as accepted, and creates session for immediate login.
/// Issue #124: User invitation system.
/// </summary>
internal sealed class AcceptInvitationCommandHandler : ICommandHandler<AcceptInvitationCommand, AcceptInvitationResponse>
{
    private readonly IInvitationTokenRepository _invitationRepo;
    private readonly IUserRepository _userRepo;
    private readonly ISessionRepository _sessionRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AcceptInvitationCommandHandler> _logger;

    public AcceptInvitationCommandHandler(
        IInvitationTokenRepository invitationRepo,
        IUserRepository userRepo,
        ISessionRepository sessionRepo,
        IUnitOfWork unitOfWork,
        ILogger<AcceptInvitationCommandHandler> logger)
    {
        _invitationRepo = invitationRepo ?? throw new ArgumentNullException(nameof(invitationRepo));
        _userRepo = userRepo ?? throw new ArgumentNullException(nameof(userRepo));
        _sessionRepo = sessionRepo ?? throw new ArgumentNullException(nameof(sessionRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AcceptInvitationResponse> Handle(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Validate passwords match
        if (!string.Equals(command.Password, command.ConfirmPassword, StringComparison.Ordinal))
            throw new ArgumentException("Password and confirmation password do not match", nameof(command));

        // Hash token and lookup invitation
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(command.Token)));
        var invitation = await _invitationRepo.GetByTokenHashAsync(tokenHash, cancellationToken).ConfigureAwait(false);

        if (invitation == null || !invitation.IsValid)
            throw new NotFoundException("InvitationToken", command.Token);

        // Create user
        var email = new Email(invitation.Email);
        var role = Role.Parse(invitation.Role);
        var passwordHash = PasswordHash.Create(command.Password);
        var userId = Guid.NewGuid();
        // Sanitize display name: strip non-alphanumeric chars except dots/hyphens/underscores, max 50 chars
        var rawName = email.Value.Split('@')[0];
        var displayName = Regex.Replace(rawName, @"[^a-zA-Z0-9._\-]", "", RegexOptions.None, TimeSpan.FromSeconds(1));
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = "User";
        if (displayName.Length > 50)
            displayName = displayName[..50];

        var user = new User(
            id: userId,
            email: email,
            displayName: displayName,
            passwordHash: passwordHash,
            role: role);

        // Email is trusted (admin-supplied), mark as verified
        user.VerifyEmail();

        // Mark invitation as accepted
        invitation.MarkAccepted(userId);

        // Create session for immediate login
        var sessionToken = SessionToken.Generate();
        var session = new Session(
            id: Guid.NewGuid(),
            userId: userId,
            token: sessionToken);

        // Persist all changes
        await _userRepo.AddAsync(user, cancellationToken).ConfigureAwait(false);
        await _invitationRepo.UpdateAsync(invitation, cancellationToken).ConfigureAwait(false);
        await _sessionRepo.AddAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("User {UserId} created from invitation {InvitationId}", userId, invitation.Id);

        var userDto = MapToUserDto(user);
        return new AcceptInvitationResponse(
            User: userDto,
            SessionToken: sessionToken.Value,
            ExpiresAt: session.ExpiresAt);
    }

    private static UserDto MapToUserDto(User user)
    {
        return new UserDto(
            Id: user.Id,
            Email: user.Email.Value,
            DisplayName: user.DisplayName,
            Role: user.Role.Value,
            Tier: user.Tier.Value,
            CreatedAt: user.CreatedAt,
            IsTwoFactorEnabled: user.IsTwoFactorEnabled,
            TwoFactorEnabledAt: user.TwoFactorEnabledAt,
            Level: user.Level,
            ExperiencePoints: user.ExperiencePoints,
            EmailVerified: user.EmailVerified,
            EmailVerifiedAt: user.EmailVerifiedAt,
            VerificationGracePeriodEndsAt: user.VerificationGracePeriodEndsAt,
            OnboardingCompleted: user.OnboardingCompleted,
            OnboardingSkipped: user.OnboardingSkipped);
    }
}
