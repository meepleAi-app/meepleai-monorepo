using System.Diagnostics;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        var scope = _scopeFactory.CreateAsyncScope();
        try
        {
            var sp = scope.ServiceProvider;
            await Core.CoreSeeder.SeedAsync(
                sp.GetRequiredService<IMediator>(),
                sp.GetRequiredService<MeepleAiDbContext>(),
                _logger, ct).ConfigureAwait(false);
        }
        finally
        {
            await scope.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task SeedCatalogAsync(CancellationToken ct)
    {
        _logger.LogInformation("Layer 2: Catalog seeding (profile: {Profile})...", _profile);
        var scope = _scopeFactory.CreateAsyncScope();
        try
        {
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<MeepleAiDbContext>();
            var adminUser = await db.Users
                .FirstOrDefaultAsync(u => u.Role == "admin", ct)
                .ConfigureAwait(false);
            var systemUserId = adminUser?.Id ?? Guid.Empty;

            await Catalog.CatalogSeeder.SeedAsync(
                _profile,
                db,
                sp.GetRequiredService<IBggApiService>(),
                systemUserId,
                _logger, ct,
                sp.GetService<IEmbeddingService>(),
                sp.GetService<IConfiguration>()).ConfigureAwait(false);
        }
        finally
        {
            await scope.DisposeAsync().ConfigureAwait(false);
        }
    }

}
