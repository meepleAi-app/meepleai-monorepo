using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Query to retrieve the VAPID public key for Web Push subscriptions.
/// Issue #4416: Push notification subscription management.
/// </summary>
internal record GetVapidPublicKeyQuery : IQuery<string>;
