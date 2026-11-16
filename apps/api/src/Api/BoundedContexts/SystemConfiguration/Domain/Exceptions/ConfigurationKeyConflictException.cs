using System.Diagnostics.CodeAnalysis;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Exceptions;

/// <summary>
/// #5016: a configuration row for this key and environment already exists, so creating it again is a 409,
/// not a 500. Thrown by <c>CreateConfigurationCommandHandler</c> when the unique index on
/// <c>(Key, Environment)</c> refuses the insert, with that <see cref="Microsoft.EntityFrameworkCore.DbUpdateException"/>
/// as the inner exception, for the log.
/// </summary>
/// <remarks>
/// The message is written for the client: it names the key and the environment, never the Postgres error.
/// <c>FeatureFlagService</c> catches this type to recover a creation that lost the race with another request
/// (#4599).
/// </remarks>
public sealed class ConfigurationKeyConflictException : ConflictException
{
    public const string Code = "configuration_key_exists";

    public string Key { get; }

    public string Environment { get; }

    [SetsRequiredMembers]
    public ConfigurationKeyConflictException(string key, string environment, Exception innerException)
        : base(
            Code,
            $"A configuration with key '{key}' already exists for environment '{environment}'.",
            innerException)
    {
        Key = key;
        Environment = environment;
    }
}
