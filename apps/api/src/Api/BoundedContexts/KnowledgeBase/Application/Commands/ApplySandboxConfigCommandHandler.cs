using System.Text.Json;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Stores sandbox configuration in Redis with 24h TTL.
/// Session key format: sandbox:config:{adminUserId}:{gameId}
/// </summary>
internal class ApplySandboxConfigCommandHandler
    : ICommandHandler<ApplySandboxConfigCommand, ApplySandboxConfigResult>
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<ApplySandboxConfigCommandHandler> _logger;

    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(24);

    public ApplySandboxConfigCommandHandler(
        IDistributedCache cache,
        ILogger<ApplySandboxConfigCommandHandler> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ApplySandboxConfigResult> Handle(
        ApplySandboxConfigCommand command,
        CancellationToken cancellationToken)
    {
        var sessionKey = $"sandbox:config:{command.AdminUserId}:{command.GameId}";
        var expiresAt = DateTime.UtcNow.Add(SessionTtl);

        var json = JsonSerializer.Serialize(command.Config);

        await _cache.SetStringAsync(
            sessionKey,
            json,
            new DistributedCacheEntryOptions { AbsoluteExpiration = expiresAt },
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "[Sandbox] Applied config for admin {AdminUserId}, game {GameId}, key {SessionKey}, expires {ExpiresAt}",
            command.AdminUserId, command.GameId, sessionKey, expiresAt);

        return new ApplySandboxConfigResult(sessionKey, expiresAt);
    }
}
