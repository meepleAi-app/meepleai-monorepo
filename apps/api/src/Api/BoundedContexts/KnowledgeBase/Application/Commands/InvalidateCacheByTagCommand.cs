using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to invalidate cache entries by tag.
/// Admin-only operation for cache management.
/// Tags examples: "game:chess", "pdf:abc123", "user:123"
/// </summary>
/// <param name="Tag">The cache tag to invalidate</param>
internal record InvalidateCacheByTagCommand(
    string Tag
) : ICommand;
