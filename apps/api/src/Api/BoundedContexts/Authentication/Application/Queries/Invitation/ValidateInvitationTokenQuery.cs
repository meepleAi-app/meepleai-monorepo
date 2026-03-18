using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries.Invitation;

/// <summary>
/// Query to validate an invitation token with security-hardened uniform error responses.
/// Returns IsValid with email/displayName on success, or a uniform error reason on failure.
/// Security: only "invalid" and "already_used" error reasons to prevent state enumeration.
/// Issue #124: User invitation system.
/// </summary>
internal record ValidateInvitationTokenQuery(string Token) : IQuery<InvitationValidationDto>;
