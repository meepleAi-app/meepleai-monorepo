using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries.Invitation;

/// <summary>
/// Handles validation of invitation tokens.
/// Hashes the raw token and looks up the invitation record.
/// Issue #124: User invitation system.
/// </summary>
internal sealed class ValidateInvitationTokenQueryHandler
    : IQueryHandler<ValidateInvitationTokenQuery, ValidateInvitationTokenResponse>
{
    private readonly IInvitationTokenRepository _invitationRepo;

    public ValidateInvitationTokenQueryHandler(IInvitationTokenRepository invitationRepo)
    {
        _invitationRepo = invitationRepo ?? throw new ArgumentNullException(nameof(invitationRepo));
    }

    public async Task<ValidateInvitationTokenResponse> Handle(
        ValidateInvitationTokenQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.Token))
            return new ValidateInvitationTokenResponse(false, null, null);

        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(query.Token)));
        var invitation = await _invitationRepo.GetByTokenHashAsync(tokenHash, cancellationToken).ConfigureAwait(false);

        if (invitation == null || !invitation.IsValid)
            return new ValidateInvitationTokenResponse(false, null, null);

        return new ValidateInvitationTokenResponse(
            Valid: true,
            Role: invitation.Role,
            ExpiresAt: invitation.ExpiresAt);
    }
}
