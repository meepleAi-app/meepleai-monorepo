using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.Authentication.Infrastructure.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Enums;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

/// <summary>
/// Handles activation of an invited user account (two-phase).
/// Phase 1 (sync): Validates token, sets password, activates user, creates session.
/// Phase 2 (async): Dispatches game suggestions to Channel for background processing.
/// </summary>
internal sealed class ActivateInvitedAccountCommandHandler
    : ICommandHandler<ActivateInvitedAccountCommand, ActivationResultDto>
{
    private readonly IInvitationTokenRepository _invitationRepo;
    private readonly IUserRepository _userRepo;
    private readonly ISessionRepository _sessionRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GameSuggestionChannel _gameSuggestionChannel;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ActivateInvitedAccountCommandHandler> _logger;

    public ActivateInvitedAccountCommandHandler(
        IInvitationTokenRepository invitationRepo,
        IUserRepository userRepo,
        ISessionRepository sessionRepo,
        IUnitOfWork unitOfWork,
        GameSuggestionChannel gameSuggestionChannel,
        TimeProvider timeProvider,
        ILogger<ActivateInvitedAccountCommandHandler> logger)
    {
        _invitationRepo = invitationRepo ?? throw new ArgumentNullException(nameof(invitationRepo));
        _userRepo = userRepo ?? throw new ArgumentNullException(nameof(userRepo));
        _sessionRepo = sessionRepo ?? throw new ArgumentNullException(nameof(sessionRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _gameSuggestionChannel = gameSuggestionChannel ?? throw new ArgumentNullException(nameof(gameSuggestionChannel));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ActivationResultDto> Handle(
        ActivateInvitedAccountCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // ── Phase 1: Synchronous, atomic transaction ──────────────────────

        // 1. Hash the raw token (same SHA-256 pattern as AcceptInvitationCommandHandler)
        var tokenHash = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(command.Token)));

        // 2. Look up InvitationToken by hash
        var invitation = await _invitationRepo
            .GetByTokenHashAsync(tokenHash, cancellationToken)
            .ConfigureAwait(false);

        // 3. Validate: token exists and is still valid (pending + not expired)
        if (invitation is null || !invitation.IsValid)
            throw new NotFoundException("InvitationToken", command.Token);

        // 4. Find the pre-provisioned pending user
        if (invitation.PendingUserId is null)
            throw new NotFoundException("InvitationToken", "PendingUserId is not set on this invitation");

        var user = await _userRepo
            .GetByIdAsync(invitation.PendingUserId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
            throw new NotFoundException("User", invitation.PendingUserId.Value.ToString());

        // 5. Validate: user must be in Pending status
        if (user.Status != UserAccountStatus.Pending)
            throw new ConflictException($"User '{user.Email.Value}' is not in Pending status");

        // 6. Hash the password
        var passwordHash = PasswordHash.Create(command.Password);

        // 7. Activate user (sets password, status → Active, email verified)
        user.ActivateFromInvitation(passwordHash, _timeProvider);

        // 8. Mark token as accepted
        invitation.MarkAccepted(user.Id);

        // 9. Create session for immediate login
        var sessionToken = SessionToken.Generate();
        var session = new Session(
            id: Guid.NewGuid(),
            userId: user.Id,
            token: sessionToken,
            timeProvider: _timeProvider);

        // 10. Persist all changes atomically
        await _userRepo.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await _invitationRepo.UpdateAsync(invitation, cancellationToken).ConfigureAwait(false);
        await _sessionRepo.AddAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "User {UserId} activated from invitation {InvitationId}",
            user.Id, invitation.Id);

        // ── Phase 2: Async, fire-and-forget game suggestion dispatch ──────

        if (invitation.GameSuggestions.Count > 0)
        {
            var invitedByUserId = invitation.InvitedByUserId != Guid.Empty
                ? invitation.InvitedByUserId
                : user.InvitedByUserId ?? Guid.Empty;

            try
            {
                await _gameSuggestionChannel.Writer.WriteAsync(
                    new GameSuggestionEvent(
                        user.Id,
                        invitedByUserId,
                        invitation.GameSuggestions),
                    cancellationToken).ConfigureAwait(false);

                _logger.LogInformation(
                    "Dispatched {Count} game suggestions for user {UserId}",
                    invitation.GameSuggestions.Count, user.Id);
            }
#pragma warning disable CA1031 // Fire-and-forget: don't let channel errors roll back activation
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to dispatch game suggestions for user {UserId}", user.Id);
            }
#pragma warning restore CA1031
        }

        // 11. Return result
        return new ActivationResultDto(
            SessionToken: sessionToken.Value,
            RequiresOnboarding: true);
    }
}
