using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

/// <summary>
/// Handles sending an invitation email to a new user.
/// Generates a secure token, creates the invitation record, and dispatches an email.
/// Issue #124: User invitation system.
/// </summary>
internal sealed class SendInvitationCommandHandler : ICommandHandler<SendInvitationCommand, InvitationDto>
{
    private static readonly string[] AllowedRoles = { "User", "Editor", "Admin" };

    private readonly IInvitationTokenRepository _invitationRepo;
    private readonly IUserRepository _userRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly ILogger<SendInvitationCommandHandler> _logger;

    public SendInvitationCommandHandler(
        IInvitationTokenRepository invitationRepo,
        IUserRepository userRepo,
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        ILogger<SendInvitationCommandHandler> logger)
    {
        _invitationRepo = invitationRepo ?? throw new ArgumentNullException(nameof(invitationRepo));
        _userRepo = userRepo ?? throw new ArgumentNullException(nameof(userRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InvitationDto> Handle(SendInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var normalizedEmail = command.Email.Trim().ToLowerInvariant();

        // Validate role
        if (!AllowedRoles.Contains(command.Role, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Role '{command.Role}' is not allowed for invitation. Allowed: {string.Join(", ", AllowedRoles)}", nameof(command));

        // Check for existing pending invitation
        var existingInvitation = await _invitationRepo.GetPendingByEmailAsync(normalizedEmail, cancellationToken).ConfigureAwait(false);
        if (existingInvitation != null)
            throw new ConflictException($"A pending invitation already exists for '{normalizedEmail}'");

        // Check if user already exists
        var email = new Api.BoundedContexts.Authentication.Domain.ValueObjects.Email(normalizedEmail);
        var existingUser = await _userRepo.ExistsByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (existingUser)
            throw new ConflictException($"A user with email '{normalizedEmail}' already exists");

        // Generate secure token
        var rawTokenBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(rawTokenBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('='); // URL-safe Base64
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        // Create invitation
        var invitation = InvitationToken.Create(normalizedEmail, command.Role, tokenHash, command.InvitedByUserId);

        await _invitationRepo.AddAsync(invitation, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Send email (fire-and-forget pattern — log errors but don't fail the operation)
        try
        {
            var inviteLink = $"/accept-invite?token={rawToken}";
            await _emailService.SendInvitationEmailAsync(
                normalizedEmail,
                command.Role,
                inviteLink,
                "Admin",
                cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send invitation email to {Email}", normalizedEmail);
        }
#pragma warning restore CA1031

        return MapToDto(invitation);
    }

    internal static InvitationDto MapToDto(InvitationToken invitation)
    {
        return new InvitationDto(
            Id: invitation.Id,
            Email: invitation.Email,
            Role: invitation.Role,
            Status: invitation.Status.ToString(),
            ExpiresAt: invitation.ExpiresAt,
            CreatedAt: invitation.CreatedAt,
            AcceptedAt: invitation.AcceptedAt,
            InvitedByUserId: invitation.InvitedByUserId);
    }
}
