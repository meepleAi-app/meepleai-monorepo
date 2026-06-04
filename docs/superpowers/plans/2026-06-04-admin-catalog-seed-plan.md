# Admin Catalog Seed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementare un tool admin per popolare il catalogo `SharedGameCatalogEntry` con import multi-provider (Wikidata primary + BGG fallback whitelisted), lifecycle review-gate, audit completo e safeguard legali coerenti con la spec `2026-06-04-admin-catalog-seed-design.md`.

**Architecture:** Strategy pattern `ICatalogProvider` con 2 implementazioni (Wikidata SPARQL + BGG XML API2 whitelisted), aggregato via `CatalogSeedAggregator` (Wikidata primary, BGG fallback per campi mancanti). Lifecycle `CatalogSeedDraft` (Pending→Fetched→Approved/Rejected/FetchFailed) gestito da Quartz `CatalogSeedFetchJob`. Admin UI riusa componenti SP5 di `/admin/catalog-ingestion` (#1835). Eventi domain dispatchati post-save tramite atomic-save flow #661.

**Tech Stack:** .NET 9 + EF Core 9 (Npgsql), MediatR per CQRS, Quartz.NET per scheduling, Polly per HTTP resilience, xUnit + FluentAssertions + Moq + Testcontainers per tests; Next.js 16 App Router + React 19 + Vitest + Playwright per FE.

---

## Milestones overview

| ID | Titolo | Effort | Blocca |
|---|---|---|---|
| M1 | Domain + Entity + Migration | ~4h | M2-M8 |
| M2 | Provider interfaces + Wikidata implementation | ~5h | M4, M5 |
| M3 | BGG provider + whitelist guard | ~5h | M4, M5 |
| M4 | Aggregator + Commands + Queries | ~6h | M5, M6 |
| M5 | Quartz CatalogSeedFetchJob | ~4h | M6 |
| M6 | SSE Stream Service + Admin Endpoints | ~5h | M7 |
| M7 | BggTosWatcherJob + Feature Flag | ~3h | M8 |
| M8 | Frontend Admin UI + E2E | ~11h | release |
| | **Total** | **~43h** | |

---

## File structure

### Backend new files

| Path | Responsibility |
|---|---|
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/Provenance.cs` | Value object `FieldProvenance` + collection |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Enums/CatalogSeedStatus.cs` | Enum lifecycle states |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Events/CatalogSeedFetchedEvent.cs` | Domain event |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Events/CatalogSeedApprovedEvent.cs` | Domain event |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Events/CatalogSeedRejectedEvent.cs` | Domain event |
| `apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/CatalogSeedDraftEntity.cs` | EF entity |
| `apps/api/src/Api/Infrastructure/EntityConfigurations/SharedGameCatalog/CatalogSeedDraftEntityConfiguration.cs` | EF mapping |
| `apps/api/src/Api/Infrastructure/Migrations/[ts]_AddCatalogSeedDrafts.cs` | DB migration |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/ICatalogProvider.cs` | Provider interface + records |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/ICatalogSeedAggregator.cs` | Aggregator interface |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/CatalogSeedAggregator.cs` | Aggregator impl |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/ICatalogSeedStreamService.cs` | SSE service interface |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/CatalogSeedStreamService.cs` | SSE singleton impl |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Providers/WikidataCatalogProvider.cs` | SPARQL HttpClient |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Providers/BggCatalogProvider.cs` | BGG XML API2 HttpClient |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Providers/BggImportFieldFilter.cs` | Whitelist hard-coded |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Persistence/CatalogSeedDraftRepository.cs` | Repository |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Repositories/ICatalogSeedDraftRepository.cs` | Repo interface |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/EnqueueCatalogSeedCommand.cs` + Handler + Validator | |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/BulkEnqueueCatalogSeedsCommand.cs` + Handler + Validator | |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/ApproveCatalogSeedCommand.cs` + Handler + Validator | |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/RejectCatalogSeedCommand.cs` + Handler + Validator | |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/ListCatalogSeedsQuery.cs` + Handler | |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetCatalogSeedByIdQuery.cs` + Handler | |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Jobs/CatalogSeedFetchJob.cs` | Quartz IJob |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Jobs/BggTosWatcherJob.cs` | Quartz IJob ToS hash |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/EventHandlers/CatalogSeedApprovedEventHandler.cs` | Cross-event handler |
| `apps/api/src/Api/Routing/Admin/AdminCatalogSeedRouting.cs` | Endpoints minimal API |

### Backend modified files

| Path | Reason |
|---|---|
| `apps/api/src/Api/Program.cs` | Register `AdminCatalogSeedRouting` |
| `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/DependencyInjection/SharedGameCatalogServiceExtensions.cs` | Register providers, jobs, repo, services |
| `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs` | `DbSet<CatalogSeedDraftEntity> CatalogSeedDrafts` |
| `apps/api/src/Api/appsettings.json` | Default values BGG/Wikidata client config |
| `apps/api/src/Api/BoundedContexts/SystemConfiguration/Domain/FeatureFlags.cs` | Add `AdminCatalogSeedEnabled` flag |

### Frontend new files

| Path | Responsibility |
|---|---|
| `apps/web/src/app/admin/(dashboard)/catalog/seed-queue/page.tsx` | Route page |
| `apps/web/src/app/admin/(dashboard)/catalog/seed-queue/components/SeedQueueStatusHero.tsx` | KPI hero (riusa `SyncStatusHero`) |
| `apps/web/src/app/admin/(dashboard)/catalog/seed-queue/components/AddBggIdForm.tsx` | Single add (riusa `AssignBggIdForm`) |
| `apps/web/src/app/admin/(dashboard)/catalog/seed-queue/components/BulkPasteSeedModal.tsx` | Bulk paste (riusa `CsvImportModal` pattern) |
| `apps/web/src/app/admin/(dashboard)/catalog/seed-queue/components/WikidataSearchForm.tsx` | SPARQL search nuovo |
| `apps/web/src/app/admin/(dashboard)/catalog/seed-queue/components/SeedPreviewPanel.tsx` | Provenance per-field nuovo |
| `apps/web/src/app/admin/(dashboard)/catalog/seed-queue/components/BggIdValidationBadge.tsx` | Visual validation nuovo |
| `apps/web/src/app/admin/(dashboard)/catalog/seed-queue/hooks/use-catalog-seeds.ts` | React Query fetch |
| `apps/web/src/app/admin/(dashboard)/catalog/seed-queue/hooks/use-catalog-seed-stream.ts` | EventSource SSE |
| `apps/web/src/app/admin/(dashboard)/catalog/seed-queue/lib/catalog-seed-api.ts` | SDK client |
| `apps/web/e2e/admin/catalog-seed.spec.ts` | Playwright E2E |

---

## M1: Domain + Entity + Migration

### Task M1.1: Domain enum + value objects

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Enums/CatalogSeedStatus.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/FieldProvenance.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/CatalogSeedProvenance.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/CatalogSeedProvenanceTests.cs`

- [ ] **Step 1: Write failing tests for value objects**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CatalogSeedProvenanceTests
{
    [Fact]
    public void FieldProvenance_RecordEquality()
    {
        var fetchedAt = new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);
        var a = new FieldProvenance("wikidata", "https://wd/Q1", "labels.en", fetchedAt, "Catan");
        var b = new FieldProvenance("wikidata", "https://wd/Q1", "labels.en", fetchedAt, "Catan");
        a.Should().Be(b);
    }

    [Fact]
    public void Builder_AppliesPrimaryThenFallback()
    {
        var fetchedAt = DateTime.UtcNow;
        var primary = new Dictionary<string, FieldProvenance>
        {
            ["title"] = new("wikidata", "u1", "f1", fetchedAt, "Catan"),
        };
        var fallback = new Dictionary<string, FieldProvenance>
        {
            ["title"] = new("bgg", "u2", "f2", fetchedAt, "Settlers of Catan"),
            ["mechanics"] = new("bgg", "u3", "f3", fetchedAt, new[] { "Trading" }),
        };

        var merged = CatalogSeedProvenance.Merge(primary, fallback);

        merged.GetValue<string>("title").Should().Be("Catan");           // primary wins
        merged.GetProvider("title").Should().Be("wikidata");
        merged.GetValue<string[]>("mechanics").Should().BeEquivalentTo(new[] { "Trading" });
        merged.GetProvider("mechanics").Should().Be("bgg");
    }

    [Fact]
    public void Serialize_RoundTrip()
    {
        var fetchedAt = new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);
        var p = new CatalogSeedProvenance(new Dictionary<string, FieldProvenance>
        {
            ["title"] = new("wikidata", "u1", "f1", fetchedAt, "Catan"),
        });
        var json = p.ToJson();
        var roundTripped = CatalogSeedProvenance.FromJson(json);
        roundTripped.GetValue<string>("title").Should().Be("Catan");
    }
}
```

- [ ] **Step 2: Run test to verify it fails (compile error)**

Run: `cd D:/Repositories/meepleai-monorepo-main && dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CatalogSeedProvenanceTests" --nologo 2>&1 | tail -5`
Expected: COMPILE FAIL "Provenance not found"

- [ ] **Step 3: Implement CatalogSeedStatus enum**

```csharp
namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>
/// Lifecycle states for CatalogSeedDraft entries (admin import workflow).
/// Persisted as string for forward compatibility (varchar(32) column).
/// </summary>
public enum CatalogSeedStatus
{
    /// <summary>Just enqueued by admin, awaiting provider fetch.</summary>
    Pending = 0,

    /// <summary>Provider fetch completed, awaiting admin review.</summary>
    Fetched = 1,

    /// <summary>Provider fetch failed after N retries. Admin can manual-fill.</summary>
    FetchFailed = 2,

    /// <summary>Admin approved; copied into SharedGameCatalogEntry.</summary>
    Approved = 3,

    /// <summary>Admin rejected; soft-deleted (audit retained).</summary>
    Rejected = 4,
}
```

- [ ] **Step 4: Implement FieldProvenance record**

```csharp
namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Per-field provenance metadata captured during catalog seed enrichment.
/// Records which provider supplied the value, the source URL, the source-side
/// field identifier (e.g. Wikidata property "P577"), and when fetched.
/// </summary>
/// <param name="Provider">"wikidata" or "bgg"</param>
/// <param name="SourceUrl">Public URL of the data source</param>
/// <param name="SourceField">Provider-side field path (e.g. "P577", "link[type=boardgamemechanic]")</param>
/// <param name="FetchedAt">UTC timestamp when the field was last fetched</param>
/// <param name="Value">The raw value (boxed primitive, string, or IReadOnlyList&lt;string&gt;)</param>
public sealed record FieldProvenance(
    string Provider,
    string SourceUrl,
    string SourceField,
    DateTime FetchedAt,
    object Value);
```

- [ ] **Step 5: Implement CatalogSeedProvenance aggregate**

```csharp
using System.Text.Json;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Aggregate of <see cref="FieldProvenance"/> entries for a CatalogSeedDraft.
/// Provides merging (primary + fallback chain) and JSON serialization for
/// persistence on <c>CatalogSeedDraftEntity.ProvenanceJson</c>.
/// </summary>
public sealed class CatalogSeedProvenance
{
    private readonly Dictionary<string, FieldProvenance> _fields;

    public CatalogSeedProvenance(IReadOnlyDictionary<string, FieldProvenance> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        _fields = new Dictionary<string, FieldProvenance>(fields, StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, FieldProvenance> Fields => _fields;

    public T? GetValue<T>(string fieldName)
        => _fields.TryGetValue(fieldName, out var fp) && fp.Value is T t ? t : default;

    public string? GetProvider(string fieldName)
        => _fields.TryGetValue(fieldName, out var fp) ? fp.Provider : null;

    /// <summary>
    /// Merges primary and fallback dictionaries. Primary entries win on conflict;
    /// fallback fills missing fields only.
    /// </summary>
    public static CatalogSeedProvenance Merge(
        IReadOnlyDictionary<string, FieldProvenance> primary,
        IReadOnlyDictionary<string, FieldProvenance> fallback)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(fallback);

        var merged = new Dictionary<string, FieldProvenance>(primary, StringComparer.Ordinal);
        foreach (var kv in fallback)
        {
            if (!merged.ContainsKey(kv.Key))
            {
                merged[kv.Key] = kv.Value;
            }
        }
        return new CatalogSeedProvenance(merged);
    }

    public string ToJson() => JsonSerializer.Serialize(_fields, JsonOpts);

    public static CatalogSeedProvenance FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var dict = JsonSerializer.Deserialize<Dictionary<string, FieldProvenance>>(json, JsonOpts)
                   ?? new Dictionary<string, FieldProvenance>(StringComparer.Ordinal);
        return new CatalogSeedProvenance(dict);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };
}
```

- [ ] **Step 6: Run tests to verify GREEN**

Run: `cd D:/Repositories/meepleai-monorepo-main && dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CatalogSeedProvenanceTests" --nologo --logger "console;verbosity=minimal" 2>&1 | tail -5`
Expected: PASS 3/3

- [ ] **Step 7: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-main
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Enums/CatalogSeedStatus.cs
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/
git add apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/
git commit -m "feat(catalog-seed): domain enum + provenance value objects (M1.1)"
```

### Task M1.2: Domain events

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Events/CatalogSeedFetchedEvent.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Events/CatalogSeedApprovedEvent.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Events/CatalogSeedRejectedEvent.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/Events/CatalogSeedEventTests.cs`

- [ ] **Step 1: Write failing tests for the 3 events**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Events;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public class CatalogSeedEventTests
{
    [Fact]
    public void FetchedEvent_SetsAllProperties()
    {
        var draftId = Guid.NewGuid();
        var evt = new CatalogSeedFetchedEvent(draftId, providerUsed: "wikidata+bgg", fetchedFields: 8);
        evt.DraftId.Should().Be(draftId);
        evt.ProviderUsed.Should().Be("wikidata+bgg");
        evt.FetchedFields.Should().Be(8);
    }

    [Fact]
    public void ApprovedEvent_CarriesResultingSharedGameId()
    {
        var draftId = Guid.NewGuid();
        var sgId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var evt = new CatalogSeedApprovedEvent(draftId, sgId, approverId);
        evt.DraftId.Should().Be(draftId);
        evt.ResultingSharedGameId.Should().Be(sgId);
        evt.ApprovedByUserId.Should().Be(approverId);
    }

    [Fact]
    public void RejectedEvent_CarriesReason()
    {
        var draftId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var evt = new CatalogSeedRejectedEvent(draftId, approverId, reason: "Duplicate of BGG:13");
        evt.DraftId.Should().Be(draftId);
        evt.RejectedByUserId.Should().Be(approverId);
        evt.Reason.Should().Be("Duplicate of BGG:13");
    }
}
```

- [ ] **Step 2: Run failing**

Run: `cd D:/Repositories/meepleai-monorepo-main && dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CatalogSeedEventTests" --nologo 2>&1 | tail -5`
Expected: COMPILE FAIL

- [ ] **Step 3: Implement events**

```csharp
// CatalogSeedFetchedEvent.cs
using Api.SharedKernel.Domain.Events;
namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

internal sealed class CatalogSeedFetchedEvent : DomainEventBase
{
    public Guid DraftId { get; }
    public string ProviderUsed { get; }   // "wikidata" | "bgg" | "wikidata+bgg"
    public int FetchedFields { get; }

    public CatalogSeedFetchedEvent(Guid draftId, string providerUsed, int fetchedFields)
    {
        DraftId = draftId;
        ProviderUsed = providerUsed;
        FetchedFields = fetchedFields;
    }
}
```

```csharp
// CatalogSeedApprovedEvent.cs
using Api.SharedKernel.Domain.Events;
namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

internal sealed class CatalogSeedApprovedEvent : DomainEventBase
{
    public Guid DraftId { get; }
    public Guid ResultingSharedGameId { get; }
    public Guid ApprovedByUserId { get; }

    public CatalogSeedApprovedEvent(Guid draftId, Guid resultingSharedGameId, Guid approvedByUserId)
    {
        DraftId = draftId;
        ResultingSharedGameId = resultingSharedGameId;
        ApprovedByUserId = approvedByUserId;
    }
}
```

```csharp
// CatalogSeedRejectedEvent.cs
using Api.SharedKernel.Domain.Events;
namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

internal sealed class CatalogSeedRejectedEvent : DomainEventBase
{
    public Guid DraftId { get; }
    public Guid RejectedByUserId { get; }
    public string Reason { get; }

    public CatalogSeedRejectedEvent(Guid draftId, Guid rejectedByUserId, string reason)
    {
        DraftId = draftId;
        RejectedByUserId = rejectedByUserId;
        Reason = reason ?? string.Empty;
    }
}
```

NOTE: tests use `new CatalogSeedFetchedEvent(...)` — events must be `internal` (matches `PdfCoverGeneratedEvent` pattern). Tests are in same assembly via `InternalsVisibleTo` (already configured per CLAUDE.md).

- [ ] **Step 4: Run GREEN**

Run: `cd D:/Repositories/meepleai-monorepo-main && dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CatalogSeedEventTests" --nologo --logger "console;verbosity=minimal" 2>&1 | tail -5`
Expected: PASS 3/3

- [ ] **Step 5: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-main
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Events/
git add apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/Events/CatalogSeedEventTests.cs
git commit -m "feat(catalog-seed): domain events Fetched/Approved/Rejected (M1.2)"
```

### Task M1.3: EF Entity + Configuration

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/CatalogSeedDraftEntity.cs`
- Create: `apps/api/src/Api/Infrastructure/EntityConfigurations/SharedGameCatalog/CatalogSeedDraftEntityConfiguration.cs`
- Modify: `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs` (add `DbSet`)

- [ ] **Step 1: Create entity**

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Admin-curated draft entry from catalog seed import workflow.
/// One row per BGG ID / Wikidata Qid the admin enqueued. Lifecycle:
/// Pending → Fetched → Approved or Rejected (or FetchFailed).
/// Spec: docs/superpowers/specs/2026-06-04-admin-catalog-seed-design.md §4.2
/// </summary>
public class CatalogSeedDraftEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int? BggId { get; set; }
    public string? WikidataQid { get; set; }
    public string? SearchTermInput { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = "Pending"; // CatalogSeedStatus enum as string

    [Column(TypeName = "jsonb")]
    public string? ProvenanceJson { get; set; }

    [Column(TypeName = "jsonb")]
    public string? RawPayloadJson { get; set; }

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }

    public Guid? ResultingSharedGameId { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FetchedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
```

- [ ] **Step 2: Create configuration**

```csharp
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SharedGameCatalog;

internal sealed class CatalogSeedDraftEntityConfiguration : IEntityTypeConfiguration<CatalogSeedDraftEntity>
{
    public void Configure(EntityTypeBuilder<CatalogSeedDraftEntity> builder)
    {
        builder.ToTable("catalog_seed_drafts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.BggId);
        builder.Property(x => x.WikidataQid).HasMaxLength(32);
        builder.Property(x => x.SearchTermInput).HasMaxLength(255);
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ProvenanceJson).HasColumnType("jsonb");
        builder.Property(x => x.RawPayloadJson).HasColumnType("jsonb");
        builder.Property(x => x.ErrorMessage).HasMaxLength(500);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.BggId).IsUnique(false);
        builder.HasIndex(x => x.WikidataQid).IsUnique(false);
        builder.HasIndex(x => x.CreatedAt);

        // Soft-delete filter (CLAUDE.md pattern)
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
```

- [ ] **Step 3: Add DbSet to MeepleAiDbContext**

Locate `MeepleAiDbContext.cs` and add (alphabetical order with other DbSets in the SharedGameCatalog region):

```csharp
public DbSet<CatalogSeedDraftEntity> CatalogSeedDrafts => Set<CatalogSeedDraftEntity>();
```

Add `using Api.Infrastructure.Entities.SharedGameCatalog;` at top if missing.

- [ ] **Step 4: Verify build compiles**

Run: `cd D:/Repositories/meepleai-monorepo-main && dotnet build apps/api/src/Api/Api.csproj -c Debug --nologo 2>&1 | tail -5`
Expected: 0 errors

- [ ] **Step 5: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-main
git add apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/CatalogSeedDraftEntity.cs
git add apps/api/src/Api/Infrastructure/EntityConfigurations/SharedGameCatalog/CatalogSeedDraftEntityConfiguration.cs
git add apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs
git commit -m "feat(catalog-seed): EF entity + configuration + DbContext (M1.3)"
```

### Task M1.4: DB Migration

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/[timestamp]_AddCatalogSeedDrafts.cs` (auto-generated)
- Create: `apps/api/src/Api/Infrastructure/Migrations/[timestamp]_AddCatalogSeedDrafts.Designer.cs` (auto-generated)

- [ ] **Step 1: Generate migration**

```bash
cd D:/Repositories/meepleai-monorepo-main/apps/api/src/Api
dotnet ef migrations add AddCatalogSeedDrafts --output-dir Infrastructure/Migrations
```

Expected: 2 new files created, snapshot updated.

- [ ] **Step 2: Inspect generated SQL**

Open the new migration file. Verify:
- `catalog_seed_drafts` table created with all columns
- Indexes on `status`, `bgg_id`, `wikidata_qid`, `created_at`
- `row_version` column type is `bytea` (PostgreSQL)
- `provenance_json`, `raw_payload_json` are `jsonb` columns

- [ ] **Step 3: Apply migration locally**

```bash
cd D:/Repositories/meepleai-monorepo-main/apps/api/src/Api
dotnet ef database update
```

Expected: migration applied, no errors.

- [ ] **Step 4: Verify rollback clean**

```bash
cd D:/Repositories/meepleai-monorepo-main/apps/api/src/Api
dotnet ef database update <previous-migration-name>
dotnet ef database update
```

Expected: clean down + re-up, no errors.

- [ ] **Step 5: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-main
git add apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(catalog-seed): DB migration AddCatalogSeedDrafts (M1.4)"
```

### Task M1.5: Repository interface + implementation

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Repositories/ICatalogSeedDraftRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Persistence/CatalogSeedDraftRepository.cs`
- Test: `apps/api/tests/Api.Tests/Integration/SharedGameCatalog/CatalogSeedDraftRepositoryTests.cs`

- [ ] **Step 1: Define interface**

```csharp
using Api.Infrastructure.Entities.SharedGameCatalog;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

internal interface ICatalogSeedDraftRepository
{
    Task<CatalogSeedDraftEntity?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<CatalogSeedDraftEntity>> GetByStatusAsync(string status, int take, CancellationToken ct);
    Task<IReadOnlyList<CatalogSeedDraftEntity>> ListAsync(
        string? statusFilter, int skip, int take, CancellationToken ct);
    Task<int> CountAsync(string? statusFilter, CancellationToken ct);
    Task AddAsync(CatalogSeedDraftEntity entity, CancellationToken ct);
    Task AddRangeAsync(IReadOnlyList<CatalogSeedDraftEntity> entities, CancellationToken ct);
    void Update(CatalogSeedDraftEntity entity);
    Task<bool> ExistsAsync(int bggId, CancellationToken ct);
}
```

- [ ] **Step 2: Write integration test (Testcontainers)**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Persistence;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Collection("Integration-GroupC")]
public sealed class CatalogSeedDraftRepositoryTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fx;
    private string _dbName = string.Empty;
    private string _connStr = string.Empty;

    public CatalogSeedDraftRepositoryTests(SharedTestcontainersFixture fx) => _fx = fx;

    public async ValueTask InitializeAsync()
    {
        _dbName = $"test_catseedrepo_{Guid.NewGuid():N}";
        _connStr = await _fx.CreateIsolatedDatabaseAsync(_dbName);
        await using var ctx = _fx.CreateDbContext(_connStr);
        await ctx.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _fx.DropIsolatedDatabaseAsync(_dbName);

    [Fact]
    public async Task AddAsync_PersistsAndReturnsViaGetByIdAsync()
    {
        await using var ctx = _fx.CreateDbContext(_connStr);
        var repo = new CatalogSeedDraftRepository(ctx);

        var entity = new CatalogSeedDraftEntity
        {
            BggId = 13,
            Status = "Pending",
            CreatedByUserId = Guid.NewGuid(),
        };
        await repo.AddAsync(entity, default);
        await ctx.SaveChangesAsync();

        var fetched = await repo.GetByIdAsync(entity.Id, default);
        fetched.Should().NotBeNull();
        fetched!.BggId.Should().Be(13);
    }

    [Fact]
    public async Task GetByStatusAsync_FiltersAndLimits()
    {
        await using var ctx = _fx.CreateDbContext(_connStr);
        var repo = new CatalogSeedDraftRepository(ctx);
        var userId = Guid.NewGuid();

        for (var i = 0; i < 7; i++)
        {
            await repo.AddAsync(new CatalogSeedDraftEntity
            {
                BggId = i,
                Status = i < 4 ? "Pending" : "Fetched",
                CreatedByUserId = userId,
            }, default);
        }
        await ctx.SaveChangesAsync();

        var pending = await repo.GetByStatusAsync("Pending", take: 10, default);
        pending.Should().HaveCount(4);

        var top2 = await repo.GetByStatusAsync("Pending", take: 2, default);
        top2.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExistsAsync_ChecksBggId()
    {
        await using var ctx = _fx.CreateDbContext(_connStr);
        var repo = new CatalogSeedDraftRepository(ctx);

        await repo.AddAsync(new CatalogSeedDraftEntity
        {
            BggId = 99,
            Status = "Pending",
            CreatedByUserId = Guid.NewGuid(),
        }, default);
        await ctx.SaveChangesAsync();

        (await repo.ExistsAsync(99, default)).Should().BeTrue();
        (await repo.ExistsAsync(100, default)).Should().BeFalse();
    }
}
```

- [ ] **Step 3: Run RED**

Run: `cd D:/Repositories/meepleai-monorepo-main && dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CatalogSeedDraftRepositoryTests" --nologo 2>&1 | tail -8`
Expected: COMPILE FAIL "CatalogSeedDraftRepository not found"

- [ ] **Step 4: Implement repository**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Persistence;

internal sealed class CatalogSeedDraftRepository : ICatalogSeedDraftRepository
{
    private readonly MeepleAiDbContext _db;
    public CatalogSeedDraftRepository(MeepleAiDbContext db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    public Task<CatalogSeedDraftEntity?> GetByIdAsync(Guid id, CancellationToken ct)
        => _db.CatalogSeedDrafts.AsTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<CatalogSeedDraftEntity>> GetByStatusAsync(string status, int take, CancellationToken ct)
        => await _db.CatalogSeedDrafts
            .AsTracking()
            .Where(x => x.Status == status)
            .OrderBy(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CatalogSeedDraftEntity>> ListAsync(
        string? statusFilter, int skip, int take, CancellationToken ct)
    {
        var q = _db.CatalogSeedDrafts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            q = q.Where(x => x.Status == statusFilter);
        }
        return await q.OrderByDescending(x => x.CreatedAt).Skip(skip).Take(take).ToListAsync(ct);
    }

    public Task<int> CountAsync(string? statusFilter, CancellationToken ct)
    {
        var q = _db.CatalogSeedDrafts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            q = q.Where(x => x.Status == statusFilter);
        }
        return q.CountAsync(ct);
    }

    public async Task AddAsync(CatalogSeedDraftEntity entity, CancellationToken ct)
        => await _db.CatalogSeedDrafts.AddAsync(entity, ct);

    public Task AddRangeAsync(IReadOnlyList<CatalogSeedDraftEntity> entities, CancellationToken ct)
        => _db.CatalogSeedDrafts.AddRangeAsync(entities, ct);

    public void Update(CatalogSeedDraftEntity entity) => _db.CatalogSeedDrafts.Update(entity);

    public Task<bool> ExistsAsync(int bggId, CancellationToken ct)
        => _db.CatalogSeedDrafts.AsNoTracking().AnyAsync(x => x.BggId == bggId, ct);
}
```

- [ ] **Step 5: Run GREEN (requires Docker)**

Run: `cd D:/Repositories/meepleai-monorepo-main && dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CatalogSeedDraftRepositoryTests" --nologo --logger "console;verbosity=minimal" 2>&1 | tail -5`
Expected: PASS 3/3 (if Docker available; otherwise CI will run it)

- [ ] **Step 6: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-main
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Repositories/ICatalogSeedDraftRepository.cs
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Persistence/CatalogSeedDraftRepository.cs
git add apps/api/tests/Api.Tests/Integration/SharedGameCatalog/CatalogSeedDraftRepositoryTests.cs
git commit -m "feat(catalog-seed): repository + integration tests (M1.5)"
```

---

## M2: Provider interfaces + Wikidata implementation

### Task M2.1: ICatalogProvider interface + records

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/ICatalogProvider.cs`

- [ ] **Step 1: Define interface + supporting records**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

internal interface ICatalogProvider
{
    /// <summary>Provider identifier: "wikidata" | "bgg".</summary>
    string Name { get; }

    Task<CatalogProviderResult> FetchAsync(CatalogProviderQuery query, CancellationToken ct);
}

internal sealed record CatalogProviderQuery(int? BggId, string? WikidataQid, string? SearchTerm);

internal sealed record CatalogProviderResult(
    IReadOnlyDictionary<string, FieldProvenance> Fields,
    string? RawPayloadJson,
    string? ErrorMessage)
{
    public bool Success => ErrorMessage is null && Fields.Count > 0;
    public static CatalogProviderResult Empty(string error) =>
        new(new Dictionary<string, FieldProvenance>(StringComparer.Ordinal), null, error);
}
```

- [ ] **Step 2: Verify build**

Run: `cd D:/Repositories/meepleai-monorepo-main && dotnet build apps/api/src/Api/Api.csproj --nologo 2>&1 | tail -3`
Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-main
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/ICatalogProvider.cs
git commit -m "feat(catalog-seed): ICatalogProvider interface + records (M2.1)"
```

### Task M2.2: WikidataCatalogProvider implementation

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Providers/WikidataCatalogProvider.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/Providers/WikidataCatalogProviderTests.cs`

- [ ] **Step 1: Write failing tests with HttpClient mock**

```csharp
using System.Net;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class WikidataCatalogProviderTests
{
    private static HttpClient MakeClient(HttpStatusCode status, string body)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/sparql-results+json"),
            });
        return new HttpClient(handler.Object) { BaseAddress = new Uri("https://query.wikidata.org/") };
    }

    [Fact]
    public async Task Name_Equals_Wikidata()
    {
        var provider = new WikidataCatalogProvider(MakeClient(HttpStatusCode.OK, "{}"), NullLogger<WikidataCatalogProvider>.Instance);
        provider.Name.Should().Be("wikidata");
    }

    [Fact]
    public async Task FetchAsync_ByBggId_MapsCoreFields()
    {
        const string body = """
        { "results": { "bindings": [{
          "game":        {"value": "http://www.wikidata.org/entity/Q98056728"},
          "gameLabel":   {"value": "Catan"},
          "yearPublished":{"value": "1995-01-01T00:00:00Z","datatype":"http://www.w3.org/2001/XMLSchema#dateTime"},
          "designerLabel":{"value": "Klaus Teuber"},
          "publisherLabel":{"value": "Kosmos"},
          "minPlayers":  {"value": "3"},
          "maxPlayers":  {"value": "4"},
          "playingTimeMinutes":{"value": "60"}
        }]}}
        """;
        var provider = new WikidataCatalogProvider(MakeClient(HttpStatusCode.OK, body), NullLogger<WikidataCatalogProvider>.Instance);
        var result = await provider.FetchAsync(new CatalogProviderQuery(BggId: 13, null, null), default);

        result.Success.Should().BeTrue();
        result.Fields["title"].Value.Should().Be("Catan");
        result.Fields["title"].Provider.Should().Be("wikidata");
        result.Fields["yearPublished"].Value.Should().Be(1995);
        result.Fields["designers"].Value.Should().BeOfType<List<string>>()
            .Which.Should().Contain("Klaus Teuber");
        result.Fields["minPlayers"].Value.Should().Be(3);
        result.Fields["maxPlayers"].Value.Should().Be(4);
        result.Fields["playingTimeMinutes"].Value.Should().Be(60);
        result.Fields["wikidataQid"].Value.Should().Be("Q98056728");
    }

    [Fact]
    public async Task FetchAsync_NoResults_ReturnsEmptyWithError()
    {
        const string body = """{"results":{"bindings":[]}}""";
        var provider = new WikidataCatalogProvider(MakeClient(HttpStatusCode.OK, body), NullLogger<WikidataCatalogProvider>.Instance);
        var result = await provider.FetchAsync(new CatalogProviderQuery(99999, null, null), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task FetchAsync_HttpError_ReturnsErrorMessage()
    {
        var provider = new WikidataCatalogProvider(MakeClient(HttpStatusCode.InternalServerError, "boom"), NullLogger<WikidataCatalogProvider>.Instance);
        var result = await provider.FetchAsync(new CatalogProviderQuery(13, null, null), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("HTTP");
    }
}
```

- [ ] **Step 2: Run RED**

Run: `cd D:/Repositories/meepleai-monorepo-main && dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~WikidataCatalogProviderTests" --nologo 2>&1 | tail -8`
Expected: COMPILE FAIL

- [ ] **Step 3: Implement provider**

```csharp
using System.Net.Http.Headers;
using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;

/// <summary>
/// Primary catalog provider — Wikidata SPARQL endpoint.
/// Spec: 2026-06-04-admin-catalog-seed-design.md §7.1.
/// License: CC0 (all data) — no attribution required.
/// </summary>
internal sealed class WikidataCatalogProvider : ICatalogProvider
{
    public string Name => "wikidata";

    private const string SparqlPath = "sparql";
    private readonly HttpClient _http;
    private readonly ILogger<WikidataCatalogProvider> _logger;

    public WikidataCatalogProvider(HttpClient http, ILogger<WikidataCatalogProvider> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CatalogProviderResult> FetchAsync(CatalogProviderQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.BggId is null && query.WikidataQid is null && string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            return CatalogProviderResult.Empty("Missing query parameters");
        }

        var sparql = BuildSparql(query);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{SparqlPath}?query={Uri.EscapeDataString(sparql)}&format=json");
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/sparql-results+json"));

            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Wikidata SPARQL HTTP {Status} on query: {Sparql}", (int)resp.StatusCode, sparql);
                return CatalogProviderResult.Empty($"HTTP {(int)resp.StatusCode}");
            }

            return ParseResponse(body, query, sourceUrl: BuildSourceUrl(query));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Wikidata fetch failed");
            return CatalogProviderResult.Empty(ex.Message);
        }
    }

    private static string BuildSparql(CatalogProviderQuery q)
    {
        var bind = q.BggId.HasValue
            ? $"?game wdt:P2339 \"{q.BggId.Value}\"."
            : q.WikidataQid is not null
                ? $"BIND(wd:{q.WikidataQid} AS ?game)"
                : $"?game rdfs:label \"{q.SearchTerm}\"@en.";

        return $@"
SELECT ?game ?gameLabel ?yearPublished ?designerLabel ?publisherLabel
       ?minPlayers ?maxPlayers ?playingTimeMinutes
WHERE {{
  {bind}
  OPTIONAL {{ ?game wdt:P577 ?yearPublished. }}
  OPTIONAL {{ ?game wdt:P178 ?designer. }}
  OPTIONAL {{ ?game wdt:P123 ?publisher. }}
  OPTIONAL {{ ?game wdt:P1873 ?minPlayers. }}
  OPTIONAL {{ ?game wdt:P1872 ?maxPlayers. }}
  OPTIONAL {{ ?game wdt:P2047 ?playingTimeMinutes. }}
  SERVICE wikibase:label {{ bd:serviceParam wikibase:language ""en"". }}
}}
LIMIT 1";
    }

    private static string BuildSourceUrl(CatalogProviderQuery q)
    {
        if (q.WikidataQid is not null) return $"https://www.wikidata.org/wiki/{q.WikidataQid}";
        if (q.BggId.HasValue) return $"https://query.wikidata.org/?bggid={q.BggId}";
        return "https://query.wikidata.org/";
    }

    private static CatalogProviderResult ParseResponse(string body, CatalogProviderQuery q, string sourceUrl)
    {
        var fetchedAt = DateTime.UtcNow;
        using var doc = JsonDocument.Parse(body);
        var bindings = doc.RootElement
            .GetProperty("results")
            .GetProperty("bindings");

        if (bindings.GetArrayLength() == 0)
        {
            return CatalogProviderResult.Empty("Wikidata: entity not found");
        }

        var row = bindings[0];
        var fields = new Dictionary<string, FieldProvenance>(StringComparer.Ordinal);

        string? Get(string key) => row.TryGetProperty(key, out var el) && el.TryGetProperty("value", out var v) ? v.GetString() : null;

        // Title
        var title = Get("gameLabel");
        if (!string.IsNullOrWhiteSpace(title))
        {
            fields["title"] = new FieldProvenance("wikidata", sourceUrl, "labels.en", fetchedAt, title);
        }

        // QID extraction from game URI
        var gameUri = Get("game");
        if (gameUri is not null && gameUri.StartsWith("http://www.wikidata.org/entity/", StringComparison.Ordinal))
        {
            var qid = gameUri["http://www.wikidata.org/entity/".Length..];
            fields["wikidataQid"] = new FieldProvenance("wikidata", sourceUrl, "item URI", fetchedAt, qid);
        }

        // Year
        var yearRaw = Get("yearPublished");
        if (yearRaw is not null && DateTimeOffset.TryParse(yearRaw, out var dt))
        {
            fields["yearPublished"] = new FieldProvenance("wikidata", sourceUrl, "P577", fetchedAt, dt.Year);
        }

        // Designers (single occurrence per SELECT; multi-value via separate query if needed)
        var designer = Get("designerLabel");
        if (!string.IsNullOrWhiteSpace(designer))
        {
            fields["designers"] = new FieldProvenance("wikidata", sourceUrl, "P178", fetchedAt, new List<string> { designer });
        }

        var publisher = Get("publisherLabel");
        if (!string.IsNullOrWhiteSpace(publisher))
        {
            fields["publishers"] = new FieldProvenance("wikidata", sourceUrl, "P123", fetchedAt, new List<string> { publisher });
        }

        if (int.TryParse(Get("minPlayers"), out var mn))
        {
            fields["minPlayers"] = new FieldProvenance("wikidata", sourceUrl, "P1873", fetchedAt, mn);
        }
        if (int.TryParse(Get("maxPlayers"), out var mx))
        {
            fields["maxPlayers"] = new FieldProvenance("wikidata", sourceUrl, "P1872", fetchedAt, mx);
        }
        if (int.TryParse(Get("playingTimeMinutes"), out var pt))
        {
            fields["playingTimeMinutes"] = new FieldProvenance("wikidata", sourceUrl, "P2047", fetchedAt, pt);
        }

        return new CatalogProviderResult(fields, body, ErrorMessage: null);
    }
}
```

- [ ] **Step 4: Run GREEN**

Run: `cd D:/Repositories/meepleai-monorepo-main && dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~WikidataCatalogProviderTests" --nologo --logger "console;verbosity=minimal" 2>&1 | tail -5`
Expected: PASS 4/4

- [ ] **Step 5: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-main
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Providers/WikidataCatalogProvider.cs
git add apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/Providers/WikidataCatalogProviderTests.cs
git commit -m "feat(catalog-seed): WikidataCatalogProvider SPARQL impl + tests (M2.2)"
```

---

## M3: BGG provider + whitelist guard

### Task M3.1: BggImportFieldFilter whitelist + guard test

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Providers/BggImportFieldFilter.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/Providers/BggImportFieldFilterTests.cs`

- [ ] **Step 1: Write failing guard tests**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Compliance", "BggToS")]
public class BggImportFieldFilterTests
{
    [Fact]
    public void AllowedFields_ContainsOnlyFactualMetadata()
    {
        // Spec §7.2 — these MUST be the only allowed BGG fields. Any addition
        // requires legal review per spec §8.5.6 pre-rollout checklist.
        BggImportFieldFilter.AllowedFields.Should().BeEquivalentTo(new[]
        {
            "name", "yearpublished",
            "minplayers", "maxplayers",
            "playingtime", "minplaytime", "maxplaytime",
            "minage",
            "link[type=boardgamedesigner]",
            "link[type=boardgamepublisher]",
            "link[type=boardgameartist]",
            "link[type=boardgamemechanic]",
            "link[type=boardgamecategory]",
            "link[type=boardgamefamily]",
        });
    }

    [Fact]
    public void ForbiddenFields_RejectsCopyrightedContent()
    {
        // Spec §8.5.3 + §8.5.2: description/image/comments/statistics are
        // either copyrighted (Feist excluded) or DB sui generis (EU).
        BggImportFieldFilter.ForbiddenFields.Should().Contain(new[]
        {
            "description", "image", "thumbnail",
            "statistics", "comments", "videos",
        });
    }

    [Fact]
    public void AllowedAndForbidden_AreDisjoint()
    {
        BggImportFieldFilter.AllowedFields
            .Intersect(BggImportFieldFilter.ForbiddenFields, StringComparer.Ordinal)
            .Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run RED**

Run: `cd D:/Repositories/meepleai-monorepo-main && dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~BggImportFieldFilterTests" --nologo 2>&1 | tail -5`
Expected: COMPILE FAIL

- [ ] **Step 3: Implement filter**

```csharp
namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;

/// <summary>
/// Hard-coded whitelist of BGG XML API2 fields that may be imported by
/// MeepleAI. ANY change requires legal review per spec §8.5.6 pre-rollout
/// checklist. Unit test <c>BggImportFieldFilterTests</c> guards drift.
/// </summary>
internal static class BggImportFieldFilter
{
    public static readonly IReadOnlySet<string> AllowedFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "name",
        "yearpublished",
        "minplayers", "maxplayers",
        "playingtime", "minplaytime", "maxplaytime",
        "minage",
        "link[type=boardgamedesigner]",
        "link[type=boardgamepublisher]",
        "link[type=boardgameartist]",
        "link[type=boardgamemechanic]",
        "link[type=boardgamecategory]",
        "link[type=boardgamefamily]",
    };

    /// <summary>
    /// Fields explicitly forbidden. Even if BGG response contains them, they
    /// are never mapped. Reason: copyright (description/comments — Feist),
    /// publisher copyright (image/thumbnail — handled by #1821/#1823), or
    /// DB sui generis EU (statistics — competing market).
    /// </summary>
    public static readonly IReadOnlySet<string> ForbiddenFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "description",
        "image", "thumbnail",
        "statistics",
        "comments",
        "videos",
    };
}
```

- [ ] **Step 4: Run GREEN**

Run: `cd D:/Repositories/meepleai-monorepo-main && dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~BggImportFieldFilterTests" --nologo --logger "console;verbosity=minimal" 2>&1 | tail -5`
Expected: PASS 3/3

- [ ] **Step 5: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-main
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Providers/BggImportFieldFilter.cs
git add apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/Providers/BggImportFieldFilterTests.cs
git commit -m "feat(catalog-seed): BggImportFieldFilter whitelist + compliance guard (M3.1)"
```

### Task M3.2: BggCatalogProvider implementation

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Providers/BggCatalogProvider.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/Providers/BggCatalogProviderTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using System.Net;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class BggCatalogProviderTests
{
    private const string CatanXml = """
    <?xml version="1.0" encoding="utf-8"?>
    <items>
      <item type="boardgame" id="13">
        <name type="primary" value="Catan"/>
        <yearpublished value="1995"/>
        <minplayers value="3"/>
        <maxplayers value="4"/>
        <playingtime value="60"/>
        <minage value="10"/>
        <link type="boardgamedesigner" id="11" value="Klaus Teuber"/>
        <link type="boardgamepublisher" id="93" value="Kosmos"/>
        <link type="boardgamemechanic" id="2008" value="Trading"/>
        <link type="boardgamemechanic" id="2018" value="Modular Board"/>
        <description>FORBIDDEN: should never be mapped</description>
        <image>FORBIDDEN_URL</image>
      </item>
    </items>
    """;

    private static HttpClient MakeClient(HttpStatusCode status, string body)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/xml"),
            });
        return new HttpClient(handler.Object) { BaseAddress = new Uri("https://boardgamegeek.com/") };
    }

    [Fact]
    public void Name_Equals_Bgg()
    {
        var p = new BggCatalogProvider(MakeClient(HttpStatusCode.OK, "<items/>"), NullLogger<BggCatalogProvider>.Instance);
        p.Name.Should().Be("bgg");
    }

    [Fact]
    public async Task FetchAsync_MapsAllowedFieldsOnly()
    {
        var p = new BggCatalogProvider(MakeClient(HttpStatusCode.OK, CatanXml), NullLogger<BggCatalogProvider>.Instance);
        var r = await p.FetchAsync(new CatalogProviderQuery(BggId: 13, null, null), default);

        r.Success.Should().BeTrue();
        r.Fields.Should().ContainKey("title");
        r.Fields["title"].Value.Should().Be("Catan");
        r.Fields["yearPublished"].Value.Should().Be(1995);
        r.Fields["mechanics"].Value.Should().BeOfType<List<string>>().Which.Should().BeEquivalentTo("Trading", "Modular Board");
        r.Fields["designers"].Value.Should().BeOfType<List<string>>().Which.Should().Contain("Klaus Teuber");
    }

    [Fact]
    public async Task FetchAsync_NeverMapsForbiddenFields_EvenIfPresent()
    {
        var p = new BggCatalogProvider(MakeClient(HttpStatusCode.OK, CatanXml), NullLogger<BggCatalogProvider>.Instance);
        var r = await p.FetchAsync(new CatalogProviderQuery(13, null, null), default);

        // Compliance guard — forbidden fields must NEVER appear in result.
        r.Fields.Should().NotContainKey("description");
        r.Fields.Should().NotContainKey("image");
        r.Fields.Should().NotContainKey("thumbnail");
        r.Fields.Should().NotContainKey("statistics");
        r.Fields.Should().NotContainKey("comments");
    }

    [Fact]
    public async Task FetchAsync_NoBggId_ReturnsError()
    {
        var p = new BggCatalogProvider(MakeClient(HttpStatusCode.OK, ""), NullLogger<BggCatalogProvider>.Instance);
        var r = await p.FetchAsync(new CatalogProviderQuery(BggId: null, WikidataQid: null, SearchTerm: null), default);
        r.Success.Should().BeFalse();
        r.ErrorMessage.Should().Contain("BggId");
    }

    [Fact]
    public async Task FetchAsync_Http500_ReturnsErrorMessage()
    {
        var p = new BggCatalogProvider(MakeClient(HttpStatusCode.InternalServerError, "fail"), NullLogger<BggCatalogProvider>.Instance);
        var r = await p.FetchAsync(new CatalogProviderQuery(13, null, null), default);
        r.Success.Should().BeFalse();
        r.ErrorMessage.Should().Contain("HTTP");
    }
}
```

- [ ] **Step 2: Run RED**

Run: `cd D:/Repositories/meepleai-monorepo-main && dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~BggCatalogProviderTests" --nologo 2>&1 | tail -5`
Expected: COMPILE FAIL

- [ ] **Step 3: Implement provider**

```csharp
using System.Globalization;
using System.Xml.Linq;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;

/// <summary>
/// Fallback catalog provider — BGG XML API2 (boardgamegeek.com/xmlapi2/thing).
/// Spec: 2026-06-04-admin-catalog-seed-design.md §7.2.
/// Strictly whitelisted (see <see cref="BggImportFieldFilter"/>). Inter-call
/// throttling expected from the orchestrator (1s/req) plus Polly retry exp.
/// </summary>
internal sealed class BggCatalogProvider : ICatalogProvider
{
    public string Name => "bgg";

    private readonly HttpClient _http;
    private readonly ILogger<BggCatalogProvider> _logger;

    public BggCatalogProvider(HttpClient http, ILogger<BggCatalogProvider> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CatalogProviderResult> FetchAsync(CatalogProviderQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.BggId is null)
        {
            return CatalogProviderResult.Empty("BggId is required for BGG provider");
        }

        var url = $"xmlapi2/thing?id={query.BggId}&stats=0";

        try
        {
            using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("BGG XML API HTTP {Status} on BggId={BggId}", (int)resp.StatusCode, query.BggId);
                return CatalogProviderResult.Empty($"HTTP {(int)resp.StatusCode}");
            }

            return ParseXml(body, query.BggId.Value);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BGG fetch failed for BggId={BggId}", query.BggId);
            return CatalogProviderResult.Empty(ex.Message);
        }
    }

    private static CatalogProviderResult ParseXml(string body, int bggId)
    {
        var sourceUrl = $"https://boardgamegeek.com/xmlapi2/thing?id={bggId}";
        var fetchedAt = DateTime.UtcNow;
        var doc = XDocument.Parse(body);
        var item = doc.Root?.Element("item");
        if (item is null)
        {
            return CatalogProviderResult.Empty("BGG: no item element found");
        }

        var fields = new Dictionary<string, FieldProvenance>(StringComparer.Ordinal);

        // name (primary)
        var name = item.Elements("name").FirstOrDefault(e => (string?)e.Attribute("type") == "primary")?.Attribute("value")?.Value;
        if (!string.IsNullOrWhiteSpace(name) && BggImportFieldFilter.AllowedFields.Contains("name"))
        {
            fields["title"] = new FieldProvenance("bgg", sourceUrl, "name[type=primary]", fetchedAt, name);
        }

        if (TryGetInt(item, "yearpublished", out var year) && BggImportFieldFilter.AllowedFields.Contains("yearpublished"))
        {
            fields["yearPublished"] = new FieldProvenance("bgg", sourceUrl, "yearpublished", fetchedAt, year);
        }
        if (TryGetInt(item, "minplayers", out var mn))
            fields["minPlayers"] = new FieldProvenance("bgg", sourceUrl, "minplayers", fetchedAt, mn);
        if (TryGetInt(item, "maxplayers", out var mx))
            fields["maxPlayers"] = new FieldProvenance("bgg", sourceUrl, "maxplayers", fetchedAt, mx);
        if (TryGetInt(item, "playingtime", out var pt))
            fields["playingTimeMinutes"] = new FieldProvenance("bgg", sourceUrl, "playingtime", fetchedAt, pt);
        if (TryGetInt(item, "minage", out var minAge))
            fields["minAge"] = new FieldProvenance("bgg", sourceUrl, "minage", fetchedAt, minAge);

        AddLinkList(fields, item, "boardgamedesigner", "designers", sourceUrl, fetchedAt);
        AddLinkList(fields, item, "boardgamepublisher", "publishers", sourceUrl, fetchedAt);
        AddLinkList(fields, item, "boardgameartist", "artists", sourceUrl, fetchedAt);
        AddLinkList(fields, item, "boardgamemechanic", "mechanics", sourceUrl, fetchedAt);
        AddLinkList(fields, item, "boardgamecategory", "categories", sourceUrl, fetchedAt);
        AddLinkList(fields, item, "boardgamefamily", "families", sourceUrl, fetchedAt);

        // bggId pass-through for cross-reference
        fields["bggId"] = new FieldProvenance("bgg", sourceUrl, "item/@id", fetchedAt, bggId);

        return new CatalogProviderResult(fields, body, ErrorMessage: null);
    }

    private static bool TryGetInt(XElement item, string elementName, out int value)
    {
        value = 0;
        var v = item.Element(elementName)?.Attribute("value")?.Value;
        return v is not null && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static void AddLinkList(
        Dictionary<string, FieldProvenance> fields,
        XElement item,
        string linkType,
        string targetField,
        string sourceUrl,
        DateTime fetchedAt)
    {
        var key = $"link[type={linkType}]";
        if (!BggImportFieldFilter.AllowedFields.Contains(key)) return;

        var values = item.Elements("link")
            .Where(e => (string?)e.Attribute("type") == linkType)
            .Select(e => (string?)e.Attribute("value"))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .ToList();

        if (values.Count > 0)
        {
            fields[targetField] = new FieldProvenance("bgg", sourceUrl, key, fetchedAt, values);
        }
    }
}
```

- [ ] **Step 4: Run GREEN**

Run: `cd D:/Repositories/meepleai-monorepo-main && dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~BggCatalogProviderTests" --nologo --logger "console;verbosity=minimal" 2>&1 | tail -5`
Expected: PASS 5/5

- [ ] **Step 5: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-main
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Providers/BggCatalogProvider.cs
git add apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/Providers/BggCatalogProviderTests.cs
git commit -m "feat(catalog-seed): BggCatalogProvider XML API2 + tests (M3.2)"
```

---

## M4: Aggregator + Commands + Queries

### Task M4.1: ICatalogSeedAggregator + impl

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/ICatalogSeedAggregator.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/CatalogSeedAggregator.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/CatalogSeedAggregatorTests.cs`

- [ ] **Step 1: Tests RED**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CatalogSeedAggregatorTests
{
    private static FieldProvenance FP(string provider, string field, object value)
        => new(provider, "http://example.test", field, DateTime.UtcNow, value);

    private static ICatalogProvider StubProvider(string name, IReadOnlyDictionary<string, FieldProvenance> fields, string? error = null)
    {
        var m = new Mock<ICatalogProvider>();
        m.SetupGet(p => p.Name).Returns(name);
        m.Setup(p => p.FetchAsync(It.IsAny<CatalogProviderQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogProviderResult(fields, "{}", error));
        return m.Object;
    }

    [Fact]
    public async Task FetchAsync_PrimaryOnly_WhenAllFieldsResolved()
    {
        var wd = StubProvider("wikidata", new Dictionary<string, FieldProvenance>
        {
            ["title"] = FP("wikidata", "labels.en", "Catan"),
            ["yearPublished"] = FP("wikidata", "P577", 1995),
        });
        var bgg = StubProvider("bgg", new Dictionary<string, FieldProvenance>());

        var agg = new CatalogSeedAggregator(wd, bgg, NullLogger<CatalogSeedAggregator>.Instance);
        var (provenance, providerUsed, rawCombined) = await agg.FetchAsync(new CatalogProviderQuery(13, null, null), default);

        provenance.GetValue<string>("title").Should().Be("Catan");
        provenance.GetProvider("title").Should().Be("wikidata");
        providerUsed.Should().Be("wikidata");
    }

    [Fact]
    public async Task FetchAsync_PrimaryAndFallback_WhenWikidataMissing()
    {
        var wd = StubProvider("wikidata", new Dictionary<string, FieldProvenance>
        {
            ["title"] = FP("wikidata", "labels.en", "Catan"),
        });
        var bgg = StubProvider("bgg", new Dictionary<string, FieldProvenance>
        {
            ["mechanics"] = FP("bgg", "boardgamemechanic", new List<string> { "Trading" }),
            ["title"] = FP("bgg", "name", "Settlers of Catan"),
        });

        var agg = new CatalogSeedAggregator(wd, bgg, NullLogger<CatalogSeedAggregator>.Instance);
        var (provenance, providerUsed, _) = await agg.FetchAsync(new CatalogProviderQuery(13, null, null), default);

        provenance.GetValue<string>("title").Should().Be("Catan");          // wd wins
        provenance.GetProvider("mechanics").Should().Be("bgg");             // bgg fills missing
        providerUsed.Should().Be("wikidata+bgg");
    }

    [Fact]
    public async Task FetchAsync_FallbackOnly_WhenWikidataFailed()
    {
        var wd = StubProvider("wikidata", new Dictionary<string, FieldProvenance>(), error: "not found");
        var bgg = StubProvider("bgg", new Dictionary<string, FieldProvenance>
        {
            ["title"] = FP("bgg", "name", "MyIndie"),
        });

        var agg = new CatalogSeedAggregator(wd, bgg, NullLogger<CatalogSeedAggregator>.Instance);
        var (provenance, providerUsed, _) = await agg.FetchAsync(new CatalogProviderQuery(50000, null, null), default);

        provenance.GetValue<string>("title").Should().Be("MyIndie");
        providerUsed.Should().Be("bgg");
    }

    [Fact]
    public async Task FetchAsync_BothFailed_ReturnsEmptyProvenance()
    {
        var wd = StubProvider("wikidata", new Dictionary<string, FieldProvenance>(), error: "fail-wd");
        var bgg = StubProvider("bgg", new Dictionary<string, FieldProvenance>(), error: "fail-bgg");

        var agg = new CatalogSeedAggregator(wd, bgg, NullLogger<CatalogSeedAggregator>.Instance);
        var (provenance, providerUsed, _) = await agg.FetchAsync(new CatalogProviderQuery(99999, null, null), default);

        provenance.Fields.Should().BeEmpty();
        providerUsed.Should().Be("none");
    }
}
```

- [ ] **Step 2: Interface**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

internal interface ICatalogSeedAggregator
{
    Task<CatalogSeedAggregationResult> FetchAsync(CatalogProviderQuery query, CancellationToken ct);
}

internal readonly record struct CatalogSeedAggregationResult(
    CatalogSeedProvenance Provenance,
    string ProviderUsed,
    string CombinedRawPayload);
```

- [ ] **Step 3: Impl**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

internal sealed class CatalogSeedAggregator : ICatalogSeedAggregator
{
    private readonly ICatalogProvider _wikidata;
    private readonly ICatalogProvider _bgg;
    private readonly ILogger<CatalogSeedAggregator> _logger;

    public CatalogSeedAggregator(
        ICatalogProvider wikidata,
        ICatalogProvider bgg,
        ILogger<CatalogSeedAggregator> logger)
    {
        if (wikidata.Name != "wikidata") throw new ArgumentException("Expected primary=wikidata", nameof(wikidata));
        if (bgg.Name != "bgg") throw new ArgumentException("Expected fallback=bgg", nameof(bgg));
        _wikidata = wikidata;
        _bgg = bgg;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CatalogSeedAggregationResult> FetchAsync(CatalogProviderQuery query, CancellationToken ct)
    {
        var primary = await _wikidata.FetchAsync(query, ct).ConfigureAwait(false);
        var fallback = await _bgg.FetchAsync(query, ct).ConfigureAwait(false);

        var merged = CatalogSeedProvenance.Merge(primary.Fields, fallback.Fields);

        var providerUsed = (primary.Success, fallback.Success) switch
        {
            (true, true) => "wikidata+bgg",
            (true, false) => "wikidata",
            (false, true) => "bgg",
            _ => "none",
        };

        var rawCombined = $"{{\"wikidata\":{primary.RawPayloadJson ?? "null"},\"bgg\":{fallback.RawPayloadJson ?? "null"}}}";

        _logger.LogInformation("CatalogSeedAggregator merged provider={Provider} fieldCount={Count}", providerUsed, merged.Fields.Count);

        return new CatalogSeedAggregationResult(merged, providerUsed, rawCombined);
    }
}
```

- [ ] **Step 4: Run GREEN**

Run: `cd D:/Repositories/meepleai-monorepo-main && dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CatalogSeedAggregatorTests" --nologo --logger "console;verbosity=minimal" 2>&1 | tail -5`
Expected: PASS 4/4

- [ ] **Step 5: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-main
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/ICatalogSeedAggregator.cs
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/CatalogSeedAggregator.cs
git add apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/CatalogSeedAggregatorTests.cs
git commit -m "feat(catalog-seed): CatalogSeedAggregator (Wikidata primary + BGG fallback) (M4.1)"
```

### Task M4.2: EnqueueCatalogSeedCommand + handler + validator

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/EnqueueCatalogSeedCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/EnqueueCatalogSeedCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Validators/EnqueueCatalogSeedCommandValidator.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/EnqueueCatalogSeedCommandHandlerTests.cs`

- [ ] **Step 1: Tests RED**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class EnqueueCatalogSeedCommandHandlerTests
{
    private readonly Mock<ICatalogSeedDraftRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private EnqueueCatalogSeedCommandHandler Handler() =>
        new(_repo.Object, _uow.Object, NullLogger<EnqueueCatalogSeedCommandHandler>.Instance);

    [Fact]
    public async Task Handle_NewBggId_InsertsPendingDraft()
    {
        _repo.Setup(r => r.ExistsAsync(13, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var userId = Guid.NewGuid();

        var result = await Handler().Handle(new EnqueueCatalogSeedCommand(BggId: 13, WikidataQid: null, SearchTermInput: null, CreatedByUserId: userId), default);

        result.Status.Should().Be("Pending");
        result.BggId.Should().Be(13);
        _repo.Verify(r => r.AddAsync(It.Is<CatalogSeedDraftEntity>(e => e.BggId == 13 && e.Status == "Pending" && e.CreatedByUserId == userId), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingBggId_ReturnsDuplicateInfoAndDoesNotInsert()
    {
        _repo.Setup(r => r.ExistsAsync(13, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await Handler().Handle(new EnqueueCatalogSeedCommand(13, null, null, Guid.NewGuid()), default);

        result.IsDuplicate.Should().BeTrue();
        _repo.Verify(r => r.AddAsync(It.IsAny<CatalogSeedDraftEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [ ] **Step 2: Run RED**

Run: `cd D:/Repositories/meepleai-monorepo-main && dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~EnqueueCatalogSeedCommandHandlerTests" --nologo 2>&1 | tail -5`
Expected: COMPILE FAIL

- [ ] **Step 3: Implement command + handler + validator**

```csharp
// EnqueueCatalogSeedCommand.cs
using Api.SharedKernel.Application.Interfaces;
namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

internal sealed record EnqueueCatalogSeedCommand(
    int? BggId,
    string? WikidataQid,
    string? SearchTermInput,
    Guid CreatedByUserId) : ICommand<EnqueueCatalogSeedResult>;

internal sealed record EnqueueCatalogSeedResult(
    Guid Id,
    int? BggId,
    string Status,
    bool IsDuplicate);
```

```csharp
// EnqueueCatalogSeedCommandHandler.cs
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

internal sealed class EnqueueCatalogSeedCommandHandler
    : ICommandHandler<EnqueueCatalogSeedCommand, EnqueueCatalogSeedResult>
{
    private readonly ICatalogSeedDraftRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<EnqueueCatalogSeedCommandHandler> _logger;

    public EnqueueCatalogSeedCommandHandler(
        ICatalogSeedDraftRepository repo,
        IUnitOfWork uow,
        ILogger<EnqueueCatalogSeedCommandHandler> logger)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EnqueueCatalogSeedResult> Handle(EnqueueCatalogSeedCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.BggId is int bggId && await _repo.ExistsAsync(bggId, ct).ConfigureAwait(false))
        {
            _logger.LogInformation("Duplicate enqueue rejected for BggId={BggId}", bggId);
            return new EnqueueCatalogSeedResult(Guid.Empty, bggId, "Duplicate", IsDuplicate: true);
        }

        var entity = new CatalogSeedDraftEntity
        {
            BggId = command.BggId,
            WikidataQid = command.WikidataQid,
            SearchTermInput = command.SearchTermInput,
            Status = "Pending",
            CreatedByUserId = command.CreatedByUserId,
        };

        await _repo.AddAsync(entity, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        return new EnqueueCatalogSeedResult(entity.Id, entity.BggId, entity.Status, IsDuplicate: false);
    }
}
```

```csharp
// EnqueueCatalogSeedCommandValidator.cs
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Validators;

internal sealed class EnqueueCatalogSeedCommandValidator : AbstractValidator<EnqueueCatalogSeedCommand>
{
    public EnqueueCatalogSeedCommandValidator()
    {
        RuleFor(x => x).Must(c =>
            c.BggId is not null || !string.IsNullOrWhiteSpace(c.WikidataQid) || !string.IsNullOrWhiteSpace(c.SearchTermInput))
            .WithMessage("At least one of BggId, WikidataQid or SearchTermInput is required.");

        RuleFor(x => x.BggId).GreaterThan(0).When(x => x.BggId.HasValue);
        RuleFor(x => x.WikidataQid).Matches(@"^Q\d+$").When(x => !string.IsNullOrWhiteSpace(x.WikidataQid));
        RuleFor(x => x.SearchTermInput).MaximumLength(255).When(x => x.SearchTermInput is not null);
        RuleFor(x => x.CreatedByUserId).NotEmpty();
    }
}
```

- [ ] **Step 4: Run GREEN**

Run: `cd D:/Repositories/meepleai-monorepo-main && dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~EnqueueCatalogSeedCommandHandlerTests" --nologo --logger "console;verbosity=minimal" 2>&1 | tail -5`
Expected: PASS 2/2

- [ ] **Step 5: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-main
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/EnqueueCatalogSeed*.cs
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Validators/EnqueueCatalogSeedCommandValidator.cs
git add apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/EnqueueCatalogSeedCommandHandlerTests.cs
git commit -m "feat(catalog-seed): EnqueueCatalogSeedCommand + handler + validator (M4.2)"
```

### Task M4.3: BulkEnqueueCatalogSeedsCommand

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/BulkEnqueueCatalogSeedsCommand.cs` + Handler + Validator
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/BulkEnqueueCatalogSeedsCommandHandlerTests.cs`

Apply same pattern as M4.2 with:
- Input: `IReadOnlyList<int> BggIds` (capped to 100 in validator).
- Iterate via `IMediator.Send(new EnqueueCatalogSeedCommand(...))` — reuse logic + duplicate handling.
- Return `BulkEnqueueResult(int Total, int Enqueued, int Duplicates, IReadOnlyList<Guid> NewDraftIds)`.
- Validator: `RuleFor(x => x.BggIds).NotEmpty().Must(l => l.Count <= 100).WithMessage("Max 100 BGG IDs per batch");`

- [ ] **Step 1-5**: same TDD pattern as M4.2 (skipped detail; reuse Mediator stub `_mediator.Setup(...).ReturnsAsync(new EnqueueCatalogSeedResult(...))` for each per-item call).
- [ ] **Commit**: `feat(catalog-seed): BulkEnqueueCatalogSeedsCommand (max 100/batch) (M4.3)`

### Task M4.4: ApproveCatalogSeedCommand + handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/ApproveCatalogSeedCommand.cs` + Handler + Validator
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/ApproveCatalogSeedCommandHandlerTests.cs`

The handler must:
1. Load draft by Id, verify status == "Fetched"
2. Parse `ProvenanceJson` → `CatalogSeedProvenance`
3. Construct `SharedGameEntity` via factory using `provenance.GetValue<T>(field)` for each
4. Upsert via `ISharedGameRepository`
5. Set draft `Status = "Approved"`, `ApprovedAt`, `ApprovedByUserId`, `ResultingSharedGameId`
6. Save via `IUnitOfWork`
7. Publish `CatalogSeedApprovedEvent` via `IMediator` **after** save success (per #1873 H1 review pattern — same as `DeletePdfCommandHandler` post-save publish)

Test scenarios:
- Happy path: Fetched draft → Approved + SharedGame inserted
- Status not Fetched → `ConflictException` (CLAUDE.md issue #2568)
- Duplicate BggId → upsert (no exception)
- Save failure → exception propagates, no event published

Commit: `feat(catalog-seed): ApproveCatalogSeedCommand + post-save event publish (M4.4)`

### Task M4.5: RejectCatalogSeedCommand + handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/RejectCatalogSeedCommand.cs` + Handler + Validator
- Test: parallel pattern

Handler must:
1. Load draft, set `IsDeleted = true`, `Status = "Rejected"`, `DeletedAt`
2. Save
3. Publish `CatalogSeedRejectedEvent` via `IMediator` post-save

Commit: `feat(catalog-seed): RejectCatalogSeedCommand (M4.5)`

### Task M4.6: ListCatalogSeedsQuery + GetCatalogSeedByIdQuery

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/ListCatalogSeedsQuery.cs` + Handler
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetCatalogSeedByIdQuery.cs` + Handler
- Tests: paginated list + filter + DTO mapping

Return `PagedResult<CatalogSeedDraftDto>` (existing pattern in codebase).

Commit: `feat(catalog-seed): list + getById queries (M4.6)`

---

## M5: Quartz CatalogSeedFetchJob

### Task M5.1: CatalogSeedFetchJob implementation

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Jobs/CatalogSeedFetchJob.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Jobs/CatalogSeedFetchJobTests.cs`

Pattern identical to `BackfillPdfCoversJob` (#1873):
- `[DisallowConcurrentExecution]`
- `BatchSize = 10`, `DelayBetweenItemsMs = 1000`
- ServiceProvider scope creation per execution
- Picks `Pending` drafts ordered by `CreatedAt asc`
- Per draft: call `ICatalogSeedAggregator.FetchAsync` → set Status `Fetched` or `FetchFailed` → save → publish event via IMediator post-save
- Inter-item delay 1s for BGG rate limit compliance

Tests (10+ scenarios):
- No Pending → no-op
- Single Pending → Fetched + event published
- Aggregator throws → FetchFailed + error message persisted
- Cancellation token → OperationCanceledException
- BatchSize limit (12 Pending → only 10 processed)
- Inter-item delay applied (timing)
- Per-item failure isolation (1 fails, others continue)

Commit: `feat(catalog-seed): CatalogSeedFetchJob Quartz (M5.1)`

### Task M5.2: Quartz registration in DI

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/DependencyInjection/SharedGameCatalogServiceExtensions.cs`

Pattern from `DocumentProcessingServiceExtensions.RegisterBackfillPdfCoversJob`. Trigger every 1 min.

Commit: `feat(catalog-seed): register CatalogSeedFetchJob Quartz trigger (M5.2)`

---

## M6: SSE Stream Service + Admin Endpoints

### Task M6.1: ICatalogSeedStreamService

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/ICatalogSeedStreamService.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/CatalogSeedStreamService.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/CatalogSeedStreamServiceTests.cs`

Mirror `PdfProgressStreamService`:
- Singleton, `ConcurrentDictionary<Guid subscriberId, Channel<CatalogSeedStreamEvent>>`
- `SubscribeAsync(CancellationToken)` returns `IAsyncEnumerable<CatalogSeedStreamEvent>` with replay of last 200 events
- `PublishAsync(CatalogSeedStreamEvent)` writes to all channels
- Auto-cleanup on subscriber cancellation

Event types: `BatchStarted`, `SeedEntryFetched`, `SeedEntryFetchFailed`, `BatchCompleted`.

Wire into `CatalogSeedFetchJob` (modify M5.1 to call `_streamService.PublishAsync(...)` between steps).

Commit: `feat(catalog-seed): CatalogSeedStreamService singleton SSE (M6.1)`

### Task M6.2: Admin endpoints minimal API

**Files:**
- Create: `apps/api/src/Api/Routing/Admin/AdminCatalogSeedRouting.cs`
- Modify: `apps/api/src/Api/Program.cs` (register routing extension method)

8 endpoints (per spec §5.4):
- POST `/api/v1/admin/catalog/seeds` → `EnqueueCatalogSeedCommand`
- POST `/api/v1/admin/catalog/seeds/bulk` → `BulkEnqueueCatalogSeedsCommand`
- GET `/api/v1/admin/catalog/seeds` → `ListCatalogSeedsQuery`
- GET `/api/v1/admin/catalog/seeds/{id}` → `GetCatalogSeedByIdQuery`
- POST `/api/v1/admin/catalog/seeds/{id}/approve` → `ApproveCatalogSeedCommand`
- POST `/api/v1/admin/catalog/seeds/{id}/reject` → `RejectCatalogSeedCommand`
- GET `/api/v1/admin/catalog/seeds/stream` → SSE (`ICatalogSeedStreamService.SubscribeAsync`)
- POST `/api/v1/admin/catalog/seeds/wikidata-search` → proxy SPARQL (calls Wikidata directly with admin user-agent)

All `[Authorize(Roles="Admin")]`. **CRITICAL**: per CLAUDE.md CQRS rule, endpoints use ONLY `IMediator.Send()` — zero direct service injection. SSE endpoint is the only exception (resolves `ICatalogSeedStreamService` from `HttpContext.RequestServices`).

Integration tests using `WebApplicationFactory` for each endpoint (auth check + happy path).

Commit: `feat(catalog-seed): admin endpoints + minimal API routing (M6.2)`

---

## M7: BggTosWatcherJob + Feature Flag

### Task M7.1: BggTosWatcherJob

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Jobs/BggTosWatcherJob.cs`
- Create: `apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/BggTosHashEntity.cs` (1-row table with current hash + lastChecked)
- Migration: `dotnet ef migrations add AddBggTosHash`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Jobs/BggTosWatcherJobTests.cs`

Job fetches `https://boardgamegeek.com/terms` HTML monthly, computes SHA-256 of body, compares to last known. On change → emits `BggTosChangedEvent` + logs warning.

Commit: `feat(catalog-seed): BggTosWatcherJob monthly ToS hash check (M7.1)`

### Task M7.2: AdminCatalogSeedEnabled feature flag

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SystemConfiguration/Domain/FeatureFlags.cs` (add constant)
- Modify: `AdminCatalogSeedRouting.cs` (add `[FeatureGate("AdminCatalogSeedEnabled")]` or runtime check in handlers)
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Jobs/CatalogSeedFetchJob.cs` (early return if disabled)
- Modify: `apps/web/src/app/admin/(dashboard)/config/...` (display toggle in admin config page)

Default: `false`. Toggle via existing `/admin/config` UI (Registration Mode pattern).

Commit: `feat(catalog-seed): AdminCatalogSeedEnabled runtime feature flag (M7.2)`

---

## M8: Frontend Admin UI + E2E

### Task M8.1: SDK client + hooks

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/catalog/seed-queue/lib/catalog-seed-api.ts`
- Create: `apps/web/src/app/admin/(dashboard)/catalog/seed-queue/hooks/use-catalog-seeds.ts` (React Query)
- Create: `apps/web/src/app/admin/(dashboard)/catalog/seed-queue/hooks/use-catalog-seed-stream.ts` (EventSource)
- Tests: Vitest unit per client + hooks (mock fetch + `eventsource-mock`)

Mirror existing pattern from `apps/web/src/app/admin/(dashboard)/catalog-ingestion/lib/catalog-ingestion-api.ts`.

Commit: `feat(catalog-seed): FE SDK + React Query hooks + EventSource (M8.1)`

### Task M8.2: Page route + layout

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/catalog/seed-queue/page.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/catalog/seed-queue/__tests__/page.test.tsx`

Two-column layout (Input | Queue) per spec §5.1. Use `DetailPageLayout` primitive (`apps/web/src/components/ui/detail-layout/`).

Commit: `feat(catalog-seed): seed-queue page route + layout (M8.2)`

### Task M8.3: Reused #1835 components adaption

**Files:**
- Create wrappers/extensions of existing components (don't duplicate; import + adapt props):
  - `SeedQueueStatusHero.tsx` (wraps `SyncStatusHero` from catalog-ingestion)
  - `SeedRunTimeline.tsx` (wraps `SyncRunTimeline`)
  - Reuse `LogStream`, `QueuePendingPanel`, `FailedItemsPanel`, `CsvImportModal` as-is via direct import.

Run `pnpm test` to verify existing test suites still pass after import additions.

Commit: `feat(catalog-seed): reuse #1835 components for seed-queue (M8.3)`

### Task M8.4: New FE components (Wikidata search + Preview + Validation)

**Files:**
- Create: `WikidataSearchForm.tsx` + test
- Create: `SeedPreviewPanel.tsx` + test
- Create: `BggIdValidationBadge.tsx` + test
- Create: `AddBggIdForm.tsx` (renamed from `AssignBggIdForm` adapted) + test

Each with Vitest unit test (renders, calls handlers, validates input).

Commit: `feat(catalog-seed): new FE components Wikidata search + Preview + validation (M8.4)`

### Task M8.5: E2E Playwright

**Files:**
- Create: `apps/web/e2e/admin/catalog-seed.spec.ts`

Scenarios:
1. Admin login → navigate to /admin/catalog/seed-queue → page renders
2. Bulk paste 3 BGG IDs (real test IDs from playable fixture: 13, 30549, 167791) → 3 Pending drafts appear in queue
3. Wait 90s for Quartz fetch (or mock SSE) → 3 Fetched
4. Click approve on first → status changes to Approved + SharedGame created
5. Feature flag toggle disable → endpoints return 403

Commit: `test(catalog-seed): Playwright E2E admin/catalog/seed-queue (M8.5)`

### Task M8.6: ADR document + Pre-rollout checklist

**Files:**
- Create: `docs/for-claude/architecture/adr/adr-NNN-catalog-seed-legal-posture.md`
  - Content references spec §8.5 (mitigations + legal framework)
- Modify: `docs/for-developers/operations/operations-manual.md` (add catalog-seed admin runbook section)
- Verify all 14 DoD items from spec §12 complete

Commit: `docs(catalog-seed): ADR + operations runbook + DoD checklist (M8.6)`

### Task M8.7: PR + holistic review + merge + close

Final steps:
1. Push branch
2. Create PR to `main-dev`
3. Request holistic Opus review (memory: subagent-driven blind spot guard)
4. Apply review findings
5. Squash-merge to `main-dev`
6. Close related issues (issue created at task start)
7. Update CLAUDE.md memory if new patterns surfaced

---

## Self-Review

**1. Spec coverage check** (per design doc sections):
- §1 Problem statement → addressed in plan goal
- §2 Goals/non-goals → tasks scoped to whitelisted fields only (M3.1 guard)
- §3 Architecture → M1.1, M2, M3, M4 (entity, providers, aggregator)
- §4 Lifecycle → M1.1 enum, M5 fetch job, M4.4 approve, M4.5 reject
- §5 Admin UI → M8.2-M8.5
- §6 SSE stream → M6.1
- §7 Providers → M2.2 (Wikidata), M3.2 (BGG)
- §8 Safeguards → M3.1 (whitelist), M7.2 (feature flag), M7.1 (ToS watcher), M6.2 (auth)
- §8.5 Legal framework → M8.6 ADR
- §9 Bounded contexts → all M1-M7 follow DDD structure
- §10 Effort → matches plan totals
- §11 Open questions → deferred (multi-lang, Excel import, stale refresh, bulk approve) — not in plan, future
- §12 DoD → M8.6 verifies all 14 items

✅ All spec sections covered.

**2. Placeholder scan**:
- M4.3, M4.4, M4.5, M4.6 use "same pattern as M4.2" abbreviation. **Risk**: engineer reading M4.4 out of order needs code. **Decision**: acceptable for adjacent milestones with identical structure; M4.2 is reference impl with full code. Each later task references specific scenarios + commit message, not "TBD".
- M5.1, M5.2, M6.1, M7.1, M7.2, M8.3, M8.4, M8.5, M8.6 use prose summary instead of full code. **Reason**: each is ~5-7h with established pattern from previous milestones — full code would inflate plan to 3000+ lines unmaintainable. **Mitigation**: explicit references to pattern source (e.g. "Pattern identical to `BackfillPdfCoversJob` #1873") + test scenarios listed.
- No "TBD", "TODO", "implement later" markers.

**3. Type consistency check**:
- `CatalogSeedStatus` enum values (Pending/Fetched/FetchFailed/Approved/Rejected) consistent across M1.1, M4.2-M4.5, M5.1.
- `FieldProvenance` record signature consistent in M1.1, M2.2, M3.2.
- `CatalogSeedAggregationResult` (named tuple positional in test) matches struct definition in M4.1.
- `EnqueueCatalogSeedResult` properties (`Id`, `BggId`, `Status`, `IsDuplicate`) consistent in M4.2 test + handler.
- `ICatalogProvider.Name` returns "wikidata" | "bgg" — consistent in M2.1, M2.2, M3.2, M4.1 (aggregator validates).
- `CatalogProviderResult.Success` derived property — consistent in M2.1, M2.2, M3.2, M4.1.

✅ All types consistent.

**4. Scope check**:
- Single feature (admin catalog seed), 8 milestones, ~43h total.
- Each milestone independently committable (no half-finished states).
- M1 unblocks M2-M8; M2/M3 unblock M4; M4 unblocks M5/M6; M6/M7 unblock M8.
- Can stop after any milestone for iteration.

✅ Scope appropriate for single plan.

---

## Definition of Done (plan-level)

All from spec §12 + delta:
- [ ] DB migration applicata + reverted-clean
- [ ] All providers + aggregator tests pass (M2.2 + M3.2 + M4.1)
- [ ] `BggImportFieldFilterTests` passa = compliance guard valido
- [ ] All command/query handler tests pass (M4.2-M4.6)
- [ ] `CatalogSeedFetchJob` tests pass + Quartz registration verified
- [ ] SSE stream subscribe/publish/buffer tests pass
- [ ] Admin endpoints with `[Authorize(Roles="Admin")]` + integration tests pass
- [ ] `BggTosWatcherJob` active + alert configured
- [ ] FE: 15+ component tests (Vitest) pass
- [ ] FE: 1+ E2E Playwright test passes
- [ ] Feature flag `AdminCatalogSeedEnabled` toggleable via `/admin/config`
- [ ] User-Agent BGG includes `mailto:abuse@meepleai.app` + email mailbox monitored
- [ ] Terms of Service aggiornato (clausola seed)
- [ ] Audit log export endpoint works (CSV via `domain_event_logs`)
- [ ] ADR `adr-NNN-catalog-seed-legal-posture.md` committed
- [ ] Operations runbook section added to `operations-manual.md`
- [ ] Pre-rollout legal checklist (§8.5.6) completed before enabling flag in staging/prod
