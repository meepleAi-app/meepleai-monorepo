using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders;

/// <summary>
/// Entry point for all seeding operations. Replaces AutoConfigurationService.
/// Resolves profile, acquires non-blocking advisory lock, runs ISeedLayer implementations in order.
/// Clears ChangeTracker between layers to prevent entity leaks.
/// </summary>
internal sealed class SeedOrchestrator
{
    private readonly IEnumerable<ISeedLayer> _layers;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SeedOrchestrator> _logger;

    public SeedOrchestrator(
        IEnumerable<ISeedLayer> layers,
        IConfiguration configuration,
        ILogger<SeedOrchestrator> logger)
    {
        _layers = layers;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Run all applicable seed layers inside an advisory lock.
    /// Called from Program.cs startup and the admin endpoint.
    /// </summary>
    public async Task RunAsync(MeepleAiDbContext db, IServiceProvider services, CancellationToken ct = default)
    {
        var profile = ResolveProfile(_configuration, logger: _logger);

        if (profile == SeedProfile.None)
        {
            _logger.LogInformation("Seed profile is None — skipping all seeding");
            return;
        }

        _logger.LogInformation("Seeding with profile: {Profile}", profile);
        var sw = Stopwatch.StartNew();

        var lockAcquired = await AdvisoryLockHelper.TryAcquireAsync(db, _logger, ct).ConfigureAwait(false);
        if (!lockAcquired)
            return;

        try
        {
            var layers = FilterLayers(_layers, profile);
            _logger.LogInformation("Running {Count} seed layer(s)", layers.Count);

            // Resolve systemUserId — may be Guid.Empty on fresh DB before Core seeds admin
            var systemUserId = await ResolveSystemUserIdAsync(db, ct).ConfigureAwait(false);

            foreach (var layer in layers)
            {
                _logger.LogInformation("Running seed layer: {Layer} (min profile: {MinProfile})",
                    layer.Name, layer.MinimumProfile);

                var context = new SeedContext(profile, db, services, _logger, systemUserId);
                await layer.SeedAsync(context, ct).ConfigureAwait(false);

                // Clear ChangeTracker between layers to prevent entity leaks
                db.ChangeTracker.Clear();

                // Re-resolve systemUserId after Core layer creates admin user
                if (layer.MinimumProfile == SeedProfile.Prod)
                    systemUserId = await ResolveSystemUserIdAsync(db, ct).ConfigureAwait(false);
            }

            _logger.LogInformation("Seeding completed in {Elapsed}ms", sw.ElapsedMilliseconds);
        }
        finally
        {
            await AdvisoryLockHelper.ReleaseAsync(db, _logger, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Resolve seed profile: SEED_PROFILE env var → Seeding:Profile config →
    /// derive from ASPNETCORE_ENVIRONMENT (fail-closed to None on unknown).
    /// </summary>
    internal static SeedProfile ResolveProfile(
        IConfiguration? configuration,
        string? environmentName = null,
        ILogger? logger = null)
    {
        // 1. Environment variable takes priority (explicit override)
        var envVar = Environment.GetEnvironmentVariable("SEED_PROFILE");
        if (!string.IsNullOrWhiteSpace(envVar))
        {
            if (Enum.TryParse<SeedProfile>(envVar, ignoreCase: true, out var envProfile))
                return envProfile;
            logger?.LogWarning("SEED_PROFILE='{Value}' is not a valid seed profile; ignoring.", envVar);
        }

        // 2. Configuration section (explicit override)
        var configValue = configuration?["Seeding:Profile"];
        if (!string.IsNullOrWhiteSpace(configValue))
        {
            if (Enum.TryParse<SeedProfile>(configValue, ignoreCase: true, out var cfgProfile))
                return cfgProfile;
            logger?.LogWarning("Seeding:Profile='{Value}' is not a valid seed profile; ignoring.", configValue);
        }

        // 3. Derive from ASPNETCORE_ENVIRONMENT (fail-closed)
        var env = environmentName ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var derived = env?.Trim().ToLowerInvariant() switch
        {
            "production" => SeedProfile.Prod,
            "staging" => SeedProfile.Staging,
            "development" => SeedProfile.Dev,
            _ => SeedProfile.None, // null / "Test" / "CI" / unknown → fail-closed
        };

        if (derived == SeedProfile.None)
        {
            logger?.LogWarning(
                "Seed profile unresolved (SEED_PROFILE and Seeding:Profile unset; ASPNETCORE_ENVIRONMENT='{Env}' unrecognized). "
                + "Seeding with profile None (no data). Set SEED_PROFILE explicitly.", env ?? "(null)");
        }

        return derived;
    }

    /// <summary>
    /// Filter layers by profile ordinal and materialize to list.
    /// A layer runs if MinimumProfile &lt;= active profile.
    /// </summary>
    internal static IReadOnlyList<ISeedLayer> FilterLayers(IEnumerable<ISeedLayer> layers, SeedProfile profile)
        => layers.Where(l => l.MinimumProfile <= profile).OrderBy(l => l.MinimumProfile).ToList();

    private static async Task<Guid> ResolveSystemUserIdAsync(MeepleAiDbContext db, CancellationToken ct)
    {
        // Issue #372: Query must match both admin and superadmin roles
        var adminUser = await db.Users
            .FirstOrDefaultAsync(u => u.Role == "admin" || u.Role == "superadmin", ct)
            .ConfigureAwait(false);
        return adminUser?.Id ?? Guid.Empty;
    }
}
