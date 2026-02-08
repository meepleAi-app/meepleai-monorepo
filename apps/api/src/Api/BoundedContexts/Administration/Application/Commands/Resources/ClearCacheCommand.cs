using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands.Resources;

/// <summary>
/// Command to clear all Redis cache keys.
/// DANGER: This will clear all cached data across the entire application.
/// Issue #3695: Resources Monitoring - Clear cache action (Level 2 confirmation required)
/// </summary>
internal record ClearCacheCommand(
    bool Confirmed = false
) : ICommand<bool>;
