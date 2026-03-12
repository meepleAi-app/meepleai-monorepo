using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders;

/// <summary>
/// Entry point for all seeding operations. Replaces AutoConfigurationService.
/// Creates isolated DI scopes per layer to prevent ChangeTracker leaks.
/// Uses PostgreSQL advisory lock for multi-replica safety.
/// </summary>
internal sealed class SeedOrchestrator
{
    private const long SeedingAdvisoryLockId = 0x4D65_6570_6C65_4149; // "MeepleAI" as long

    private readonly SeedProfile _profile;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SeedOrchestrator> _logger;

    public SeedOrchestrator(
        SeedProfile profile,
        IServiceScopeFactory scopeFactory,
        ILogger<SeedOrchestrator> logger)
    {
        _profile = profile;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        if (_profile == SeedProfile.None)
        {
            _logger.LogInformation("Seed profile is None - skipping all seeding");
            return;
        }

        _logger.LogInformation("Seeding with profile: {Profile}", _profile);
        var sw = Stopwatch.StartNew();

        // Acquire PostgreSQL advisory lock - only one replica seeds at a time
        var lockScope = _scopeFactory.CreateAsyncScope();
        try
        {
            var lockDb = lockScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

            var lockAcquired = await lockDb.Database
                .SqlQueryRaw<bool>("SELECT pg_try_advisory_lock({0})", SeedingAdvisoryLockId)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (!lockAcquired)
            {
                _logger.LogInformation("Another replica is seeding. Skipping.");
                return;
            }

            try
            {
                // Layer 1: Core (always) - isolated scope
                await SeedCoreAsync(ct).ConfigureAwait(false);

                // Layer 2: Catalog (manifest-driven) - isolated scope
                await SeedCatalogAsync(ct).ConfigureAwait(false);

                // Layer 3: LivedIn (staging only) - isolated scope
                if (_profile == SeedProfile.Staging)
                    await SeedLivedInAsync(ct).ConfigureAwait(false);

                _logger.LogInformation("Seeding completed in {Elapsed}ms", sw.ElapsedMilliseconds);
            }
            finally
            {
                await lockDb.Database
                    .ExecuteSqlRawAsync("SELECT pg_advisory_unlock({0})", SeedingAdvisoryLockId)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            await lockScope.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task SeedCoreAsync(CancellationToken ct)
    {
        _logger.LogInformation("Layer 1: Core seeding...");
        // Phase 1: Delegate to existing AutoConfigurationService (backward compat)
        // Phase 2+: Will call CoreSeeder.SeedAsync() directly
        var scope = _scopeFactory.CreateAsyncScope();
        try
        {
            var autoConfig = scope.ServiceProvider
                .GetRequiredService<Api.BoundedContexts.Administration.Application.Services.IAutoConfigurationService>();
            await autoConfig.InitializeAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            await scope.DisposeAsync().ConfigureAwait(false);
        }
    }

    private Task SeedCatalogAsync(CancellationToken ct)
    {
        _logger.LogInformation("Layer 2: Catalog seeding (profile: {Profile})...", _profile);
        // Phase 1: No-op - existing seeders in AutoConfigurationService handle this
        // Phase 2+: Will call CatalogSeeder.SeedAsync() with YAML manifest
        return Task.CompletedTask;
    }

    private Task SeedLivedInAsync(CancellationToken ct)
    {
        _logger.LogInformation("Layer 3: LivedIn seeding (staging only)...");
        // Phase 4: Will call LivedInSeeder.SeedAsync()
        return Task.CompletedTask;
    }
}
