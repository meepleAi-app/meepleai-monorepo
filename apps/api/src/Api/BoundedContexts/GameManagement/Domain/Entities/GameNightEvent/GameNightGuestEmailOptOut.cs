using System.Net.Mail;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;

/// <summary>
/// An address that asked to stop receiving game night emails (#4846). A guest is an address, not an account,
/// so there is no notification preference page to turn them off from: the choice is recorded against the
/// address, and every send to a guest consults it.
/// </summary>
/// <remarks>
/// The address is kept trimmed and in lower case, the way invitations keep it, so the same guest invited under
/// two spellings is one entry. <see cref="SourceInvitationId"/> says which invitation the request came through;
/// it is a trace, not a reference, and the entry outlives that invitation.
/// </remarks>
internal sealed class GameNightGuestEmailOptOut : Entity<Guid>
{
    /// <summary>The longest address an email can have; the column holds exactly that (RFC 5321: 254 + room, aligned with invitations).</summary>
    public const int MaxEmailLength = 320;

    public string Email { get; private set; }
    public DateTimeOffset OptedOutAt { get; private set; }
    public Guid SourceInvitationId { get; private set; }

    private GameNightGuestEmailOptOut(Guid id, string email, DateTimeOffset optedOutAt, Guid sourceInvitationId)
        : base(id)
    {
        Email = email;
        OptedOutAt = optedOutAt;
        SourceInvitationId = sourceInvitationId;
    }

    /// <summary>
    /// The form an address is stored and looked up in: the bare address, trimmed, lower case. «Marco &lt;marco@x.com&gt;»
    /// is <c>marco@x.com</c>, because that is where the transport writes: compared as a string the two spellings would be
    /// two addresses, and an opt-out recorded for one would not stop the other.
    /// </summary>
    public static string Normalize(string email)
    {
        var trimmed = email.Trim();
        if (trimmed.Contains('<', StringComparison.Ordinal) && MailAddress.TryCreate(trimmed, out var parsed))
        {
            trimmed = parsed.Address;
        }

        return trimmed.ToLowerInvariant();
    }

    public static GameNightGuestEmailOptOut Create(string email, Guid sourceInvitationId, DateTimeOffset utcNow)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required", nameof(email));

        var normalized = Normalize(email);
        if (normalized.Length > MaxEmailLength)
            throw new ArgumentException($"Email cannot exceed {MaxEmailLength} characters", nameof(email));

        if (sourceInvitationId == Guid.Empty)
            throw new ArgumentException("SourceInvitationId is required", nameof(sourceInvitationId));

        return new GameNightGuestEmailOptOut(Guid.NewGuid(), normalized, utcNow, sourceInvitationId);
    }
}
