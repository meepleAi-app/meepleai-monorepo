using Microsoft.Extensions.Configuration;

namespace Api.Helpers;

/// <summary>
/// Wrapper interface for IConfiguration to enable mocking of extension methods.
/// Solves TEST-900 RC-1: Moq cannot mock extension methods like Exists() and GetValue<T>().
/// </summary>
public interface IConfigurationWrapper
{
    /// <summary>
    /// Checks if a configuration key exists.
    /// Wraps ConfigurationExtensions.Exists() extension method.
    /// </summary>
    bool Exists(string key);

    /// <summary>
    /// Gets a typed configuration value.
    /// Wraps ConfigurationBinder.GetValue<T>() extension method.
    /// </summary>
    T? GetValue<T>(string key);

    /// <summary>
    /// Gets a configuration section by key.
    /// Direct IConfiguration method, included for completeness.
    /// </summary>
    IConfigurationSection GetSection(string key);

    /// <summary>
    /// Gets a configuration value by key using indexer.
    /// Direct IConfiguration method, included for completeness.
    /// </summary>
    string? this[string key] { get; }
}
