using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for DeleteUserLlmDataCommand.
/// Issue #5509: GDPR Art. 17 — deletes all LLM-related data for a user.
/// Data stores: LlmRequestLog (DB), ConversationMemory (DB), Redis user keys.
/// JSONL logs already hash UserIds (OpenRouterFileLogger) — no action needed.
/// </summary>
internal sealed class DeleteUserLlmDataCommandHandler
    : IRequestHandler<DeleteUserLlmDataCommand, DeleteUserLlmDataResult>
{
    private readonly ILlmRequestLogRepository _llmRequestLogRepository;
    private readonly IConversationMemoryRepository _conversationMemoryRepository;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<DeleteUserLlmDataCommandHandler> _logger;

    public DeleteUserLlmDataCommandHandler(
        ILlmRequestLogRepository llmRequestLogRepository,
        IConversationMemoryRepository conversationMemoryRepository,
        IConnectionMultiplexer redis,
        ILogger<DeleteUserLlmDataCommandHandler> logger)
    {
        _llmRequestLogRepository = llmRequestLogRepository ?? throw new ArgumentNullException(nameof(llmRequestLogRepository));
        _conversationMemoryRepository = conversationMemoryRepository ?? throw new ArgumentNullException(nameof(conversationMemoryRepository));
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DeleteUserLlmDataResult> Handle(
        DeleteUserLlmDataCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogWarning(
            "GDPR erasure initiated: UserId={UserId}, RequestedBy={RequestedBy}, IsAdmin={IsAdmin}",
            request.UserId, request.RequestedByUserId, request.IsAdminRequest);

        // 1. Delete LLM request logs
        var logsDeleted = await _llmRequestLogRepository
            .DeleteByUserIdAsync(request.UserId, cancellationToken)
            .ConfigureAwait(false);

        // 2. Delete conversation memories
        var memoriesDeleted = await _conversationMemoryRepository
            .DeleteByUserIdAsync(request.UserId, cancellationToken)
            .ConfigureAwait(false);

        // 3. Clear Redis user keys
        var redisCleared = await ClearRedisUserKeysAsync(request.UserId).ConfigureAwait(false);

        var result = new DeleteUserLlmDataResult(
            LlmRequestLogsDeleted: logsDeleted,
            ConversationMemoriesDeleted: memoriesDeleted,
            RedisKeysCleared: redisCleared,
            DeletedAt: DateTime.UtcNow);

        _logger.LogWarning(
            "GDPR erasure completed: UserId={UserId}, Logs={Logs}, Memories={Memories}, Redis={Redis}",
            request.UserId, logsDeleted, memoriesDeleted, redisCleared);

        return result;
    }

    private async Task<bool> ClearRedisUserKeysAsync(Guid userId)
    {
        try
        {
            var db = _redis.GetDatabase();

            // Clear user session keys (pattern: user_sessions:{userId})
            var userSessionsKey = $"user_sessions:{userId}";
            var sessionKeys = await db.SetMembersAsync(userSessionsKey).ConfigureAwait(false);

            if (sessionKeys.Length > 0)
            {
                var redisKeys = sessionKeys.Select(k => (RedisKey)k.ToString()).ToArray();
                await db.KeyDeleteAsync(redisKeys).ConfigureAwait(false);
            }

            await db.KeyDeleteAsync(userSessionsKey).ConfigureAwait(false);
            return true;
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex, "Failed to clear Redis keys for user {UserId}", userId);
            return false;
        }
    }
}
