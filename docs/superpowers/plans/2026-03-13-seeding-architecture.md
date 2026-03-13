# Epic #318: Hybrid Layered Seeding Architecture — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the monolithic `AutoConfigurationService` with a layered, profile-driven seed orchestrator that supports YAML manifests, PostgreSQL advisory locks, and composable seed layers (Core, Catalog, LivedIn).

**Architecture:** New `SeedOrchestrator` replaces `AutoConfigurationService.InitializeAsync()` as the single entry point. It reads a `SeedProfile` (Dev/Staging/Prod) and runs composable layers in order: Core (users, AI models, e2e) → Catalog (shared games from YAML + badges + feature flags + rate limits) → LivedIn (synthetic sessions, optional). Each layer implements `ISeedLayer`. YAML manifests in `data/seed-manifests/` drive SharedGame creation instead of hardcoded dictionaries. Advisory lock prevents concurrent seeding across replicas.

**Tech Stack:** .NET 9, EF Core, PostgreSQL advisory locks (`pg_advisory_lock`), YamlDotNet 16.3.0, xUnit, NSubstitute

---

## File Structure

### New Files
| File | Responsibility |
|------|----------------|
| `Infrastructure/Seeders/ISeedLayer.cs` | Interface: `Task SeedAsync(SeedContext ctx, CancellationToken ct)` |
| `Infrastructure/Seeders/SeedContext.cs` | Record holding `SeedProfile`, `MeepleAiDbContext`, `IServiceProvider`, `ILogger`, `Guid SystemUserId` |
| `Infrastructure/Seeders/SeedProfile.cs` | Enum: `Dev`, `Staging`, `Prod` |
| `Infrastructure/Seeders/SeedOrchestrator.cs` | Entry point: advisory lock → resolve layers → run in order |
| `Infrastructure/Seeders/AdvisoryLockHelper.cs` | `pg_advisory_lock(key)` / `pg_advisory_unlock(key)` wrapper |
| `Infrastructure/Seeders/Core/CoreSeedLayer.cs` | Layer: admin user, test user, E2E users, AI models |
| `Infrastructure/Seeders/Catalog/CatalogSeedLayer.cs` | Layer: YAML shared games, badges, rate limits, feature flags, PDF rulebooks, agents |
| `Infrastructure/Seeders/Catalog/SeedManifestLoader.cs` | Reads + validates YAML from `data/seed-manifests/` |
| `Infrastructure/Seeders/Catalog/SeedManifestModels.cs` | YAML deserialization models |
| `Infrastructure/Seeders/LivedIn/LivedInSeedLayer.cs` | Layer: synthetic sessions, placeholder for future |
| `Routing/AdminSeedingEndpoints.cs` | `POST /admin/seeding/orchestrate` endpoint |
| `data/seed-manifests/shared-games.yaml` | YAML manifest for shared games (replaces hardcoded dict) |
| `infra/scripts/db-dump.ps1` | Production data dump script |
| `infra/scripts/db-restore.ps1` | Staging data restore script |

### Modified Files
| File | Change |
|------|--------|
| `Program.cs:397-401` | Replace `AutoConfigurationService` call with `SeedOrchestrator` |
| `AdministrationServiceExtensions.cs` | Register `SeedOrchestrator` and seed layers |
| `AutoConfigurationService.cs` | Mark as `[Obsolete]`, delegate to `SeedOrchestrator` |

### Test Files
| File | Tests |
|------|-------|
| `tests/Api.Tests/Infrastructure/Seeders/SeedOrchestratorTests.cs` | Orchestrator logic, layer ordering, advisory lock |
| `tests/Api.Tests/Infrastructure/Seeders/SeedManifestLoaderTests.cs` | YAML parsing, validation |
| `tests/Api.Tests/Infrastructure/Seeders/CoreSeedLayerTests.cs` | Core layer dispatch |
| `tests/Api.Tests/Infrastructure/Seeders/CatalogSeedLayerTests.cs` | Catalog layer dispatch |
| `tests/Api.Tests/Infrastructure/Seeders/AdvisoryLockHelperTests.cs` | Lock acquire/release |

---

## Chunk 1: Foundation — ISeedLayer, SeedContext, SeedProfile, AdvisoryLock

### Task 1: Create ISeedLayer interface, SeedContext, and SeedProfile enum (#351)

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Seeders/ISeedLayer.cs`
- Create: `apps/api/src/Api/Infrastructure/Seeders/SeedContext.cs`
- Create: `apps/api/src/Api/Infrastructure/Seeders/SeedProfile.cs`
- Test: `tests/Api.Tests/Infrastructure/Seeders/SeedContextTests.cs`

- [ ] **Step 1: Create SeedProfile enum**

```csharp
// apps/api/src/Api/Infrastructure/Seeders/SeedProfile.cs
namespace Api.Infrastructure.Seeders;

/// <summary>
/// Determines which seed layers run and with what data.
/// Set via SEED_PROFILE env var or appsettings.
/// </summary>
public enum SeedProfile
{
    /// <summary>No seeding (CI/testing environments).</summary>
    None = 0,
    /// <summary>Production: Core layer only (admin + AI models).</summary>
    Prod = 1,
    /// <summary>Staging: Core + Catalog (shared games, badges, flags).</summary>
    Staging = 2,
    /// <summary>Development: Core + Catalog + LivedIn (synthetic data).</summary>
    Dev = 3
}
```

- [ ] **Step 2: Create ISeedLayer interface**

```csharp
// apps/api/src/Api/Infrastructure/Seeders/ISeedLayer.cs
namespace Api.Infrastructure.Seeders;

/// <summary>
/// A composable seed layer. Layers run in registration order.
/// Each layer is idempotent — safe to re-run.
/// </summary>
public interface ISeedLayer
{
    /// <summary>Display name for logging.</summary>
    string Name { get; }

    /// <summary>Minimum profile required to run this layer.</summary>
    SeedProfile MinimumProfile { get; }

    /// <summary>Execute seeding logic.</summary>
    Task SeedAsync(SeedContext context, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: Create SeedContext record**

```csharp
// apps/api/src/Api/Infrastructure/Seeders/SeedContext.cs
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders;

/// <summary>
/// Shared context passed to all seed layers.
/// </summary>
public sealed record SeedContext(
    SeedProfile Profile,
    MeepleAiDbContext DbContext,
    IServiceProvider Services,
    ILogger Logger,
    Guid SystemUserId);
```

- [ ] **Step 4: Write unit test for SeedContext**

```csharp
// tests/Api.Tests/Infrastructure/Seeders/SeedContextTests.cs
using Api.Infrastructure.Seeders;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders;

public sealed class SeedContextTests
{
    [Fact]
    public void SeedContext_StoresAllProperties()
    {
        var profile = SeedProfile.Dev;
        var userId = Guid.NewGuid();
        var services = Substitute.For<IServiceProvider>();
        var logger = NullLogger.Instance;

        var ctx = new SeedContext(profile, null!, services, logger, userId);

        Assert.Equal(SeedProfile.Dev, ctx.Profile);
        Assert.Equal(userId, ctx.SystemUserId);
        Assert.Same(services, ctx.Services);
    }

    [Theory]
    [InlineData(SeedProfile.None, 0)]
    [InlineData(SeedProfile.Prod, 1)]
    [InlineData(SeedProfile.Staging, 2)]
    [InlineData(SeedProfile.Dev, 3)]
    public void SeedProfile_HasCorrectOrdinalValues(SeedProfile profile, int expected)
    {
        Assert.Equal(expected, (int)profile);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~SeedContextTests" -v quiet`
Expected: 5 tests pass

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Seeders/ISeedLayer.cs \
        apps/api/src/Api/Infrastructure/Seeders/SeedContext.cs \
        apps/api/src/Api/Infrastructure/Seeders/SeedProfile.cs \
        tests/Api.Tests/Infrastructure/Seeders/SeedContextTests.cs
git commit -m "feat(seeding): add ISeedLayer interface, SeedContext, SeedProfile enum (#351)"
```

---

### Task 2: Create AdvisoryLockHelper (#351)

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Seeders/AdvisoryLockHelper.cs`
- Test: `tests/Api.Tests/Infrastructure/Seeders/AdvisoryLockHelperTests.cs`

- [ ] **Step 1: Write failing test for AdvisoryLockHelper**

```csharp
// tests/Api.Tests/Infrastructure/Seeders/AdvisoryLockHelperTests.cs
using Api.Infrastructure.Seeders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders;

public sealed class AdvisoryLockHelperTests
{
    [Fact]
    public void LockKey_IsConsistentAcrossCalls()
    {
        // The lock key must be deterministic so all replicas use the same lock
        var key1 = AdvisoryLockHelper.SeedingLockKey;
        var key2 = AdvisoryLockHelper.SeedingLockKey;

        Assert.Equal(key1, key2);
        Assert.NotEqual(0, key1);
    }

    [Fact]
    public void AcquireSql_ContainsPgAdvisoryLock()
    {
        var sql = AdvisoryLockHelper.AcquireLockSql;
        Assert.Contains("pg_advisory_lock", sql);
        Assert.Contains(AdvisoryLockHelper.SeedingLockKey.ToString(), sql);
    }

    [Fact]
    public void ReleaseSql_ContainsPgAdvisoryUnlock()
    {
        var sql = AdvisoryLockHelper.ReleaseLockSql;
        Assert.Contains("pg_advisory_unlock", sql);
        Assert.Contains(AdvisoryLockHelper.SeedingLockKey.ToString(), sql);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~AdvisoryLockHelperTests" -v quiet`
Expected: FAIL — `AdvisoryLockHelper` does not exist

- [ ] **Step 3: Implement AdvisoryLockHelper**

```csharp
// apps/api/src/Api/Infrastructure/Seeders/AdvisoryLockHelper.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders;

/// <summary>
/// PostgreSQL advisory lock wrapper for safe multi-replica seeding.
/// Uses session-level advisory locks (released on disconnect).
/// </summary>
internal static class AdvisoryLockHelper
{
    /// <summary>
    /// Deterministic lock key derived from "MeepleAI_Seeding" hash.
    /// All replicas must use the same key.
    /// </summary>
    public static long SeedingLockKey { get; } = "MeepleAI_Seeding".GetDeterministicHashCode();

    public static string AcquireLockSql => $"SELECT pg_advisory_lock({SeedingLockKey})";
    public static string ReleaseLockSql => $"SELECT pg_advisory_unlock({SeedingLockKey})";

    /// <summary>
    /// Acquires the advisory lock. Blocks until the lock is available.
    /// </summary>
    public static async Task AcquireAsync(MeepleAiDbContext db, ILogger logger, CancellationToken ct = default)
    {
        logger.LogInformation("🔒 Acquiring seeding advisory lock (key={LockKey})...", SeedingLockKey);
        await db.Database.ExecuteSqlRawAsync(AcquireLockSql, ct).ConfigureAwait(false);
        logger.LogInformation("🔓 Seeding advisory lock acquired");
    }

    /// <summary>
    /// Releases the advisory lock.
    /// </summary>
    public static async Task ReleaseAsync(MeepleAiDbContext db, ILogger logger, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(ReleaseLockSql, ct).ConfigureAwait(false);
        logger.LogInformation("🔒 Seeding advisory lock released");
    }

    /// <summary>
    /// Deterministic hash code that doesn't change across app restarts (.NET randomizes GetHashCode).
    /// </summary>
    private static long GetDeterministicHashCode(this string str)
    {
        unchecked
        {
            long hash = 5381;
            foreach (var c in str)
            {
                hash = ((hash << 5) + hash) ^ c;
            }
            return hash;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~AdvisoryLockHelperTests" -v quiet`
Expected: 3 tests pass

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Seeders/AdvisoryLockHelper.cs \
        tests/Api.Tests/Infrastructure/Seeders/AdvisoryLockHelperTests.cs
git commit -m "feat(seeding): add AdvisoryLockHelper with PostgreSQL advisory locks (#351)"
```

---

### Task 3: Create SeedOrchestrator (#351)

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Seeders/SeedOrchestrator.cs`
- Test: `tests/Api.Tests/Infrastructure/Seeders/SeedOrchestratorTests.cs`

- [ ] **Step 1: Write failing test for SeedOrchestrator**

```csharp
// tests/Api.Tests/Infrastructure/Seeders/SeedOrchestratorTests.cs
using Api.Infrastructure.Seeders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders;

public sealed class SeedOrchestratorTests
{
    [Fact]
    public void GetProfile_ReadsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seeding:Profile"] = "Staging"
            })
            .Build();

        var profile = SeedOrchestrator.ResolveProfile(config);

        Assert.Equal(SeedProfile.Staging, profile);
    }

    [Fact]
    public void GetProfile_DefaultsToDev_WhenNotSet()
    {
        var config = new ConfigurationBuilder().Build();

        var profile = SeedOrchestrator.ResolveProfile(config);

        Assert.Equal(SeedProfile.Dev, profile);
    }

    [Fact]
    public void GetProfile_ReadsFromEnvironmentVariable()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SEED_PROFILE"] = "Prod"
            })
            .Build();

        var profile = SeedOrchestrator.ResolveProfile(config);

        Assert.Equal(SeedProfile.Prod, profile);
    }

    [Fact]
    public void FilterLayers_ExcludesLayersAboveProfile()
    {
        var coreMock = CreateMockLayer("Core", SeedProfile.Prod);
        var catalogMock = CreateMockLayer("Catalog", SeedProfile.Staging);
        var livedInMock = CreateMockLayer("LivedIn", SeedProfile.Dev);

        var layers = new[] { coreMock, catalogMock, livedInMock };

        var filtered = SeedOrchestrator.FilterLayers(layers, SeedProfile.Staging);

        Assert.Equal(2, filtered.Count);
        Assert.Equal("Core", filtered[0].Name);
        Assert.Equal("Catalog", filtered[1].Name);
    }

    [Fact]
    public void FilterLayers_NoneProfile_ReturnsEmpty()
    {
        var coreMock = CreateMockLayer("Core", SeedProfile.Prod);
        var layers = new[] { coreMock };

        var filtered = SeedOrchestrator.FilterLayers(layers, SeedProfile.None);

        Assert.Empty(filtered);
    }

    private static ISeedLayer CreateMockLayer(string name, SeedProfile minProfile)
    {
        var layer = Substitute.For<ISeedLayer>();
        layer.Name.Returns(name);
        layer.MinimumProfile.Returns(minProfile);
        return layer;
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~SeedOrchestratorTests" -v quiet`
Expected: FAIL — `SeedOrchestrator` does not exist

- [ ] **Step 3: Implement SeedOrchestrator**

```csharp
// apps/api/src/Api/Infrastructure/Seeders/SeedOrchestrator.cs
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders;

/// <summary>
/// Entry point for the layered seeding system.
/// Acquires advisory lock → resolves profile → runs layers in order.
/// Replaces AutoConfigurationService as the single startup seed orchestrator.
/// </summary>
internal sealed class SeedOrchestrator
{
    private readonly IEnumerable<ISeedLayer> _layers;
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SeedOrchestrator> _logger;

    public SeedOrchestrator(
        IEnumerable<ISeedLayer> layers,
        IUserRepository userRepository,
        IConfiguration configuration,
        ILogger<SeedOrchestrator> logger)
    {
        _layers = layers ?? throw new ArgumentNullException(nameof(layers));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Run the seeding pipeline under advisory lock.
    /// </summary>
    public async Task RunAsync(MeepleAiDbContext db, IServiceProvider services, CancellationToken ct = default)
    {
        var profile = ResolveProfile(_configuration);

        if (profile == SeedProfile.None)
        {
            _logger.LogInformation("🌱 Seed profile is None — skipping all seeding");
            return;
        }

        _logger.LogInformation("🌱 Starting seeding pipeline (profile={Profile})", profile);

        try
        {
            await AdvisoryLockHelper.AcquireAsync(db, _logger, ct).ConfigureAwait(false);

            // Resolve system user (admin) — created by CoreSeedLayer
            var adminUser = await db.Users
                .FirstOrDefaultAsync(u => u.Role == "admin", ct)
                .ConfigureAwait(false);
            var systemUserId = adminUser?.Id ?? Guid.Empty;

            var context = new SeedContext(profile, db, services, _logger, systemUserId);
            var activeLayers = FilterLayers(_layers, profile);

            foreach (var layer in activeLayers)
            {
                _logger.LogInformation("🌱 Running seed layer: {LayerName}", layer.Name);
                await layer.SeedAsync(context, ct).ConfigureAwait(false);

                // After Core layer, re-resolve system user ID (admin just created)
                if (layer.MinimumProfile == SeedProfile.Prod && context.SystemUserId == Guid.Empty)
                {
                    adminUser = await db.Users
                        .FirstOrDefaultAsync(u => u.Role == "admin", ct)
                        .ConfigureAwait(false);
                    if (adminUser != null)
                    {
                        context = context with { SystemUserId = adminUser.Id };
                    }
                }
            }

            _logger.LogInformation("🌱 Seeding pipeline completed successfully ({LayerCount} layers)", activeLayers.Count);
        }
        finally
        {
            await AdvisoryLockHelper.ReleaseAsync(db, _logger, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Resolve seed profile from configuration. Priority: Seeding:Profile > SEED_PROFILE > default Dev.
    /// </summary>
    public static SeedProfile ResolveProfile(IConfiguration configuration)
    {
        var profileStr = configuration["Seeding:Profile"]
                      ?? configuration["SEED_PROFILE"];

        if (string.IsNullOrEmpty(profileStr))
            return SeedProfile.Dev;

        return Enum.TryParse<SeedProfile>(profileStr, ignoreCase: true, out var parsed)
            ? parsed
            : SeedProfile.Dev;
    }

    /// <summary>
    /// Filter layers to only those matching the active profile.
    /// A layer runs if its MinimumProfile ordinal ≤ the active profile ordinal.
    /// </summary>
    public static IReadOnlyList<ISeedLayer> FilterLayers(IEnumerable<ISeedLayer> layers, SeedProfile profile)
    {
        if (profile == SeedProfile.None)
            return Array.Empty<ISeedLayer>();

        return layers
            .Where(l => (int)l.MinimumProfile <= (int)profile)
            .ToList();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~SeedOrchestratorTests" -v quiet`
Expected: 4 tests pass

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Seeders/SeedOrchestrator.cs \
        tests/Api.Tests/Infrastructure/Seeders/SeedOrchestratorTests.cs
git commit -m "feat(seeding): add SeedOrchestrator with profile resolution and layer filtering (#351)"
```

---

## Chunk 2: Seed Layers — Core, Catalog, LivedIn

### Task 4: Create CoreSeedLayer (#353)

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Seeders/Core/CoreSeedLayer.cs`
- Test: `tests/Api.Tests/Infrastructure/Seeders/CoreSeedLayerTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
// tests/Api.Tests/Infrastructure/Seeders/CoreSeedLayerTests.cs
using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Core;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders;

public sealed class CoreSeedLayerTests
{
    [Fact]
    public void Name_IsCore()
    {
        var layer = new CoreSeedLayer();
        Assert.Equal("Core", layer.Name);
    }

    [Fact]
    public void MinimumProfile_IsProd()
    {
        var layer = new CoreSeedLayer();
        Assert.Equal(SeedProfile.Prod, layer.MinimumProfile);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~CoreSeedLayerTests" -v quiet`
Expected: FAIL

- [ ] **Step 3: Implement CoreSeedLayer**

This layer wraps existing CQRS seeder commands. It delegates to `IMediator` for admin user, test user, E2E users, AI models — exactly what `AutoConfigurationService.InitializeAsync()` does in its first block.

```csharp
// apps/api/src/Api/Infrastructure/Seeders/Core/CoreSeedLayer.cs
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Infrastructure.Seeders.Core;

/// <summary>
/// Core seed layer: admin user, test user, E2E users, AI models.
/// Runs in all profiles (Prod, Staging, Dev).
/// </summary>
internal sealed class CoreSeedLayer : ISeedLayer
{
    public string Name => "Core";
    public SeedProfile MinimumProfile => SeedProfile.Prod;

    public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken = default)
    {
        var mediator = context.Services.GetRequiredService<IMediator>();

        context.Logger.LogInformation("🌱 [Core] Seeding admin user...");
        await mediator.Send(new SeedAdminUserCommand(), cancellationToken).ConfigureAwait(false);

        context.Logger.LogInformation("🌱 [Core] Seeding test user...");
        await mediator.Send(new SeedTestUserCommand(), cancellationToken).ConfigureAwait(false);

        context.Logger.LogInformation("🌱 [Core] Seeding E2E test users...");
        await mediator.Send(new SeedE2ETestUsersCommand(), cancellationToken).ConfigureAwait(false);

        context.Logger.LogInformation("🌱 [Core] Seeding AI models...");
        await mediator.Send(new SeedAiModelsCommand(), cancellationToken).ConfigureAwait(false);

        context.Logger.LogInformation("🌱 [Core] Seeding agent definitions...");
        await mediator.Send(new SeedAgentDefinitionsCommand(), cancellationToken).ConfigureAwait(false);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~CoreSeedLayerTests" -v quiet`
Expected: 2 tests pass

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Seeders/Core/CoreSeedLayer.cs \
        tests/Api.Tests/Infrastructure/Seeders/CoreSeedLayerTests.cs
git commit -m "feat(seeding): add CoreSeedLayer wrapping user/model seeding (#353)"
```

---

### Task 5: Create YAML SeedManifest models and loader (#352)

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Seeders/Catalog/SeedManifestModels.cs`
- Create: `apps/api/src/Api/Infrastructure/Seeders/Catalog/SeedManifestLoader.cs`
- Create: `data/seed-manifests/shared-games.yaml`
- Test: `tests/Api.Tests/Infrastructure/Seeders/SeedManifestLoaderTests.cs`

- [ ] **Step 1: Create YAML manifest file**

Extract the hardcoded `SharedGameSeeder.GameMappings` dictionary into YAML. File: `data/seed-manifests/shared-games.yaml`

```yaml
# Shared Game Catalog seed manifest
# Used by CatalogSeedLayer to create SharedGame entries
# BGG IDs used to fetch metadata; fallback data used when BGG unavailable
games:
  - title: "7 Wonders"
    bggId: 68448
    language: en
    pdfFile: "7-wonders_rulebook.pdf"
    imageUrl: "https://cf.geekdo-images.com/35h9Za_JvMMMtx_92kT0Jg__original/img/jt70jJDZ1y1FWJs4ZQf5FI8APVY=/0x0/filters:format(jpeg)/pic7149798.jpg"
    thumbnailUrl: "https://cf.geekdo-images.com/35h9Za_JvMMMtx_92kT0Jg__small/img/BUOso8b0M1aUOkU80FWlhE8uuxc=/fit-in/200x150/filters:strip_icc()/pic7149798.jpg"

  - title: "Agricola"
    bggId: 31260
    language: en
    pdfFile: "agricola_rulebook.pdf"
    imageUrl: "https://cf.geekdo-images.com/3L6ZtOll9W5O6-3-EwSMyw__original/img/V37KuMJlCzpxAilzN39BzeLvc9Q=/0x0/filters:format(jpeg)/pic1899157.jpg"
    thumbnailUrl: "https://cf.geekdo-images.com/3L6ZtOll9W5O6-3-EwSMyw__small/img/TpNF65wCIb1n3EGW18eJJ3MlRMo=/fit-in/200x150/filters:strip_icc()/pic1899157.jpg"

  - title: "Azul"
    bggId: 230802
    language: en
    pdfFile: "azul_rulebook.pdf"
    imageUrl: "https://cf.geekdo-images.com/aPSHJO0d0XOpQR5X-wJonw__original/img/AkbtYVc6xXJF3c9EUrakklcclKw=/0x0/filters:format(png)/pic6973671.png"
    thumbnailUrl: "https://cf.geekdo-images.com/aPSHJO0d0XOpQR5X-wJonw__small/img/ccsXKrdGJw-YSClWwzVUwk5Nh9Y=/fit-in/200x150/filters:strip_icc()/pic6973671.png"

  - title: "Catan"
    bggId: 13
    language: en
    pdfFile: "catan_rulebook.pdf"
    imageUrl: "https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__original/img/IwRwEpu1I6YfkyYjFIekCh80ntc=/0x0/filters:format(jpeg)/pic2419375.jpg"
    thumbnailUrl: "https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__small/img/7a0LOL48K-2lC3IG0HyYT3XxJBs=/fit-in/200x150/filters:strip_icc()/pic2419375.jpg"

  - title: "Carcassonne"
    bggId: 822
    language: en
    pdfFile: "carcassone_rulebook.pdf"
    imageUrl: "https://cf.geekdo-images.com/peUgu3A20LRmAXAMyDQfpQ__original/img/bP18m_PYjyFOv1IBGgMOteQUneA=/0x0/filters:format(jpeg)/pic8621446.jpg"
    thumbnailUrl: "https://cf.geekdo-images.com/peUgu3A20LRmAXAMyDQfpQ__small/img/oEEslN-EGqh82sNI6Aj4_MFXYg0=/fit-in/200x150/filters:strip_icc()/pic8621446.jpg"

  - title: "Pandemic"
    bggId: 30549
    language: en
    pdfFile: "pandemic_rulebook.pdf"
    imageUrl: "https://cf.geekdo-images.com/S3ybV1LAp-8SnHIXLLjVqA__original/img/IsrvRLpUV1TEyZsO5rC-btXaPz0=/0x0/filters:format(jpeg)/pic1534148.jpg"
    thumbnailUrl: "https://cf.geekdo-images.com/S3ybV1LAp-8SnHIXLLjVqA__small/img/oqViRj6nVxK3m36NluTxU1PZkrk=/fit-in/200x150/filters:strip_icc()/pic1534148.jpg"

  - title: "Chess"
    bggId: null
    language: it
    pdfFile: "scacchi-fide_2017_rulebook.pdf"

  - title: "Splendor"
    bggId: 148228
    language: en
    pdfFile: "splendor_rulebook.pdf"
    imageUrl: "https://cf.geekdo-images.com/vNFe4JkhKAERzi4T0Ntwpw__original/img/rqcUdtu_N4v-SpI96XVmpYHnJww=/0x0/filters:format(png)/pic8234167.png"
    thumbnailUrl: "https://cf.geekdo-images.com/vNFe4JkhKAERzi4T0Ntwpw__small/img/KKU_42Uswt4tKCpf1zY5kTzgr-g=/fit-in/200x150/filters:strip_icc()/pic8234167.png"

  - title: "Ticket to Ride"
    bggId: 9209
    language: en
    pdfFile: "ticket-to-ride_rulebook.pdf"
    imageUrl: "https://cf.geekdo-images.com/kdWYkW-7AqG63HhqPL6ekA__original/img/rWF8r4JXXCQQ7QhiWHhmT-rQ3Pc=/0x0/filters:format(jpeg)/pic8937637.jpg"
    thumbnailUrl: "https://cf.geekdo-images.com/kdWYkW-7AqG63HhqPL6ekA__small/img/5G46jv8MFh_BfX67iMSouTMhKxc=/fit-in/200x150/filters:strip_icc()/pic8937637.jpg"

  - title: "Wingspan"
    bggId: 266192
    language: en
    pdfFile: "wingspan_en_rulebook.pdf"
    imageUrl: "https://cf.geekdo-images.com/yLZJCVLlIx4c7eJEWUNJ7w__original/img/cI782Zis9cT66j2MjSHKJGnFPNw=/0x0/filters:format(jpeg)/pic4458123.jpg"
    thumbnailUrl: "https://cf.geekdo-images.com/yLZJCVLlIx4c7eJEWUNJ7w__small/img/VNToqgS2-pOGU6MuvIkMPKn_y-s=/fit-in/200x150/filters:strip_icc()/pic4458123.jpg"
```

- [ ] **Step 2: Create SeedManifestModels**

```csharp
// apps/api/src/Api/Infrastructure/Seeders/Catalog/SeedManifestModels.cs
namespace Api.Infrastructure.Seeders.Catalog;

/// <summary>
/// Root model for shared-games.yaml manifest.
/// </summary>
public sealed class SharedGamesManifest
{
    public List<GameManifestEntry> Games { get; set; } = [];
}

/// <summary>
/// A single game entry in the seed manifest.
/// </summary>
public sealed class GameManifestEntry
{
    public string Title { get; set; } = string.Empty;
    public int? BggId { get; set; }
    public string Language { get; set; } = "en";
    public string? PdfFile { get; set; }
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
}
```

- [ ] **Step 3: Write failing test for SeedManifestLoader**

```csharp
// tests/Api.Tests/Infrastructure/Seeders/SeedManifestLoaderTests.cs
using Api.Infrastructure.Seeders.Catalog;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders;

public sealed class SeedManifestLoaderTests
{
    [Fact]
    public void LoadFromString_ParsesValidYaml()
    {
        var yaml = """
            games:
              - title: "Catan"
                bggId: 13
                language: en
                pdfFile: "catan_rulebook.pdf"
                imageUrl: "https://example.com/catan.jpg"
                thumbnailUrl: "https://example.com/catan_thumb.jpg"
              - title: "Chess"
                language: it
                pdfFile: "chess_rulebook.pdf"
            """;

        var manifest = SeedManifestLoader.LoadFromString(yaml);

        Assert.Equal(2, manifest.Games.Count);
        Assert.Equal("Catan", manifest.Games[0].Title);
        Assert.Equal(13, manifest.Games[0].BggId);
        Assert.Equal("en", manifest.Games[0].Language);
        Assert.Equal("Chess", manifest.Games[1].Title);
        Assert.Null(manifest.Games[1].BggId);
        Assert.Equal("it", manifest.Games[1].Language);
    }

    [Fact]
    public void LoadFromString_EmptyYaml_ReturnsEmptyList()
    {
        var yaml = "games: []";

        var manifest = SeedManifestLoader.LoadFromString(yaml);

        Assert.Empty(manifest.Games);
    }

    [Fact]
    public void LoadFromString_MissingOptionalFields_UsesDefaults()
    {
        var yaml = """
            games:
              - title: "Test Game"
            """;

        var manifest = SeedManifestLoader.LoadFromString(yaml);

        Assert.Single(manifest.Games);
        Assert.Equal("en", manifest.Games[0].Language);
        Assert.Null(manifest.Games[0].BggId);
        Assert.Null(manifest.Games[0].ImageUrl);
    }

    [Fact]
    public void Validate_RejectsEmptyTitle()
    {
        var manifest = new SharedGamesManifest
        {
            Games = [new GameManifestEntry { Title = "" }]
        };

        var errors = SeedManifestLoader.Validate(manifest);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("title"));
    }

    [Fact]
    public void Validate_RejectsDuplicateTitles()
    {
        var manifest = new SharedGamesManifest
        {
            Games =
            [
                new GameManifestEntry { Title = "Catan" },
                new GameManifestEntry { Title = "Catan" }
            ]
        };

        var errors = SeedManifestLoader.Validate(manifest);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_AcceptsValidManifest()
    {
        var manifest = new SharedGamesManifest
        {
            Games =
            [
                new GameManifestEntry { Title = "Catan", BggId = 13 },
                new GameManifestEntry { Title = "Chess" }
            ]
        };

        var errors = SeedManifestLoader.Validate(manifest);

        Assert.Empty(errors);
    }
}
```

- [ ] **Step 4: Run test to verify it fails**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~SeedManifestLoaderTests" -v quiet`
Expected: FAIL — `SeedManifestLoader` does not exist

- [ ] **Step 5: Implement SeedManifestLoader**

```csharp
// apps/api/src/Api/Infrastructure/Seeders/Catalog/SeedManifestLoader.cs
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Api.Infrastructure.Seeders.Catalog;

/// <summary>
/// Loads and validates YAML seed manifests from data/seed-manifests/.
/// </summary>
internal static class SeedManifestLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Load manifest from a YAML string.
    /// </summary>
    public static SharedGamesManifest LoadFromString(string yaml)
    {
        return Deserializer.Deserialize<SharedGamesManifest>(yaml) ?? new SharedGamesManifest();
    }

    /// <summary>
    /// Load manifest from a file path.
    /// </summary>
    public static SharedGamesManifest LoadFromFile(string filePath, ILogger logger)
    {
        if (!File.Exists(filePath))
        {
            logger.LogWarning("⚠️ Seed manifest not found: {Path}", filePath);
            return new SharedGamesManifest();
        }

        var yaml = File.ReadAllText(filePath);
        var manifest = LoadFromString(yaml);

        logger.LogInformation("📋 Loaded seed manifest: {Path} ({Count} games)", filePath, manifest.Games.Count);
        return manifest;
    }

    /// <summary>
    /// Validate manifest entries. Returns list of error messages (empty = valid).
    /// </summary>
    public static IReadOnlyList<string> Validate(SharedGamesManifest manifest)
    {
        var errors = new List<string>();

        for (var i = 0; i < manifest.Games.Count; i++)
        {
            var game = manifest.Games[i];
            if (string.IsNullOrWhiteSpace(game.Title))
                errors.Add($"Game at index {i} has empty title");
        }

        var duplicates = manifest.Games
            .Where(g => !string.IsNullOrWhiteSpace(g.Title))
            .GroupBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var dup in duplicates)
            errors.Add($"Duplicate game title: '{dup}'");

        return errors;
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~SeedManifestLoaderTests" -v quiet`
Expected: 6 tests pass

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Seeders/Catalog/SeedManifestModels.cs \
        apps/api/src/Api/Infrastructure/Seeders/Catalog/SeedManifestLoader.cs \
        data/seed-manifests/shared-games.yaml \
        tests/Api.Tests/Infrastructure/Seeders/SeedManifestLoaderTests.cs
git commit -m "feat(seeding): add YAML manifest loader and shared-games.yaml (#352)"
```

---

### Task 6: Create CatalogSeedLayer (#352, #353)

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Seeders/Catalog/CatalogSeedLayer.cs`
- Test: `tests/Api.Tests/Infrastructure/Seeders/CatalogSeedLayerTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
// tests/Api.Tests/Infrastructure/Seeders/CatalogSeedLayerTests.cs
using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Catalog;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders;

public sealed class CatalogSeedLayerTests
{
    [Fact]
    public void Name_IsCatalog()
    {
        var layer = new CatalogSeedLayer();
        Assert.Equal("Catalog", layer.Name);
    }

    [Fact]
    public void MinimumProfile_IsStaging()
    {
        var layer = new CatalogSeedLayer();
        Assert.Equal(SeedProfile.Staging, layer.MinimumProfile);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~CatalogSeedLayerTests" -v quiet`
Expected: FAIL

- [ ] **Step 3: Implement CatalogSeedLayer**

This layer delegates to existing static seeders (SharedGameSeeder, BadgeSeeder, etc.) and adds YAML-based SharedGame creation. It wraps all the logic from `AutoConfigurationService.SeedSharedGamesAndRelatedDataAsync()`.

```csharp
// apps/api/src/Api/Infrastructure/Seeders/Catalog/CatalogSeedLayer.cs
using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Infrastructure.Seeders.Catalog;

/// <summary>
/// Catalog seed layer: shared games (YAML + BGG), badges, rate limits, feature flags, PDF rulebooks.
/// Runs in Staging and Dev profiles.
/// </summary>
internal sealed class CatalogSeedLayer : ISeedLayer
{
    public string Name => "Catalog";
    public SeedProfile MinimumProfile => SeedProfile.Staging;

    public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken = default)
    {
        var db = context.DbContext;
        var logger = context.Logger;
        var config = context.Services.GetRequiredService<IConfiguration>();
        var bggService = context.Services.GetRequiredService<IBggApiService>();
        var embeddingService = context.Services.GetRequiredService<IEmbeddingService>();

        // 1. Shared games from BGG data (existing seeder — uses hardcoded dict)
        logger.LogInformation("🌱 [Catalog] Seeding shared games from BGG...");
        await SharedGameSeeder.SeedSharedGamesAsync(
            db, bggService, context.SystemUserId, logger, cancellationToken).ConfigureAwait(false);

        // 2. Catan POC Agent (must run before strategy patterns)
        logger.LogInformation("🌱 [Catalog] Seeding Catan POC Agent...");
        await CatanPocAgentSeeder.SeedAsync(db, logger, cancellationToken: cancellationToken).ConfigureAwait(false);

        // 3. Strategy patterns (configurable)
        var strategyEnabled = config.GetValue("Seeding:EnableStrategyPatterns", true);
        if (strategyEnabled)
        {
            logger.LogInformation("🌱 [Catalog] Seeding strategy patterns...");
            await StrategyPatternSeeder.SeedAsync(db, logger, embeddingService, cancellationToken).ConfigureAwait(false);
        }

        // 4. Badges
        logger.LogInformation("🌱 [Catalog] Seeding badges...");
        await BadgeSeeder.SeedBadgesAsync(db, logger, cancellationToken).ConfigureAwait(false);

        // 5. Rate limit configs
        logger.LogInformation("🌱 [Catalog] Seeding rate limit configs...");
        await RateLimitConfigSeeder.SeedRateLimitConfigsAsync(db, logger, cancellationToken).ConfigureAwait(false);

        // 6. Feature flags
        logger.LogInformation("🌱 [Catalog] Seeding feature flags...");
        await FeatureFlagSeeder.SeedFeatureFlagsAsync(db, context.SystemUserId, logger, cancellationToken).ConfigureAwait(false);

        // 7. PDF rulebooks
        logger.LogInformation("🌱 [Catalog] Seeding PDF rulebooks...");
        await PdfRulebookSeeder.SeedRulebooksAsync(
            db, context.SystemUserId, logger, config["PDF_STORAGE_PATH"], cancellationToken).ConfigureAwait(false);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~CatalogSeedLayerTests" -v quiet`
Expected: 2 tests pass

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Seeders/Catalog/CatalogSeedLayer.cs \
        tests/Api.Tests/Infrastructure/Seeders/CatalogSeedLayerTests.cs
git commit -m "feat(seeding): add CatalogSeedLayer wrapping game/badge/flag seeders (#352, #353)"
```

---

### Task 7: Create LivedInSeedLayer (#353)

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Seeders/LivedIn/LivedInSeedLayer.cs`

- [ ] **Step 1: Create LivedInSeedLayer placeholder**

```csharp
// apps/api/src/Api/Infrastructure/Seeders/LivedIn/LivedInSeedLayer.cs
namespace Api.Infrastructure.Seeders.LivedIn;

/// <summary>
/// LivedIn seed layer: synthetic game sessions, activity data.
/// Only runs in Dev profile for realistic development data.
/// Currently a placeholder — will be expanded with synthetic data generators.
/// </summary>
internal sealed class LivedInSeedLayer : ISeedLayer
{
    public string Name => "LivedIn";
    public SeedProfile MinimumProfile => SeedProfile.Dev;

    public Task SeedAsync(SeedContext context, CancellationToken cancellationToken = default)
    {
        context.Logger.LogInformation("🌱 [LivedIn] Synthetic data seeding (placeholder — no-op)");
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Seeders/LivedIn/LivedInSeedLayer.cs
git commit -m "feat(seeding): add LivedInSeedLayer placeholder for synthetic data (#353)"
```

---

## Chunk 3: Wiring — DI, Program.cs, Endpoint, Obsolete

### Task 8: Register layers and orchestrator in DI (#351)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/DependencyInjection/AdministrationServiceExtensions.cs`

- [ ] **Step 1: Read current file**

Read `AdministrationServiceExtensions.cs` to find where `IAutoConfigurationService` is registered.

- [ ] **Step 2: Add seed layer registrations**

Add after the existing `IAutoConfigurationService` registration:

```csharp
// Layered seeding system (Epic #318)
services.AddScoped<ISeedLayer, CoreSeedLayer>();
services.AddScoped<ISeedLayer, CatalogSeedLayer>();
services.AddScoped<ISeedLayer, LivedInSeedLayer>();
services.AddScoped<SeedOrchestrator>();
```

Required usings:
```csharp
using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Core;
using Api.Infrastructure.Seeders.Catalog;
using Api.Infrastructure.Seeders.LivedIn;
```

- [ ] **Step 3: Build to verify compilation**

Run: `cd apps/api/src/Api && dotnet build --no-restore -v quiet`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Infrastructure/DependencyInjection/AdministrationServiceExtensions.cs
git commit -m "feat(seeding): register seed layers and orchestrator in DI (#351)"
```

---

### Task 9: Update Program.cs to use SeedOrchestrator (#351)

**Files:**
- Modify: `apps/api/src/Api/Program.cs` (lines 397-401)

- [ ] **Step 1: Read current Program.cs seeding block**

Lines 397-401 currently:
```csharp
var autoConfigService = scope.ServiceProvider.GetRequiredService<Api.BoundedContexts.Administration.Application.Services.IAutoConfigurationService>();
await autoConfigService.InitializeAsync().ConfigureAwait(false);
```

- [ ] **Step 2: Replace with SeedOrchestrator call**

Replace the above with:
```csharp
// Epic #318: Layered seeding pipeline with advisory lock
var seedOrchestrator = scope.ServiceProvider.GetRequiredService<Api.Infrastructure.Seeders.SeedOrchestrator>();
await seedOrchestrator.RunAsync(db, scope.ServiceProvider).ConfigureAwait(false);
```

- [ ] **Step 3: Build to verify compilation**

Run: `cd apps/api/src/Api && dotnet build --no-restore -v quiet`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Program.cs
git commit -m "feat(seeding): switch Program.cs from AutoConfigurationService to SeedOrchestrator (#351)"
```

---

### Task 10: Mark AutoConfigurationService as obsolete (#353)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Application/Services/AutoConfigurationService.cs`

- [ ] **Step 1: Add [Obsolete] attribute to class**

```csharp
[Obsolete("Use SeedOrchestrator instead. Will be removed in a future release.")]
internal sealed class AutoConfigurationService : IAutoConfigurationService
```

- [ ] **Step 2: Build to verify**

Run: `cd apps/api/src/Api && dotnet build --no-restore -v quiet`
Expected: 0 errors (warnings OK for obsolete usage)

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Application/Services/AutoConfigurationService.cs
git commit -m "refactor(seeding): mark AutoConfigurationService as obsolete (#353)"
```

---

### Task 11: Create admin seeding endpoint (#351)

**Files:**
- Create: `apps/api/src/Api/Routing/AdminSeedingEndpoints.cs`

- [ ] **Step 1: Implement admin endpoint**

```csharp
// apps/api/src/Api/Routing/AdminSeedingEndpoints.cs
using Api.Filters;
using Api.Infrastructure.Seeders;

namespace Api.Routing;

/// <summary>
/// Admin endpoint for manually triggering the seeding pipeline.
/// </summary>
internal static class AdminSeedingEndpoints
{
    public static void MapAdminSeedingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/admin/seeding")
            .AddEndpointFilter<RequireAdminSessionFilter>()
            .WithTags("Admin - Seeding");

        group.MapPost("/orchestrate", async (
            SeedOrchestrator orchestrator,
            MeepleAiDbContext db,
            IServiceProvider services,
            CancellationToken ct) =>
        {
            await orchestrator.RunAsync(db, services, ct).ConfigureAwait(false);
            return Results.Ok(new { message = "Seeding pipeline completed" });
        })
        .WithName("OrchestrateSeeding")
        .WithSummary("Run the seeding pipeline manually");
    }
}
```

- [ ] **Step 2: Register endpoint in Program.cs**

Find where other admin endpoints are mapped (search for `MapAdmin`) and add:
```csharp
app.MapAdminSeedingEndpoints();
```

- [ ] **Step 3: Build to verify**

Run: `cd apps/api/src/Api && dotnet build --no-restore -v quiet`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Routing/AdminSeedingEndpoints.cs apps/api/src/Api/Program.cs
git commit -m "feat(seeding): add POST /admin/seeding/orchestrate endpoint (#351)"
```

---

## Chunk 4: Dump/Restore Scripts (#354)

### Task 12: Create database dump script

**Files:**
- Create: `infra/scripts/db-dump.ps1`

- [ ] **Step 1: Create dump script**

```powershell
# infra/scripts/db-dump.ps1
# Exports production database, excluding sensitive tables
# Usage: pwsh db-dump.ps1 [-OutputFile dump.sql] [-ExcludeSensitive]
param(
    [string]$OutputFile = "meepleai-dump-$(Get-Date -Format 'yyyy-MM-dd-HHmmss').sql",
    [switch]$ExcludeSensitive = $true
)

$secretFile = Join-Path $PSScriptRoot ".." "secrets" "database.secret"
if (-not (Test-Path $secretFile)) {
    Write-Error "database.secret not found at $secretFile"
    exit 1
}

# Read connection string from secret
$envVars = Get-Content $secretFile | Where-Object { $_ -match '=' } | ForEach-Object {
    $parts = $_ -split '=', 2
    @{ Name = $parts[0].Trim(); Value = $parts[1].Trim() }
}

$dbHost = ($envVars | Where-Object { $_.Name -eq 'POSTGRES_HOST' }).Value ?? 'localhost'
$dbPort = ($envVars | Where-Object { $_.Name -eq 'POSTGRES_PORT' }).Value ?? '5432'
$dbName = ($envVars | Where-Object { $_.Name -eq 'POSTGRES_DB' }).Value ?? 'meepleai'
$dbUser = ($envVars | Where-Object { $_.Name -eq 'POSTGRES_USER' }).Value ?? 'meepleai'

# Tables to exclude (contain sensitive data)
$excludeTables = @(
    "Users",
    "Sessions",
    "RefreshTokens",
    "ApiKeys",
    "AuditLogs"
)

$excludeArgs = if ($ExcludeSensitive) {
    $excludeTables | ForEach-Object { "--exclude-table-data=$_" }
} else { @() }

Write-Host "Dumping database $dbName from ${dbHost}:${dbPort}..."
Write-Host "Output: $OutputFile"
if ($ExcludeSensitive) { Write-Host "Excluding sensitive data from: $($excludeTables -join ', ')" }

$env:PGPASSWORD = ($envVars | Where-Object { $_.Name -eq 'POSTGRES_PASSWORD' }).Value

& pg_dump -h $dbHost -p $dbPort -U $dbUser -d $dbName `
    --format=plain --no-owner --no-privileges `
    @excludeArgs `
    --file=$OutputFile

if ($LASTEXITCODE -eq 0) {
    $size = (Get-Item $OutputFile).Length / 1MB
    Write-Host "Dump completed: $OutputFile ($([math]::Round($size, 2)) MB)"
} else {
    Write-Error "pg_dump failed with exit code $LASTEXITCODE"
    exit 1
}
```

- [ ] **Step 2: Commit**

```bash
git add infra/scripts/db-dump.ps1
git commit -m "feat(infra): add database dump script with sensitive data exclusion (#354)"
```

---

### Task 13: Create database restore script

**Files:**
- Create: `infra/scripts/db-restore.ps1`

- [ ] **Step 1: Create restore script**

```powershell
# infra/scripts/db-restore.ps1
# Restores a database dump to a target environment
# Usage: pwsh db-restore.ps1 -InputFile dump.sql [-TargetDb meepleai_staging]
param(
    [Parameter(Mandatory)]
    [string]$InputFile,
    [string]$TargetDb = "meepleai_staging",
    [switch]$DropExisting = $false
)

if (-not (Test-Path $InputFile)) {
    Write-Error "Input file not found: $InputFile"
    exit 1
}

$secretFile = Join-Path $PSScriptRoot ".." "secrets" "database.secret"
if (-not (Test-Path $secretFile)) {
    Write-Error "database.secret not found at $secretFile"
    exit 1
}

$envVars = Get-Content $secretFile | Where-Object { $_ -match '=' } | ForEach-Object {
    $parts = $_ -split '=', 2
    @{ Name = $parts[0].Trim(); Value = $parts[1].Trim() }
}

$dbHost = ($envVars | Where-Object { $_.Name -eq 'POSTGRES_HOST' }).Value ?? 'localhost'
$dbPort = ($envVars | Where-Object { $_.Name -eq 'POSTGRES_PORT' }).Value ?? '5432'
$dbUser = ($envVars | Where-Object { $_.Name -eq 'POSTGRES_USER' }).Value ?? 'meepleai'
$env:PGPASSWORD = ($envVars | Where-Object { $_.Name -eq 'POSTGRES_PASSWORD' }).Value

Write-Host "Restoring $InputFile to $TargetDb on ${dbHost}:${dbPort}..."

if ($DropExisting) {
    Write-Host "WARNING: Dropping existing database $TargetDb in 5 seconds... (Ctrl+C to cancel)"
    Start-Sleep -Seconds 5
    & psql -h $dbHost -p $dbPort -U $dbUser -c "DROP DATABASE IF EXISTS $TargetDb;"
    & psql -h $dbHost -p $dbPort -U $dbUser -c "CREATE DATABASE $TargetDb;"
}

& psql -h $dbHost -p $dbPort -U $dbUser -d $TargetDb -f $InputFile

if ($LASTEXITCODE -eq 0) {
    Write-Host "Restore completed successfully to $TargetDb"
} else {
    Write-Error "Restore failed with exit code $LASTEXITCODE"
    exit 1
}
```

- [ ] **Step 2: Commit**

```bash
git add infra/scripts/db-restore.ps1
git commit -m "feat(infra): add database restore script for staging (#354)"
```

---

## Chunk 5: Final Verification

### Task 14: Full build + test verification

- [ ] **Step 1: Run full backend build**

Run: `cd apps/api/src/Api && dotnet build --no-restore -v quiet`
Expected: 0 errors, 0 warnings (obsolete warning OK)

- [ ] **Step 2: Run all new seeder tests**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~Infrastructure.Seeders" -v normal`
Expected: All tests pass (≥15 tests)

- [ ] **Step 3: Run full test suite to check no regressions**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "Category=Unit" -v quiet`
Expected: 0 failures

- [ ] **Step 4: Final commit with all changes verified**

```bash
git log --oneline -10  # Verify commit history looks clean
```

---

### Task 15: Close issues on GitHub

- [ ] **Step 1: Update issue #351**
```bash
gh issue close 351 --comment "Implemented: SeedOrchestrator, AdvisoryLockHelper, SeedProfile, ISeedLayer interface, admin endpoint, DI wiring, Program.cs integration"
```

- [ ] **Step 2: Update issue #352**
```bash
gh issue close 352 --comment "Implemented: YAML manifest loader (SeedManifestLoader), shared-games.yaml, SeedManifestModels"
```

- [ ] **Step 3: Update issue #353**
```bash
gh issue close 353 --comment "Implemented: CoreSeedLayer, CatalogSeedLayer, LivedInSeedLayer (placeholder), AutoConfigurationService marked obsolete"
```

- [ ] **Step 4: Update issue #354**
```bash
gh issue close 354 --comment "Implemented: db-dump.ps1 and db-restore.ps1 in infra/scripts/"
```

- [ ] **Step 5: Create PR and request code review**

Use `superpowers:finishing-a-development-branch` to create the PR to `main-dev`.
