using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Command to invalidate configuration cache.
/// If key is null, invalidates entire cache.
/// </summary>
internal record InvalidateCacheCommand(
    string? Key = null
) : ICommand<Unit>;

/// <summary>
/// Unit type for commands with no return value.
/// </summary>
internal readonly record struct Unit;
