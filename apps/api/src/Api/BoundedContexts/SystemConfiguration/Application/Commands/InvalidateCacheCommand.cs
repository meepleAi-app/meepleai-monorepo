using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Command to invalidate configuration cache.
/// If key is null, invalidates entire cache.
/// </summary>
public record InvalidateCacheCommand(
    string? Key = null
) : ICommand<Unit>;

/// <summary>
/// Unit type for commands with no return value.
/// </summary>
public readonly record struct Unit;
