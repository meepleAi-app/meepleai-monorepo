namespace Api.BoundedContexts.UserNotifications.Application.Services;

/// <summary>
/// Service for generating and validating email unsubscribe JWT tokens.
/// Issue #38: GDPR-compliant unsubscribe.
/// </summary>
internal interface IUnsubscribeTokenService
{
    string GenerateToken(Guid userId, string notificationType);
    string GenerateUnsubscribeUrl(Guid userId, string notificationType);
}
