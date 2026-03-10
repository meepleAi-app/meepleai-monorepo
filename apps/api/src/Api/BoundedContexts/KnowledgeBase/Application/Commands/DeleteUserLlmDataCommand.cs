using Api.BoundedContexts.Administration.Application.Attributes;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// CQRS command to delete all LLM-related data for a specific user.
/// Issue #5509: GDPR Art. 17 right to erasure for LLM subsystem data.
/// </summary>
[AuditableAction("GdprLlmDataDeleted", "User", Level = 3)]
internal record DeleteUserLlmDataCommand(
    Guid UserId,
    Guid RequestedByUserId,
    bool IsAdminRequest
) : IRequest<DeleteUserLlmDataResult>;

/// <summary>
/// Result of the LLM data deletion operation for audit purposes.
/// </summary>
internal record DeleteUserLlmDataResult(
    int LlmRequestLogsDeleted,
    int ConversationMemoriesDeleted,
    bool RedisKeysCleared,
    DateTime DeletedAt);
