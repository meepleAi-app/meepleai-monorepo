using Api.BoundedContexts.SystemConfiguration.Domain.Entities;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Repositories;

/// <summary>
/// Issue #5498: Repository for LLM system configuration.
/// Single-row pattern — at most one active configuration row.
/// </summary>
public interface ILlmSystemConfigRepository
{
    /// <summary>
    /// Get the current LLM system configuration (single row), or null if not yet configured.
    /// </summary>
    Task<LlmSystemConfig?> GetCurrentAsync(CancellationToken ct = default);

    /// <summary>
    /// Save or update the LLM system configuration.
    /// </summary>
    Task UpsertAsync(LlmSystemConfig config, CancellationToken ct = default);
}
