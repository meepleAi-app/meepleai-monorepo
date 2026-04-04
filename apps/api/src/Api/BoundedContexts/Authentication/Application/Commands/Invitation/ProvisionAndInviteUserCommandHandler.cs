using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure.Security;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

/// <summary>
/// Handles provisioning a pending user and creating an invitation token in one operation.
/// Creates both the User (Pending status) and InvitationToken entities, then sends the invitation email.
/// Issue #124: Admin invitation flow.
/// </summary>
internal sealed class ProvisionAndInviteUserCommandHandler : ICommandHandler<ProvisionAndInviteUserCommand, InvitationDto>
{
    private readonly IUserRepository _userRepo;
    private readonly IInvitationTokenRepository _invitationRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProvisionAndInviteUserCommandHandler> _logger;

    public ProvisionAndInviteUserCommandHandler(
        IUserRepository userRepo,
        IInvitationTokenRepository invitationRepo,
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        TimeProvider timeProvider,
        ILogger<ProvisionAndInviteUserCommandHandler> logger)
    {
        _userRepo = userRepo ?? throw new ArgumentNullException(nameof(userRepo));
        _invitationRepo = invitationRepo ?? throw new ArgumentNullException(nameof(invitationRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InvitationDto> Handle(ProvisionAndInviteUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var normalizedEmail = command.Email.Trim().ToLowerInvariant();

        // 1. Check email uniqueness — no existing user with this email
        var email = new Email(normalizedEmail);
        var existingUser = await _userRepo.ExistsByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (existingUser)
            throw new ConflictException($"A user with email '{normalizedEmail}' already exists");

        // 2. Check no pending invitation exists for this email
        var existingInvitation = await _invitationRepo.GetPendingByEmailAsync(normalizedEmail, cancellationToken).ConfigureAwait(false);
        if (existingInvitation != null)
            throw new ConflictException($"A pending invitation already exists for '{normalizedEmail}'");

        // 3. Generate secure token
        var rawTokenBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(rawTokenBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('='); // URL-safe Base64
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        // 4. Create pending user
        var role = Role.Parse(command.Role);
        var tier = UserTier.Parse(command.Tier);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddDays(command.ExpiresInDays);

        var pendingUser = User.CreatePending(
            email,
            command.DisplayName,
            role,
            tier,
            command.InvitedByUserId,
            expiresAt,
            _timeProvider);

        // 5. Create invitation token
        var invitation = InvitationToken.Create(
            email,
            role,
            pendingUser.Id,
            command.CustomMessage,
            command.ExpiresInDays,
            _timeProvider,
            tokenHash,
            command.InvitedByUserId);

        // 6. Add game suggestions to token
        if (command.GameSuggestions is { Count: > 0 })
        {
            foreach (var suggestion in command.GameSuggestions)
            {
                var suggestionType = Enum.Parse<GameSuggestionType>(suggestion.Type, ignoreCase: true);
                invitation.AddGameSuggestion(suggestion.GameId, suggestionType);
            }
        }

        // 7. Save user + token via repositories + IUnitOfWork.SaveChangesAsync()
        await _userRepo.AddAsync(pendingUser, cancellationToken).ConfigureAwait(false);
        await _invitationRepo.AddAsync(invitation, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 8. Try send email (fire-and-forget pattern): catch exceptions, log, set emailSent = false
        var emailSent = true;
        try
        {
            await _emailService.SendInvitationEmailAsync(
                normalizedEmail,
                command.Role,
                rawToken,
                command.DisplayName,
                cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send invitation email to {Email}", DataMasking.MaskEmail(normalizedEmail));
            emailSent = false;
        }
#pragma warning restore CA1031

        // 9. Return InvitationDto with EmailSent status
        return MapToDto(invitation, emailSent);
    }

    private static InvitationDto MapToDto(InvitationToken invitation, bool emailSent)
    {
        var gameSuggestions = invitation.GameSuggestions
            .Select(gs => new GameSuggestionDto(gs.GameId, gs.Type.ToString()))
            .ToList();

        return new InvitationDto(
            Id: invitation.Id,
            Email: invitation.Email,
            Role: invitation.Role,
            Status: invitation.Status.ToString(),
            ExpiresAt: invitation.ExpiresAt,
            CreatedAt: invitation.CreatedAt,
            AcceptedAt: invitation.AcceptedAt,
            InvitedByUserId: invitation.InvitedByUserId,
            EmailSent: emailSent,
            GameSuggestions: gameSuggestions);
    }
}
