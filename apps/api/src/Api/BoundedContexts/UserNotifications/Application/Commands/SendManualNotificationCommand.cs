using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command for admin to send manual notifications to users.
/// Supports targeting: all users, by role, or specific user IDs.
/// Channels: in-app notification + optional email via queue.
/// </summary>
internal record SendManualNotificationCommand : ICommand<SendManualNotificationResult>
{
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required string[] Channels { get; init; }
    public required string RecipientType { get; init; }
    public string? RecipientRole { get; init; }
    public Guid[]? RecipientUserIds { get; init; }
    public string? DeepLinkPath { get; init; }
    public required Guid SentByAdminId { get; init; }
    public required string SentByAdminName { get; init; }
}

internal record SendManualNotificationResult(
    int TotalRecipients,
    int Dispatched,
    int Skipped,
    bool WasCapped);
