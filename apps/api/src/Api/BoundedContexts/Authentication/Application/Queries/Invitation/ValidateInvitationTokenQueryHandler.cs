using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries.Invitation;

/// <summary>
/// Handles validation of invitation tokens with security-hardened uniform error responses.
/// Only two error reasons: "invalid" (expired/revoked/not-found) and "already_used" (accepted).
/// Issue #124: User invitation system.
/// </summary>
internal sealed class ValidateInvitationTokenQueryHandler
    : IQueryHandler<ValidateInvitationTokenQuery, InvitationValidationDto>
{
    private readonly IInvitationTokenRepository _invitationRepo;
    private readonly IUserRepository _userRepo;

    public ValidateInvitationTokenQueryHandler(
        IInvitationTokenRepository invitationRepo,
        IUserRepository userRepo)
    {
        _invitationRepo = invitationRepo ?? throw new ArgumentNullException(nameof(invitationRepo));
        _userRepo = userRepo ?? throw new ArgumentNullException(nameof(userRepo));
    }

    public async Task<InvitationValidationDto> Handle(
        ValidateInvitationTokenQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.Token))
            return new InvitationValidationDto(false, null, null, "invalid");

        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(query.Token)));
        var invitation = await _invitationRepo.GetByTokenHashAsync(tokenHash, cancellationToken).ConfigureAwait(false);

        // Not found → uniform "invalid"
        if (invitation == null)
            return new InvitationValidationDto(false, null, null, "invalid");

        // Accepted → "already_used" (allows redirect to login)
        if (invitation.Status == InvitationStatus.Accepted)
            return new InvitationValidationDto(false, null, null, "already_used");

        // Expired → uniform "invalid"
        if (invitation.Status == InvitationStatus.Expired || DateTime.UtcNow > invitation.ExpiresAt)
            return new InvitationValidationDto(false, null, null, "invalid");

        // Revoked → uniform "invalid"
        if (invitation.Status == InvitationStatus.Revoked)
            return new InvitationValidationDto(false, null, null, "invalid");

        // Valid: look up pending user to get DisplayName
        string? displayName = null;
        if (invitation.PendingUserId.HasValue)
        {
            var pendingUser = await _userRepo.GetByIdAsync(invitation.PendingUserId.Value, cancellationToken).ConfigureAwait(false);
            displayName = pendingUser?.DisplayName;
        }

        return new InvitationValidationDto(true, invitation.Email, displayName, null);
    }
}
