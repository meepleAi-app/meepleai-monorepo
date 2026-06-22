namespace Api.BoundedContexts.SystemConfiguration.Application.DTOs;

/// <summary>
/// Issue #1089: Public-facing status banner payload.
/// MessageId is a deterministic hash of (Message, Severity, UpdatedAt) used
/// by the frontend to remember per-banner dismissals across sessions.
/// </summary>
public sealed record PublicStatusBannerResponse(
    Guid MessageId,
    string Message,
    string Severity,
    DateTime UpdatedAt);
