using Api.BoundedContexts.Administration.Application.Attributes;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Self-service account deletion command (GDPR Art. 17 right to erasure).
/// Cascades deletion to sessions, API keys, OAuth accounts, LLM data, AI consent, and notifications.
/// </summary>
[AuditableAction("GdprAccountDeleted", "User", Level = 3)]
internal record DeleteOwnAccountCommand(
    Guid UserId
) : ICommand<DeleteOwnAccountResult>;

/// <summary>
/// Result of the account deletion for audit/compliance logging.
/// </summary>
internal record DeleteOwnAccountResult(
    int SessionsRevoked,
    int LlmRequestLogsDeleted,
    int ConversationMemoriesDeleted,
    bool RedisKeysCleared,
    DateTime DeletedAt);
