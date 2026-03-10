using Api.BoundedContexts.Administration.Application.Attributes;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// GDPR Art. 20 data portability: export all user data in machine-readable JSON format.
/// Aggregates data from all bounded contexts for the requesting user.
/// This is a query (read-only) despite living in Commands folder for GDPR grouping.
/// </summary>
[AuditableAction("GdprDataExported", "User", Level = 2)]
internal record ExportUserDataCommand(
    Guid UserId
) : IQuery<ExportUserDataResult>;

/// <summary>
/// Result containing all user data in a structured, portable format.
/// </summary>
internal record ExportUserDataResult(
    ExportedUserProfile Profile,
    ExportedUserPreferences Preferences,
    IReadOnlyList<ExportedLibraryEntry> Library,
    IReadOnlyList<ExportedChatThread> ChatThreads,
    IReadOnlyList<ExportedNotification> Notifications,
    ExportedAiConsent? AiConsent,
    ExportedDataSummary Summary,
    DateTime ExportedAt);

internal record ExportedUserProfile(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    string Tier,
    DateTime CreatedAt,
    bool EmailVerified,
    bool Has2FAEnabled);

internal record ExportedUserPreferences(
    string Language,
    bool EmailNotifications,
    string Theme,
    int DataRetentionDays);

internal record ExportedLibraryEntry(
    Guid GameId,
    string GameTitle,
    string State,
    bool IsFavorite,
    DateTime AddedAt);

internal record ExportedChatThread(
    Guid ThreadId,
    string? GameTitle,
    int MessageCount,
    DateTime CreatedAt,
    DateTime? LastMessageAt);

internal record ExportedNotification(
    Guid Id,
    string Type,
    string Title,
    bool IsRead,
    DateTime CreatedAt);

internal record ExportedAiConsent(
    bool ConsentGiven,
    DateTime? ConsentedAt,
    DateTime? RevokedAt);

internal record ExportedDataSummary(
    int TotalLibraryGames,
    int TotalChatThreads,
    int TotalNotifications,
    int TotalConversationMemories,
    int TotalApiKeys);
