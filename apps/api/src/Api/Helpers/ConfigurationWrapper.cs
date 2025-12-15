using Microsoft.Extensions.Configuration;

namespace Api.Helpers;

/// <summary>
/// Concrete implementation of IConfigurationWrapper.
/// Wraps IConfiguration to enable testing with extension methods.
/// Solves TEST-900 RC-1: Moq cannot mock extension methods.
/// </summary>
internal class ConfigurationWrapper : IConfigurationWrapper
{
    private readonly IConfiguration _configuration;

    public ConfigurationWrapper(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <inheritdoc />
    public bool Exists(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        var section = _configuration.GetSection(key);
        return section.Exists();
    }

    /// <inheritdoc />
    public T? GetValue<T>(string key)
    {
        return _configuration.GetValue<T>(key);
    }

    /// <inheritdoc />
    public IConfigurationSection GetSection(string key)
    {
        return _configuration.GetSection(key);
    }

    /// <inheritdoc />
    public string? this[string key] => _configuration[key];
}
