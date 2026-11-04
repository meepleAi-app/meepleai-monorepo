using Api.Models;

namespace Api.Services;

/// <summary>
/// Service for managing system-wide dynamic configuration.
/// Enables runtime configuration changes without redeployment.
/// </summary>
internal interface IConfigurationService
{
    /// <summary>
    /// Get a configuration by its key (internal infrastructure method).
    /// </summary>
    /// <param name="key">Configuration key (e.g., "RateLimit:Admin:MaxTokens")</param>
    /// <param name="environment">Optional environment filter (defaults to current environment)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Configuration DTO or null if not found</returns>
    Task<SystemConfigurationDto?> GetConfigurationByKeyAsync(
        string key,
        string? environment = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a typed configuration value by key.
    /// </summary>
    /// <typeparam name="T">Expected value type</typeparam>
    /// <param name="key">Configuration key</param>
    /// <param name="defaultValue">Default value if configuration not found</param>
    /// <param name="environment">Optional environment filter</param>
    /// <returns>Typed configuration value or default</returns>
    Task<T?> GetValueAsync<T>(string key, T? defaultValue = default, string? environment = null);

    /// <summary>
    /// Dimentica il valore in cache per una chiave, dopo che qualcuno l'ha scritta.
    /// </summary>
    /// <remarks>
    /// #3844 — le letture sono in cache, comprese quelle che non trovano nulla, e nessuna
    /// scrittura invalidava. Bastava creare una configurazione perche' la lettura successiva
    /// continuasse a rispondere "non esiste" per cinque minuti: chi scriveva di nuovo prendeva il
    /// ramo "crea" e violava il vincolo di unicita' su (Key, Environment).
    ///
    /// Si e' manifestato su abilita/disabilita di un feature flag per tier — il primo riusciva,
    /// il secondo dava 500 — ma la causa non era li': era che la cache non sapeva della scrittura.
    /// </remarks>
    Task InvalidateAsync(string key, string? environment = null, CancellationToken cancellationToken = default);
}
