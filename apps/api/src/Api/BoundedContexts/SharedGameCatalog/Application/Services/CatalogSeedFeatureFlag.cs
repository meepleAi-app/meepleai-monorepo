using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Default <see cref="ICatalogSeedFeatureFlag"/> implementation backed by the
/// shared runtime configuration store (<see cref="IConfigurationService"/>).
/// Issue #1903 M7.2.
/// </summary>
/// <remarks>
/// Reads the flag under <see cref="ICatalogSeedFeatureFlag.FlagKey"/> and
/// returns <c>false</c> on any read failure (fail-closed). Logs at
/// <c>Warning</c> when the underlying configuration service throws — operators
/// see this in dashboards as a signal that runtime config is partially
/// degraded but the pipeline is correctly parked.
/// </remarks>
internal sealed class CatalogSeedFeatureFlag : ICatalogSeedFeatureFlag
{
    private readonly IConfigurationService _configService;
    private readonly ILogger<CatalogSeedFeatureFlag> _logger;

    public CatalogSeedFeatureFlag(
        IConfigurationService configService,
        ILogger<CatalogSeedFeatureFlag> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> IsEnabledAsync(CancellationToken ct = default)
    {
        try
        {
            // GetValueAsync<bool?> returns null when the key is absent; the
            // "defaultValue: false" arg is fully redundant for the absence
            // case (nullable returns null) so we coalesce explicitly here.
            var value = await _configService
                .GetValueAsync<bool?>(ICatalogSeedFeatureFlag.FlagKey, defaultValue: null)
                .ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return value ?? false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125
        // SERVICE BOUNDARY: any failure in the runtime config layer (DB
        // unreachable, deserialization failure, etc.) must NOT enable the
        // pipeline. Fail closed → false. Log type only, per CLAUDE.md memory
        // ex-message-leak-pattern.
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex,
                "CatalogSeedFeatureFlag: failed to read {Key}, defaulting to disabled (errorType={ErrorType})",
                ICatalogSeedFeatureFlag.FlagKey, ex.GetType().Name);
            return false;
        }
    }
}
