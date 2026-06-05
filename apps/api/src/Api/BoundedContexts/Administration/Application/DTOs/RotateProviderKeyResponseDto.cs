namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Response payload returned by a successful rotation. <c>NewKeyFingerprint</c> is the masked
/// first5..last4 form — the plaintext is never exposed to the client (no "show once" — issue
/// #1859 design §2 D-5). <c>PreviousKeyDisabledAt</c> equals <c>RotatedAt</c> in the current
/// design (deactivate + insert happen in the same transaction).
/// Issue #1859.
/// </summary>
public sealed record RotateProviderKeyResponseDto(
    string ProviderName,
    string NewKeyFingerprint,
    DateTime RotatedAt,
    DateTime PreviousKeyDisabledAt);
