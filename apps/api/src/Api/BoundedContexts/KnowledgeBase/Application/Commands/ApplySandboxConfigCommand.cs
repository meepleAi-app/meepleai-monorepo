using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Applies a sandbox configuration for an admin debug chat session.
/// Stores the config in Redis with 24h TTL keyed by admin user ID.
/// </summary>
internal record ApplySandboxConfigCommand(
    Guid AdminUserId,
    Guid GameId,
    SandboxConfigOverrideDto Config
) : ICommand<ApplySandboxConfigResult>;

/// <summary>
/// Result of applying sandbox configuration.
/// </summary>
internal record ApplySandboxConfigResult(
    string SessionKey,
    DateTime ExpiresAt
);
