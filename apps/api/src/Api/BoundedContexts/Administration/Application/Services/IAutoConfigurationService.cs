namespace Api.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Service for automatic system configuration during first run.
/// Detects first run and executes seed commands for users and AI models.
/// </summary>
public interface IAutoConfigurationService
{
    /// <summary>
    /// Initializes system configuration if this is the first run.
    /// Seeds admin user, test user, and AI models.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if this is the first run (no users exist).
    /// </summary>
    Task<bool> IsFirstRunAsync(CancellationToken cancellationToken = default);
}
