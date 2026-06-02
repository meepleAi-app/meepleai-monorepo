# SP5 Admin KB #1673 — Re-index with version selector Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aggiungere supporto al re-index versionato per documenti KB lato admin — registry di versioni indicizzatore code-resident, persistenza per-doc della versione applicata, audit, UI con dropdown versione + 409 su reindex in-flight.

**Architecture:** Approccio foundation-first basato sul design doc `2026-06-01-sp5-admin-kb-fu4-spinouts-design.md` §3.2 (D-B: code-resident strategy registry ≤3 versioni). Aggiungiamo la colonna `pdf_documents.indexer_version`, estendiamo `ReindexDocumentCommand` con parametro opzionale `IndexerVersion`, esponiamo un nuovo endpoint `GET /api/v1/admin/indexer/versions` per popolare il dropdown, e renderizziamo la versione nel hero metadata del detail panel (chiude anche #1676 sub-task F3). Il registry oggi contiene solo `Current` (selectable) + `Legacy v0` (marker per row backfilled, non selectable) — strategie diverse verranno aggiunte quando una vera divergenza pipeline viene shippata.

**Tech Stack:**
- BE: .NET 9 / ASP.NET Minimal APIs + MediatR (CQRS), EF Core 9 (PostgreSQL + pgvector), FluentValidation, xUnit + Testcontainers, FluentAssertions
- FE: Next.js 16 (App Router) + React 19, TypeScript, TanStack Query v5, Zod, Vitest + React Testing Library, Tailwind 4

**Issue**: [#1673](https://github.com/meepleAi-app/meepleai-monorepo/issues/1673) (P3) · parent design doc `docs/superpowers/specs/2026-06-01-sp5-admin-kb-fu4-spinouts-design.md` §3.2

**Branch**: `feature/issue-1673-reindex-version-selector` (parent: `main-dev`)

---

## File Structure (decomposition)

### BE — Created
| Path | Responsibility |
|------|---------------|
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/ValueObjects/IndexerVersion.cs` | Value object record (Version, DisplayName, IsSelectable) |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/ValueObjects/IndexerVersionRegistry.cs` | Static registry (Current, Legacy, All, TryGet) |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetIndexerVersionRegistry/GetIndexerVersionRegistryQuery.cs` | MediatR query (parameterless) |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetIndexerVersionRegistry/GetIndexerVersionRegistryHandler.cs` | Returns selectable versions from registry |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetIndexerVersionRegistry/IndexerVersionDto.cs` | DTO (Version, DisplayName, IsCurrent) |
| `apps/api/src/Api/Infrastructure/Migrations/<TIMESTAMP>_AddIndexerVersionToPdfDocuments.cs` | EF migration: nullable column + backfill `'v0'` |
| `apps/api/src/Api/Routing/AdminIndexerEndpoints.cs` | Endpoint `GET /api/v1/admin/indexer/versions` |
| `apps/api/tests/Api.Tests/Unit/DocumentProcessing/IndexerVersionRegistryTests.cs` | Unit tests value object + registry |
| `apps/api/tests/Api.Tests/Unit/DocumentProcessing/ReindexDocumentCommandValidatorTests.cs` | Unit tests validator (PdfId + IndexerVersion) |
| `apps/api/tests/Api.Tests/Unit/DocumentProcessing/ReindexDocumentCommandHandlerTests.cs` | Unit tests handler (resolution + conflict + persistence) |
| `apps/api/tests/Api.Tests/Unit/DocumentProcessing/GetIndexerVersionRegistryHandlerTests.cs` | Unit tests query handler |
| `apps/api/tests/Api.Tests/Integration/DocumentProcessing/ReindexDocumentVersionIntegrationTests.cs` | Integration: end-to-end + audit + 409 in-flight |
| `apps/web/src/components/admin/knowledge-base/explorer/actions/KbReindexDropdown.tsx` | Dropdown button (version picker + confirm) |
| `apps/web/src/components/admin/knowledge-base/explorer/actions/__tests__/KbReindexDropdown.test.tsx` | Vitest component test |
| `apps/web/src/hooks/queries/useIndexerVersions.ts` | TanStack Query hook (registry fetch) |
| `apps/web/src/lib/api/schemas/indexer-versions.schemas.ts` | Zod schemas (registry response) |

### BE — Modified
| Path | Change |
|------|--------|
| `apps/api/src/Api/Infrastructure/Entities/DocumentProcessing/PdfDocumentEntity.cs` | + `string? IndexerVersion { get; set; }` |
| `apps/api/src/Api/Infrastructure/EntityConfigurations/DocumentProcessing/PdfDocumentEntityConfiguration.cs` | + Property mapping `indexer_version varchar(32) NULL` |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/ReindexDocumentCommand.cs` | + `string? IndexerVersion` param + `[AuditableAction]` attribute |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/ReindexDocumentCommandHandler.cs` | Version resolution + conflict guard + persistence |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Validators/ReindexDocumentCommandValidator.cs` | + Validation: IndexerVersion null OR in registry as selectable |
| `apps/api/src/Api/Routing/AdminPdfManagementEndpoints.cs` | Reindex endpoint accepts optional body `{ "indexerVersion": "v1.0" }` |
| `apps/api/src/Api/Program.cs` | Wire `MapAdminIndexerEndpoints` |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbDocumentById/KbDocumentDto.cs` | + `string? IndexerVersion` field |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbDocumentById/GetKbDocumentByIdHandler.cs` | Populate IndexerVersion from entity |

### FE — Modified
| Path | Change |
|------|--------|
| `apps/web/src/lib/api/clients/pdfClient.ts` | `reindexDocument(pdfId, body?)` + `getIndexerVersions()` |
| `apps/web/src/lib/api/schemas/kb-chunks.schemas.ts` | Add `indexerVersion: z.string().nullable().optional()` to `KbDocDetailSchema` |
| `apps/web/src/hooks/queries/useKbDocActions.ts` | `useReindexDoc(docId)` accepts `{ indexerVersion?: string }` payload |
| `apps/web/src/components/admin/knowledge-base/explorer/actions/KbDocActions.tsx` | Swap button → `<KbReindexDropdown>` |
| `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx` | Render `indexerVersion` in hero metadata block |

---

## Tasks

### Task 1: BE Domain — `IndexerVersion` value object + `IndexerVersionRegistry`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/ValueObjects/IndexerVersion.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/ValueObjects/IndexerVersionRegistry.cs`
- Test: `apps/api/tests/Api.Tests/Unit/DocumentProcessing/IndexerVersionRegistryTests.cs`

- [ ] **Step 1: Write failing tests for `IndexerVersion` + `IndexerVersionRegistry`**

```csharp
// apps/api/tests/Api.Tests/Unit/DocumentProcessing/IndexerVersionRegistryTests.cs
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "1673")]
public sealed class IndexerVersionRegistryTests
{
    [Fact]
    public void Current_ReturnsLatestSelectableVersion()
    {
        IndexerVersionRegistry.Current.Version.Should().Be("v1.0");
        IndexerVersionRegistry.Current.IsSelectable.Should().BeTrue();
    }

    [Fact]
    public void Legacy_ReturnsV0NonSelectable()
    {
        IndexerVersionRegistry.Legacy.Version.Should().Be("v0");
        IndexerVersionRegistry.Legacy.IsSelectable.Should().BeFalse();
    }

    [Fact]
    public void All_ContainsLegacyAndCurrent()
    {
        var versions = IndexerVersionRegistry.All;
        versions.Should().HaveCountGreaterThanOrEqualTo(2);
        versions.Should().Contain(v => v.Version == "v0");
        versions.Should().Contain(v => v.Version == "v1.0");
    }

    [Theory]
    [InlineData("v0")]
    [InlineData("v1.0")]
    public void TryGet_KnownVersion_ReturnsTrue(string input)
    {
        IndexerVersionRegistry.TryGet(input, out var version).Should().BeTrue();
        version!.Version.Should().Be(input);
    }

    [Theory]
    [InlineData("v99")]
    [InlineData("")]
    [InlineData(null)]
    public void TryGet_UnknownVersion_ReturnsFalse(string? input)
    {
        IndexerVersionRegistry.TryGet(input, out var version).Should().BeFalse();
        version.Should().BeNull();
    }

    [Fact]
    public void IsSelectable_LegacyV0_ReturnsFalse()
    {
        IndexerVersionRegistry.IsSelectable("v0").Should().BeFalse();
    }

    [Fact]
    public void IsSelectable_Current_ReturnsTrue()
    {
        IndexerVersionRegistry.IsSelectable("v1.0").Should().BeTrue();
    }

    [Fact]
    public void IsSelectable_Unknown_ReturnsFalse()
    {
        IndexerVersionRegistry.IsSelectable("v99").Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~IndexerVersionRegistryTests" --no-build 2>&1 | tail -20
```

Expected: build failure with "The type or namespace name 'IndexerVersion' could not be found".

- [ ] **Step 3: Implement `IndexerVersion` value object**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/ValueObjects/IndexerVersion.cs
namespace Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

/// <summary>
/// Identifies a pipeline indexer version. Code-resident registry per design doc D-B
/// (2026-06-01-sp5-admin-kb-fu4-spinouts-design.md §3.2): ≤3 versioni concorrenti, nessuna
/// container infrastructure. Issue #1673.
/// </summary>
/// <remarks>
/// <para>
/// <b>IsSelectable</b>: <c>false</c> per il marker storico <c>v0</c> (pre-versioning,
/// usato dal backfill della migration). <c>true</c> per ogni versione effettivamente
/// invocabile da `/admin/pdfs/{id}/reindex`.
/// </para>
/// </remarks>
internal sealed record IndexerVersion(string Version, string DisplayName, bool IsSelectable);
```

- [ ] **Step 4: Implement `IndexerVersionRegistry`**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/ValueObjects/IndexerVersionRegistry.cs
using System.Diagnostics.CodeAnalysis;

namespace Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

/// <summary>
/// Code-resident registry of pipeline indexer versions per design doc D-B.
/// Issue #1673.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aggiungere una nuova versione</b>: introdurre la `static readonly IndexerVersion Vx_y`,
/// includerla in <see cref="All"/>, aggiornare <see cref="Current"/>. NIENTE breaking change
/// per <see cref="Legacy"/> (v0 resta marker di backfill).
/// </para>
/// <para>
/// <b>Deprecation policy</b> (OQ-2 risolta dal spec-panel review PR #1790): le versioni
/// storiche restano nel registry per ≥18 mesi post-supersession da una versione più recente.
/// Oltre quel termine il code-resident slot può essere riciclato.
/// </para>
/// </remarks>
internal static class IndexerVersionRegistry
{
    /// <summary>
    /// Marker per documenti pre-versioning ingeriti prima dell'introduzione del versioning.
    /// Non selectable: serve solo a non lasciare la colonna nullable per le righe storiche.
    /// </summary>
    public static readonly IndexerVersion Legacy =
        new("v0", "v0 (legacy pre-versioning)", IsSelectable: false);

    /// <summary>
    /// Versione corrente della pipeline. Equivale al comportamento di default quando
    /// `reindexDocument` viene chiamato senza `IndexerVersion` esplicito.
    /// </summary>
    public static readonly IndexerVersion Current =
        new("v1.0", "v1.0 — current pipeline", IsSelectable: true);

    public static IReadOnlyList<IndexerVersion> All { get; } = [Legacy, Current];

    /// <summary>
    /// Restituisce la lista delle versioni effettivamente invocabili da `/reindex`.
    /// </summary>
    public static IReadOnlyList<IndexerVersion> Selectable { get; } =
        All.Where(v => v.IsSelectable).ToArray();

    public static bool TryGet(string? version, [NotNullWhen(true)] out IndexerVersion? result)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            result = null;
            return false;
        }

        result = All.FirstOrDefault(v => string.Equals(v.Version, version, StringComparison.Ordinal));
        return result is not null;
    }

    public static bool IsSelectable(string? version) =>
        TryGet(version, out var v) && v.IsSelectable;
}
```

- [ ] **Step 5: Run tests to verify they pass**

```
cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~IndexerVersionRegistryTests" --no-build 2>&1 | tail -10
```

Expected: 8 passed.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/ValueObjects/IndexerVersion.cs apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/ValueObjects/IndexerVersionRegistry.cs apps/api/tests/Api.Tests/Unit/DocumentProcessing/IndexerVersionRegistryTests.cs
git commit -m "feat(api/document-processing): #1673 add IndexerVersion value object + registry"
```

---

### Task 2: BE Migration — `indexer_version` column + backfill `'v0'`

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Entities/DocumentProcessing/PdfDocumentEntity.cs`
- Modify: `apps/api/src/Api/Infrastructure/EntityConfigurations/DocumentProcessing/PdfDocumentEntityConfiguration.cs`
- Create: `apps/api/src/Api/Infrastructure/Migrations/<TIMESTAMP>_AddIndexerVersionToPdfDocuments.cs` (generated)

- [ ] **Step 1: Add property to `PdfDocumentEntity`**

In `apps/api/src/Api/Infrastructure/Entities/DocumentProcessing/PdfDocumentEntity.cs`, dopo `public string? VersionLabel { get; set; }` (riga 94), aggiungere:

```csharp
    // Issue #1673: Pipeline indexer version applied at last reindex.
    // Nullable for backwards compat — backfilled to 'v0' on migration.
    public string? IndexerVersion { get; set; }
```

- [ ] **Step 2: Add property mapping to `PdfDocumentEntityConfiguration`**

In `apps/api/src/Api/Infrastructure/EntityConfigurations/DocumentProcessing/PdfDocumentEntityConfiguration.cs`, dopo il blocco `VersionLabel` (riga 163-166), aggiungere:

```csharp
        // Issue #1673: Pipeline indexer version (nullable; backfilled to 'v0').
        builder.Property(e => e.IndexerVersion)
            .HasMaxLength(32)
            .HasColumnName("indexer_version")
            .IsRequired(false);

        builder.HasIndex(e => e.IndexerVersion)
            .HasDatabaseName("ix_pdf_documents_indexer_version");
```

- [ ] **Step 3: Generate migration**

```
cd apps/api/src/Api
dotnet ef migrations add AddIndexerVersionToPdfDocuments --output-dir Infrastructure/Migrations
```

Expected: 2 new files in `Infrastructure/Migrations/<TIMESTAMP>_AddIndexerVersionToPdfDocuments.*`.

- [ ] **Step 4: Edit migration to backfill `'v0'` for existing rows**

Aprire `<TIMESTAMP>_AddIndexerVersionToPdfDocuments.cs`. Nel metodo `Up`, dopo `migrationBuilder.AddColumn<string>(...)` aggiungere:

```csharp
            // Backfill legacy marker for rows ingested before versioning support.
            migrationBuilder.Sql(
                "UPDATE pdf_documents SET indexer_version = 'v0' WHERE indexer_version IS NULL;");
```

Il metodo `Down` può restare auto-generato (rimuove la colonna).

- [ ] **Step 5: Verify build**

```
dotnet build apps/api/src/Api/Api.csproj --no-restore 2>&1 | tail -10
```

Expected: Build succeeded. 0 Warning(s). 0 Error(s).

- [ ] **Step 6: Run existing PdfDocument tests to confirm no regression**

```
cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "BoundedContext=DocumentProcessing" --no-build 2>&1 | tail -15
```

Expected: tutti pass (la colonna nullable non rompe niente).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/DocumentProcessing/PdfDocumentEntity.cs apps/api/src/Api/Infrastructure/EntityConfigurations/DocumentProcessing/PdfDocumentEntityConfiguration.cs apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(api/document-processing): #1673 add indexer_version column + v0 backfill"
```

---

### Task 3: BE Validator — extend `ReindexDocumentCommandValidator`

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Validators/ReindexDocumentCommandValidator.cs`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/ReindexDocumentCommand.cs` (extend signature first)
- Create: `apps/api/tests/Api.Tests/Unit/DocumentProcessing/ReindexDocumentCommandValidatorTests.cs`

- [ ] **Step 1: Extend `ReindexDocumentCommand` signature**

Sostituire l'intero contenuto di `ReindexDocumentCommand.cs` con:

```csharp
using Api.BoundedContexts.Administration.Application.Attributes;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to reindex a PDF document (delete vectors/chunks, reset to Pending, re-trigger pipeline).
/// PDF Storage Management Hub: Phase 5. Issue #1673 estende con selettore versione indexer.
/// </summary>
/// <param name="PdfId">ID del documento PDF da re-indicizzare.</param>
/// <param name="IndexerVersion">
/// Versione pipeline da applicare. <c>null</c> = usa la versione storica del documento se
/// presente, altrimenti <c>IndexerVersionRegistry.Current.Version</c>.
/// </param>
[AuditableAction("DocumentReindex", "Document", Level = 2)]
internal sealed record ReindexDocumentCommand(Guid PdfId, string? IndexerVersion = null) : ICommand;
```

- [ ] **Step 2: Write failing tests for validator**

```csharp
// apps/api/tests/Api.Tests/Unit/DocumentProcessing/ReindexDocumentCommandValidatorTests.cs
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Validators;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "1673")]
public sealed class ReindexDocumentCommandValidatorTests
{
    private readonly ReindexDocumentCommandValidator _validator = new();

    [Fact]
    public void PdfId_Empty_FailsWithNotEmpty()
    {
        var result = _validator.TestValidate(new ReindexDocumentCommand(Guid.Empty));
        result.ShouldHaveValidationErrorFor(c => c.PdfId);
    }

    [Fact]
    public void IndexerVersion_Null_Passes()
    {
        var result = _validator.TestValidate(new ReindexDocumentCommand(Guid.NewGuid()));
        result.ShouldNotHaveValidationErrorFor(c => c.IndexerVersion);
    }

    [Fact]
    public void IndexerVersion_Current_Passes()
    {
        var result = _validator.TestValidate(new ReindexDocumentCommand(Guid.NewGuid(), "v1.0"));
        result.ShouldNotHaveValidationErrorFor(c => c.IndexerVersion);
    }

    [Fact]
    public void IndexerVersion_LegacyV0_FailsAsNotSelectable()
    {
        var result = _validator.TestValidate(new ReindexDocumentCommand(Guid.NewGuid(), "v0"));
        result.ShouldHaveValidationErrorFor(c => c.IndexerVersion)
            .WithErrorMessage("Indexer version 'v0' is not selectable (legacy marker).");
    }

    [Fact]
    public void IndexerVersion_Unknown_FailsAsUnknown()
    {
        var result = _validator.TestValidate(new ReindexDocumentCommand(Guid.NewGuid(), "v99"));
        result.ShouldHaveValidationErrorFor(c => c.IndexerVersion)
            .WithErrorMessage("Unknown indexer version 'v99'.");
    }
}
```

- [ ] **Step 3: Run tests — they should fail at compile time**

```
cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ReindexDocumentCommandValidatorTests" --no-build 2>&1 | tail -10
```

Expected: failure (validator non aggiornato).

- [ ] **Step 4: Update `ReindexDocumentCommandValidator`**

Sostituire l'intero contenuto:

```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for ReindexDocumentCommand. Issue #1673: enforces optional IndexerVersion
/// must be a known, selectable version.
/// </summary>
internal sealed class ReindexDocumentCommandValidator : AbstractValidator<ReindexDocumentCommand>
{
    public ReindexDocumentCommandValidator()
    {
        RuleFor(x => x.PdfId)
            .NotEmpty()
            .WithMessage("PDF ID is required.");

        RuleFor(x => x.IndexerVersion)
            .Cascade(CascadeMode.Stop)
            .Must(BeKnownIfProvided)
            .WithMessage(c => $"Unknown indexer version '{c.IndexerVersion}'.")
            .Must(BeSelectableIfProvided)
            .WithMessage(c => $"Indexer version '{c.IndexerVersion}' is not selectable (legacy marker).");
    }

    private static bool BeKnownIfProvided(string? version) =>
        version is null || IndexerVersionRegistry.TryGet(version, out _);

    private static bool BeSelectableIfProvided(string? version) =>
        version is null || IndexerVersionRegistry.IsSelectable(version);
}
```

- [ ] **Step 5: Run tests to verify they pass**

```
cd apps/api/src/Api && dotnet build && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ReindexDocumentCommandValidatorTests" --no-build 2>&1 | tail -10
```

Expected: 5 passed.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/ReindexDocumentCommand.cs apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Validators/ReindexDocumentCommandValidator.cs apps/api/tests/Api.Tests/Unit/DocumentProcessing/ReindexDocumentCommandValidatorTests.cs
git commit -m "feat(api/document-processing): #1673 extend ReindexDocumentCommand with IndexerVersion"
```

---

### Task 4: BE Handler — version resolution + conflict guard + persistence

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/ReindexDocumentCommandHandler.cs`
- Create: `apps/api/tests/Api.Tests/Unit/DocumentProcessing/ReindexDocumentCommandHandlerTests.cs`

- [ ] **Step 1: Write failing unit tests for handler**

```csharp
// apps/api/tests/Api.Tests/Unit/DocumentProcessing/ReindexDocumentCommandHandlerTests.cs
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "1673")]
public sealed class ReindexDocumentCommandHandlerTests : IAsyncLifetime
{
    private MeepleAiDbContext _db = default!;
    private Mock<IMediator> _mediator = default!;

    public ValueTask InitializeAsync()
    {
        var opts = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"reindex_{Guid.NewGuid():N}")
            .Options;
        _db = new MeepleAiDbContext(opts);
        _mediator = new Mock<IMediator>(MockBehavior.Strict);
        _mediator.Setup(m => m.Send(It.IsAny<EnqueuePdfCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<PdfDocumentEntity> SeedPdfAsync(string state = "Ready", string? indexerVersion = null)
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "test.pdf",
            FilePath = "/tmp/test.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = state,
            IndexerVersion = indexerVersion,
        };
        _db.PdfDocuments.Add(pdf);
        await _db.SaveChangesAsync();
        return pdf;
    }

    private ReindexDocumentCommandHandler CreateHandler() =>
        new(_db, _mediator.Object, NullLogger<ReindexDocumentCommandHandler>.Instance);

    [Fact]
    public async Task Handle_NoExplicitVersion_NoStoredVersion_UsesCurrent()
    {
        var pdf = await SeedPdfAsync();
        var handler = CreateHandler();

        await handler.Handle(new ReindexDocumentCommand(pdf.Id), CancellationToken.None);

        var reloaded = await _db.PdfDocuments.FirstAsync(p => p.Id == pdf.Id);
        reloaded.IndexerVersion.Should().Be("v1.0");
    }

    [Fact]
    public async Task Handle_NoExplicitVersion_StoredVersionPresent_UsesStored()
    {
        var pdf = await SeedPdfAsync(indexerVersion: "v1.0");
        var handler = CreateHandler();

        await handler.Handle(new ReindexDocumentCommand(pdf.Id), CancellationToken.None);

        var reloaded = await _db.PdfDocuments.FirstAsync(p => p.Id == pdf.Id);
        reloaded.IndexerVersion.Should().Be("v1.0");
    }

    [Fact]
    public async Task Handle_ExplicitVersionOverridesStored()
    {
        var pdf = await SeedPdfAsync(indexerVersion: "v0");
        var handler = CreateHandler();

        await handler.Handle(new ReindexDocumentCommand(pdf.Id, "v1.0"), CancellationToken.None);

        var reloaded = await _db.PdfDocuments.FirstAsync(p => p.Id == pdf.Id);
        reloaded.IndexerVersion.Should().Be("v1.0");
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Uploading")]
    [InlineData("Extracting")]
    [InlineData("Chunking")]
    [InlineData("Embedding")]
    [InlineData("Indexing")]
    public async Task Handle_DocInFlight_ThrowsConflictException(string state)
    {
        var pdf = await SeedPdfAsync(state: state);
        var handler = CreateHandler();

        var act = () => handler.Handle(new ReindexDocumentCommand(pdf.Id), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.WithMessage($"*currently being processed*state={state}*");
    }

    [Theory]
    [InlineData("Ready")]
    [InlineData("Failed")]
    public async Task Handle_DocTerminalState_AllowsReindex(string state)
    {
        var pdf = await SeedPdfAsync(state: state);
        var handler = CreateHandler();

        await handler.Handle(new ReindexDocumentCommand(pdf.Id), CancellationToken.None);

        var reloaded = await _db.PdfDocuments.FirstAsync(p => p.Id == pdf.Id);
        reloaded.ProcessingState.Should().Be("Pending");
    }

    [Fact]
    public async Task Handle_PdfNotFound_ThrowsNotFoundException()
    {
        var handler = CreateHandler();
        var act = () => handler.Handle(new ReindexDocumentCommand(Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_Success_EnqueuesPdfForProcessing()
    {
        var pdf = await SeedPdfAsync();
        var handler = CreateHandler();

        await handler.Handle(new ReindexDocumentCommand(pdf.Id), CancellationToken.None);

        _mediator.Verify(m => m.Send(
            It.Is<EnqueuePdfCommand>(c => c.PdfId == pdf.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2: Run failing tests**

```
cd apps/api/src/Api && dotnet build && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ReindexDocumentCommandHandlerTests" --no-build 2>&1 | tail -20
```

Expected: 11 failed (handler non aggiornato + `NotFoundException` non lanciato).

- [ ] **Step 3: Update handler**

Sostituire l'intero contenuto di `ReindexDocumentCommandHandler.cs`:

```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for ReindexDocumentCommand. Issue #1673 estende il flusso con:
/// 1. Risoluzione versione: <c>command.IndexerVersion ?? pdf.IndexerVersion ?? Current</c>.
/// 2. Conflict guard: se il documento è in pipeline (stati non-terminali), 409 Conflict.
/// 3. Persistenza della versione risolta su <c>pdf.IndexerVersion</c>.
/// 4. Audit via <c>[AuditableAction("DocumentReindex", "Document", Level=2)]</c> sul command.
/// </summary>
internal sealed class ReindexDocumentCommandHandler : ICommandHandler<ReindexDocumentCommand>
{
    // Stati pre-terminali. Reindex bloccato finché non si raggiunge Ready o Failed.
    private static readonly HashSet<string> InFlightStates =
        new(StringComparer.Ordinal)
        {
            "Pending",
            "Uploading",
            "Extracting",
            "Chunking",
            "Embedding",
            "Indexing",
        };

    private readonly MeepleAiDbContext _dbContext;
    private readonly IMediator _mediator;
    private readonly ILogger<ReindexDocumentCommandHandler> _logger;

    public ReindexDocumentCommandHandler(
        MeepleAiDbContext dbContext,
        IMediator mediator,
        ILogger<ReindexDocumentCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(ReindexDocumentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var pdf = await _dbContext.PdfDocuments
            .FirstOrDefaultAsync(p => p.Id == command.PdfId, cancellationToken)
            .ConfigureAwait(false);

        if (pdf is null)
        {
            throw new NotFoundException($"PDF document {command.PdfId} not found");
        }

        // Conflict guard: rifiuta il reindex se la pipeline è in-flight.
        if (InFlightStates.Contains(pdf.ProcessingState))
        {
            throw new ConflictException(
                $"Document {command.PdfId} is currently being processed (state={pdf.ProcessingState}); cannot reindex until it reaches Ready or Failed.");
        }

        // Risoluzione versione: explicit → stored → current.
        var resolvedVersion = command.IndexerVersion
            ?? pdf.IndexerVersion
            ?? IndexerVersionRegistry.Current.Version;

        // Cancella chunks associati.
        var chunks = await _dbContext.TextChunks
            .Where(tc => tc.PdfDocumentId == command.PdfId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (chunks.Count > 0)
        {
            _dbContext.TextChunks.RemoveRange(chunks);
        }

        // Reset state + scrive la versione risolta.
        pdf.ProcessingState = "Pending";
        pdf.ProcessedAt = null;
        pdf.ProcessingError = null;
        pdf.RetryCount = 0;
        pdf.ErrorCategory = null;
        pdf.FailedAtState = null;
        pdf.IndexerVersion = resolvedVersion;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Enqueue Quartz.
        try
        {
            var userId = pdf.UploadedByUserId;
            await _mediator.Send(
                new EnqueuePdfCommand(command.PdfId, userId, Priority: 0),
                cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Reindexed PDF {PdfId} with version {IndexerVersion} enqueued for processing",
                command.PdfId, resolvedVersion);
        }
#pragma warning disable CA1031 // Best-effort enqueue
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not enqueue reindexed PDF {PdfId} (may already be queued)", command.PdfId);
        }
#pragma warning restore CA1031
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```
cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ReindexDocumentCommandHandlerTests" --no-build 2>&1 | tail -15
```

Expected: 11 passed.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/ReindexDocumentCommandHandler.cs apps/api/tests/Api.Tests/Unit/DocumentProcessing/ReindexDocumentCommandHandlerTests.cs
git commit -m "feat(api/document-processing): #1673 version resolution + conflict guard in reindex handler"
```

---

### Task 5: BE Query — `GetIndexerVersionRegistryQuery` + handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetIndexerVersionRegistry/GetIndexerVersionRegistryQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetIndexerVersionRegistry/GetIndexerVersionRegistryHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetIndexerVersionRegistry/IndexerVersionDto.cs`
- Create: `apps/api/tests/Api.Tests/Unit/DocumentProcessing/GetIndexerVersionRegistryHandlerTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// apps/api/tests/Api.Tests/Unit/DocumentProcessing/GetIndexerVersionRegistryHandlerTests.cs
using Api.BoundedContexts.DocumentProcessing.Application.Queries.GetIndexerVersionRegistry;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "1673")]
public sealed class GetIndexerVersionRegistryHandlerTests
{
    private readonly GetIndexerVersionRegistryHandler _handler = new();

    [Fact]
    public async Task Handle_ReturnsOnlySelectableVersions()
    {
        var result = await _handler.Handle(new GetIndexerVersionRegistryQuery(), CancellationToken.None);

        result.Should().NotBeEmpty();
        result.Should().OnlyContain(v => !string.Equals(v.Version, "v0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Handle_MarksCurrentVersion()
    {
        var result = await _handler.Handle(new GetIndexerVersionRegistryQuery(), CancellationToken.None);

        var current = result.Should().ContainSingle(v => v.IsCurrent).Subject;
        current.Version.Should().Be("v1.0");
        current.DisplayName.Should().NotBeNullOrWhiteSpace();
    }
}
```

- [ ] **Step 2: Run failing tests**

```
cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GetIndexerVersionRegistryHandlerTests" --no-build 2>&1 | tail -10
```

Expected: build failure (`GetIndexerVersionRegistryQuery` not defined).

- [ ] **Step 3: Implement DTO**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetIndexerVersionRegistry/IndexerVersionDto.cs
namespace Api.BoundedContexts.DocumentProcessing.Application.Queries.GetIndexerVersionRegistry;

/// <summary>
/// Public projection of an <see cref="Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects.IndexerVersion"/>.
/// </summary>
public sealed record IndexerVersionDto(string Version, string DisplayName, bool IsCurrent);
```

- [ ] **Step 4: Implement Query + Handler**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetIndexerVersionRegistry/GetIndexerVersionRegistryQuery.cs
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries.GetIndexerVersionRegistry;

/// <summary>
/// Returns selectable indexer versions for the admin dropdown.
/// Issue #1673.
/// </summary>
internal sealed record GetIndexerVersionRegistryQuery : IQuery<IReadOnlyList<IndexerVersionDto>>;
```

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetIndexerVersionRegistry/GetIndexerVersionRegistryHandler.cs
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries.GetIndexerVersionRegistry;

internal sealed class GetIndexerVersionRegistryHandler
    : IQueryHandler<GetIndexerVersionRegistryQuery, IReadOnlyList<IndexerVersionDto>>
{
    public Task<IReadOnlyList<IndexerVersionDto>> Handle(
        GetIndexerVersionRegistryQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IndexerVersionDto> result = IndexerVersionRegistry.Selectable
            .Select(v => new IndexerVersionDto(
                Version: v.Version,
                DisplayName: v.DisplayName,
                IsCurrent: string.Equals(v.Version, IndexerVersionRegistry.Current.Version, StringComparison.Ordinal)))
            .ToList();
        return Task.FromResult(result);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

```
cd apps/api/src/Api && dotnet build && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GetIndexerVersionRegistryHandlerTests" --no-build 2>&1 | tail -10
```

Expected: 2 passed.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetIndexerVersionRegistry/ apps/api/tests/Api.Tests/Unit/DocumentProcessing/GetIndexerVersionRegistryHandlerTests.cs
git commit -m "feat(api/document-processing): #1673 GetIndexerVersionRegistryQuery"
```

---

### Task 6: BE Endpoints — `GET /admin/indexer/versions` + extend reindex body

**Files:**
- Create: `apps/api/src/Api/Routing/AdminIndexerEndpoints.cs`
- Modify: `apps/api/src/Api/Routing/AdminPdfManagementEndpoints.cs`
- Modify: `apps/api/src/Api/Program.cs`

- [ ] **Step 1: Create `AdminIndexerEndpoints.cs`**

```csharp
// apps/api/src/Api/Routing/AdminIndexerEndpoints.cs
using Api.BoundedContexts.DocumentProcessing.Application.Queries.GetIndexerVersionRegistry;
using Api.Filters;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for indexer pipeline metadata.
/// Issue #1673: registry per dropdown versione reindex.
/// </summary>
internal static class AdminIndexerEndpoints
{
    public static void MapAdminIndexerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/indexer")
            .WithTags("Admin - Indexer")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        group.MapGet("/versions", GetVersions)
            .WithName("GetIndexerVersions")
            .WithSummary("Returns selectable indexer versions for the reindex dropdown");
    }

    private static async Task<IResult> GetVersions(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var versions = await mediator.Send(new GetIndexerVersionRegistryQuery(), cancellationToken)
            .ConfigureAwait(false);
        return Results.Ok(versions);
    }
}
```

- [ ] **Step 2: Update reindex endpoint to accept optional body**

In `apps/api/src/Api/Routing/AdminPdfManagementEndpoints.cs`, sostituire il metodo `ReindexDocument` e aggiungere il record richiesta in fondo al file:

```csharp
    private static async Task<IResult> ReindexDocument(
        Guid pdfId,
        ReindexDocumentRequest? request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(
            new ReindexDocumentCommand(pdfId, request?.IndexerVersion),
            cancellationToken).ConfigureAwait(false);
        return Results.Ok(new { success = true, message = "Document queued for reindexing" });
    }
```

In fondo al file, dopo `BulkDeletePdfsRequest`:

```csharp
internal record ReindexDocumentRequest(string? IndexerVersion);
```

- [ ] **Step 3: Wire endpoint group in Program.cs**

Cercare `MapAdminPdfManagementEndpoints` in `Program.cs` e affiancare la chiamata:

```bash
grep -n "MapAdminPdfManagementEndpoints" apps/api/src/Api/Program.cs
```

Sotto la riga trovata, aggiungere:

```csharp
app.MapAdminIndexerEndpoints();
```

- [ ] **Step 4: Verify build**

```
dotnet build apps/api/src/Api/Api.csproj --no-restore 2>&1 | tail -5
```

Expected: Build succeeded. 0 Error(s).

- [ ] **Step 5: Run full BE test suite for regressions**

```
cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "BoundedContext=DocumentProcessing&Category=Unit" --no-build 2>&1 | tail -10
```

Expected: tutti i test DocumentProcessing pass.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Routing/AdminIndexerEndpoints.cs apps/api/src/Api/Routing/AdminPdfManagementEndpoints.cs apps/api/src/Api/Program.cs
git commit -m "feat(api/routing): #1673 GET /admin/indexer/versions + extend reindex body"
```

---

### Task 7: BE Integration test — end-to-end reindex + 409 in-flight

**Files:**
- Create: `apps/api/tests/Api.Tests/Integration/DocumentProcessing/ReindexDocumentVersionIntegrationTests.cs`

- [ ] **Step 1: Write integration test**

Pattern modellato su `DeleteKbDocumentCommandHandlerIntegrationTests.cs`. Usa Testcontainers Postgres via `SharedTestcontainersFixture`.

```csharp
// apps/api/tests/Api.Tests/Integration/DocumentProcessing/ReindexDocumentVersionIntegrationTests.cs
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Integration tests for ReindexDocumentCommandHandler version selector. Issue #1673.
/// Verifies version persistence + audit row + 409 on in-flight reindex.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "1673")]
public sealed class ReindexDocumentVersionIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private IMediator? _mediator;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private static readonly Guid TestUserId = new("A0000000-0000-0000-0000-000000001673");
    private static readonly Guid TestSharedGameId = new("B0000000-0000-0000-0000-000000001673");

    public ReindexDocumentVersionIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_reindex_v_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        await _dbContext.Database.MigrateAsync(TestCancellationToken);
        await SeedBaseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null) await _dbContext.DisposeAsync();
        if (_serviceProvider is IAsyncDisposable d) await d.DisposeAsync();
        await _fixture.DropDatabaseAsync(_databaseName);
    }

    private async Task SeedBaseAsync()
    {
        _dbContext!.Set<UserEntity>().Add(new UserEntity
        {
            Id = TestUserId,
            Email = "reindex-v-test@meepleai.test",
            PasswordHash = "x",
            DisplayName = "Reindex V Test",
        });
        _dbContext.Set<SharedGameEntity>().Add(new SharedGameEntity
        {
            Id = TestSharedGameId,
            Name = "Reindex V Test Game",
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private async Task<PdfDocumentEntity> SeedPdfAsync(string state = "Ready", string? indexerVersion = null)
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "reindex-v.pdf",
            FilePath = "/tmp/reindex-v.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = TestUserId,
            SharedGameId = TestSharedGameId,
            ProcessingState = state,
            IndexerVersion = indexerVersion,
        };
        _dbContext!.PdfDocuments.Add(pdf);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        return pdf;
    }

    [Fact]
    public async Task Reindex_ExplicitVersion_PersistsOnEntity()
    {
        var pdf = await SeedPdfAsync();

        await _mediator!.Send(
            new ReindexDocumentCommand(pdf.Id, "v1.0"),
            TestCancellationToken);

        var reloaded = await _dbContext!.PdfDocuments
            .FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);
        reloaded.IndexerVersion.Should().Be("v1.0");
        reloaded.ProcessingState.Should().Be("Pending");
    }

    [Fact]
    public async Task Reindex_NullVersionWithStoredV0_KeepsStoredValue()
    {
        // v0 backfill marker stored → reindex senza override usa il valore stored, NON Current.
        // L'admin che vuole promuovere alla pipeline corrente deve passare esplicitamente "v1.0"
        // dal dropdown (Adzic scenario di rollback / promote-on-demand).
        var pdf = await SeedPdfAsync(indexerVersion: "v0");

        await _mediator!.Send(
            new ReindexDocumentCommand(pdf.Id),
            TestCancellationToken);

        var reloaded = await _dbContext!.PdfDocuments
            .FirstAsync(p => p.Id == pdf.Id, TestCancellationToken);
        reloaded.IndexerVersion.Should().Be("v0");
    }

    [Fact]
    public async Task Reindex_InFlight_ThrowsConflictException()
    {
        var pdf = await SeedPdfAsync(state: "Chunking");

        var act = () => _mediator!.Send(
            new ReindexDocumentCommand(pdf.Id, "v1.0"),
            TestCancellationToken);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Reindex_WritesAuditOutboxRow()
    {
        var pdf = await SeedPdfAsync();

        await _mediator!.Send(
            new ReindexDocumentCommand(pdf.Id, "v1.0"),
            TestCancellationToken);

        // Filtra client-side (jsonb non supporta SQL LIKE) per identificare la riga
        // di audit emessa dal command pattern [AuditableAction].
        var rows = await _dbContext!.AuditOutbox.AsNoTracking().ToListAsync(TestCancellationToken);
        rows.Should().Contain(r =>
            string.Equals(r.Action, "DocumentReindex", StringComparison.Ordinal)
            && string.Equals(r.Resource, "Document", StringComparison.Ordinal));
    }
}
```

> **Nota integration test infrastructure**: la classe `IntegrationServiceCollectionBuilder.CreateBase` registra MediatR + EF + MeepleAiDbContext con la connection string isolata. Se manca un servizio richiesto dal handler (es. `IAuditOutboxRepository`) la chiamata di test fallirà sollevando un'eccezione di DI: in quel caso aggiungere `services.AddScoped<...>` nel test prima di `BuildServiceProvider()`, **mai** mockare l'audit outbox — è proprio la riga che vogliamo verificare.

- [ ] **Step 2: Run integration tests**

```
cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ReindexDocumentVersionIntegrationTests" --no-build 2>&1 | tail -15
```

Expected: 4 passed.

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/Integration/DocumentProcessing/ReindexDocumentVersionIntegrationTests.cs
git commit -m "test(api/document-processing): #1673 reindex version E2E + 409 + audit"
```

---

### Task 8: BE DTO — expose `IndexerVersion` in `KbDocumentDto`

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbDocumentById/KbDocumentDto.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbDocumentById/GetKbDocumentByIdHandler.cs`

- [ ] **Step 1: Extend DTO**

Sostituire la signature di `KbDocumentDto`:

```csharp
internal sealed record KbDocumentDto(
    Guid Id,
    string Title,
    string DocType,
    Guid? GameId,
    string? GameName,
    string UploaderName,
    DateTime UploadedAt,
    DateTime LastIngestedAt,
    string ProcessingStatus,
    int ChunkCount,
    int? PageCount,
    string Language,
    IReadOnlyList<string> Tags,
    long FileSize,
    // Issue #1673: indexer version applicato all'ultimo reindex (null = mai indicizzato; "v0" = pre-versioning).
    string? IndexerVersion
);
```

- [ ] **Step 2: Populate field in handler**

In `GetKbDocumentByIdHandler.cs:156-173`, aggiungere `IndexerVersion: data.pdf.IndexerVersion,` come ultimo argomento nel `new KbDocumentDto(...)`.

- [ ] **Step 3: Verify build**

```
dotnet build apps/api/src/Api/Api.csproj --no-restore 2>&1 | tail -5
```

Expected: Build succeeded.

- [ ] **Step 4: Run KnowledgeBase tests**

```
cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~GetKbDocumentById" --no-build 2>&1 | tail -10
```

Expected: tutti i test KB pass. Eventuali test che asseriscono lo shape del DTO con un costruttore posizionale vanno aggiornati (l'ultimo parametro è `IndexerVersion: null` per default nei mock).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbDocumentById/KbDocumentDto.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbDocumentById/GetKbDocumentByIdHandler.cs
git commit -m "feat(api/knowledge-base): #1673 surface IndexerVersion in KbDocumentDto"
```

---

### Task 9: FE Client + Zod — `indexerVersion` schemas + API client

**Files:**
- Create: `apps/web/src/lib/api/schemas/indexer-versions.schemas.ts`
- Modify: `apps/web/src/lib/api/schemas/kb-chunks.schemas.ts`
- Modify: `apps/web/src/lib/api/clients/pdfClient.ts`

- [ ] **Step 1: Create indexer-versions schema**

```typescript
// apps/web/src/lib/api/schemas/indexer-versions.schemas.ts
/**
 * Indexer Versions Schemas (Issue #1673)
 *
 * Zod schemas for GET /api/v1/admin/indexer/versions.
 * Matches IndexerVersionDto from
 * apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetIndexerVersionRegistry/IndexerVersionDto.cs.
 */

import { z } from 'zod';

export const IndexerVersionSchema = z.object({
  version: z.string().min(1),
  displayName: z.string().min(1),
  isCurrent: z.boolean(),
});

export type IndexerVersion = z.infer<typeof IndexerVersionSchema>;

export const IndexerVersionListSchema = z.array(IndexerVersionSchema);
export type IndexerVersionList = z.infer<typeof IndexerVersionListSchema>;
```

- [ ] **Step 2: Extend `KbDocDetailSchema`**

In `apps/web/src/lib/api/schemas/kb-chunks.schemas.ts:33-51`, aggiungere alla definizione di `KbDocDetailSchema`, dopo `fileSize: z.number().int().nonnegative().optional(),`:

```typescript
  // Issue #1673: indexer version applicato all'ultimo reindex. Nullable per documenti
  // mai indicizzati; "v0" è il marker legacy pre-versioning (read-only nell'UI).
  indexerVersion: z.string().min(1).nullable().optional(),
```

- [ ] **Step 3: Extend `pdfClient.reindexDocument` + add `getIndexerVersions`**

In `apps/web/src/lib/api/clients/pdfClient.ts`, trovare `reindexDocument` (riga ~355) e sostituirla con:

```typescript
    /**
     * Reindex a PDF document with optional indexer version (Issue #1673).
     * POST /api/v1/admin/pdfs/{pdfId}/reindex
     */
    async reindexDocument(
      pdfId: string,
      body?: { indexerVersion?: string }
    ): Promise<void> {
      const payload =
        body?.indexerVersion !== undefined ? { indexerVersion: body.indexerVersion } : {};
      return httpClient.post(
        `/api/v1/admin/pdfs/${encodeURIComponent(pdfId)}/reindex`,
        payload
      );
    },

    /**
     * Get the registry of selectable indexer versions (Issue #1673).
     * GET /api/v1/admin/indexer/versions
     */
    async getIndexerVersions(): Promise<IndexerVersionList> {
      return httpClient.get('/api/v1/admin/indexer/versions', IndexerVersionListSchema);
    },
```

In testa al file aggiungere l'import:

```typescript
import {
  IndexerVersionListSchema,
  type IndexerVersionList,
} from '../schemas/indexer-versions.schemas';
```

Se la pagina barrel `apps/web/src/lib/api/schemas/index.ts` esporta gli schemi, aggiungere `export * from './indexer-versions.schemas';` (verifica con `grep -n "from './kb-chunks.schemas'" apps/web/src/lib/api/schemas/index.ts` per il pattern esistente).

- [ ] **Step 4: Type-check FE**

```
cd apps/web && pnpm typecheck 2>&1 | tail -10
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/api/schemas/indexer-versions.schemas.ts apps/web/src/lib/api/schemas/kb-chunks.schemas.ts apps/web/src/lib/api/clients/pdfClient.ts apps/web/src/lib/api/schemas/index.ts
git commit -m "feat(web/api): #1673 indexer versions client + schema + indexerVersion in KbDocDetail"
```

---

### Task 10: FE Hooks — `useIndexerVersions` + extend `useReindexDoc`

**Files:**
- Create: `apps/web/src/hooks/queries/useIndexerVersions.ts`
- Modify: `apps/web/src/hooks/queries/useKbDocActions.ts`
- Create: `apps/web/src/hooks/queries/__tests__/useIndexerVersions.test.tsx`

- [ ] **Step 1: Create `useIndexerVersions` hook**

```typescript
// apps/web/src/hooks/queries/useIndexerVersions.ts
/**
 * useIndexerVersions — TanStack Query hook for the indexer version registry.
 * Issue #1673.
 */

'use client';

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { IndexerVersionList } from '@/lib/api/schemas/indexer-versions.schemas';

export const indexerVersionsKeys = {
  all: ['admin', 'indexer', 'versions'] as const,
};

/**
 * Fetch the selectable indexer versions for the reindex dropdown.
 * The registry is static within a deploy, so we cache for 1 hour and disable refetch on focus.
 */
export function useIndexerVersions(): UseQueryResult<IndexerVersionList, Error> {
  return useQuery({
    queryKey: indexerVersionsKeys.all,
    queryFn: () => api.pdf.getIndexerVersions(),
    staleTime: 60 * 60 * 1000,
    refetchOnWindowFocus: false,
  });
}
```

- [ ] **Step 2: Update `useReindexDoc` to accept payload**

In `apps/web/src/hooks/queries/useKbDocActions.ts:68-77`, sostituire `useReindexDoc` con:

```typescript
/**
 * Trigger a reindex for a specific document (admin). Optionally pass an indexer
 * version to override the stored one. Issue #1673.
 *
 * Invalidates the detail view and the full chunks list for this doc.
 *
 * @example
 * const { mutateAsync } = useReindexDoc(docId);
 * await mutateAsync({ indexerVersion: 'v1.0' });
 */
export function useReindexDoc(
  docId: string
): UseMutationResult<void, Error, { indexerVersion?: string } | void> {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (payload) =>
      api.pdf.reindexDocument(
        docId,
        payload && 'indexerVersion' in payload && payload.indexerVersion !== undefined
          ? { indexerVersion: payload.indexerVersion }
          : undefined
      ),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: kbDocDetailKeys.byId(docId) });
      qc.invalidateQueries({ queryKey: kbChunksListKeys.all });
    },
  });
}
```

> **Compatibilità chiamate esistenti**: il chiamante attuale `reindexMutation.mutate(undefined, ...)` resta valido grazie al union type `… | void`. Le chiamate non vengono rompute.

- [ ] **Step 3: Write hook unit test**

```typescript
// apps/web/src/hooks/queries/__tests__/useIndexerVersions.test.tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { describe, expect, it, vi, beforeEach } from 'vitest';

import { api } from '@/lib/api';

import { useIndexerVersions } from '../useIndexerVersions';

vi.mock('@/lib/api', () => ({
  api: {
    pdf: {
      getIndexerVersions: vi.fn(),
    },
  },
}));

function makeWrapper() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

describe('useIndexerVersions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns the registry list on success', async () => {
    vi.mocked(api.pdf.getIndexerVersions).mockResolvedValue([
      { version: 'v1.0', displayName: 'v1.0 — current pipeline', isCurrent: true },
    ]);

    const { result } = renderHook(() => useIndexerVersions(), { wrapper: makeWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual([
      { version: 'v1.0', displayName: 'v1.0 — current pipeline', isCurrent: true },
    ]);
  });

  it('exposes errors via isError', async () => {
    vi.mocked(api.pdf.getIndexerVersions).mockRejectedValue(new Error('boom'));

    const { result } = renderHook(() => useIndexerVersions(), { wrapper: makeWrapper() });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.message).toBe('boom');
  });
});
```

- [ ] **Step 4: Run FE tests**

```
cd apps/web && pnpm test --run src/hooks/queries/__tests__/useIndexerVersions.test.tsx 2>&1 | tail -20
```

Expected: 2 passed.

- [ ] **Step 5: Run existing `useKbDocActions` tests for regression**

```
cd apps/web && pnpm test --run src/hooks/queries/__tests__/useKbDocActions.test.tsx 2>&1 | tail -10
```

Expected: all pass — the `mutate(undefined, ...)` call signature stays valid.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/hooks/queries/useIndexerVersions.ts apps/web/src/hooks/queries/useKbDocActions.ts apps/web/src/hooks/queries/__tests__/useIndexerVersions.test.tsx
git commit -m "feat(web/hooks): #1673 useIndexerVersions + useReindexDoc payload support"
```

---

### Task 11: FE Component — `<KbReindexDropdown>`

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/explorer/actions/KbReindexDropdown.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/explorer/actions/__tests__/KbReindexDropdown.test.tsx`

- [ ] **Step 1: Write component test (TDD-first)**

```tsx
// apps/web/src/components/admin/knowledge-base/explorer/actions/__tests__/KbReindexDropdown.test.tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { describe, expect, it, vi, beforeEach } from 'vitest';

import { api } from '@/lib/api';

import { KbReindexDropdown } from '../KbReindexDropdown';

vi.mock('@/lib/api', () => ({
  api: {
    pdf: {
      reindexDocument: vi.fn(),
      getIndexerVersions: vi.fn(),
    },
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

function makeWrapper() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

describe('KbReindexDropdown', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.pdf.getIndexerVersions).mockResolvedValue([
      { version: 'v1.0', displayName: 'v1.0 — current pipeline', isCurrent: true },
    ]);
    vi.mocked(api.pdf.reindexDocument).mockResolvedValue(undefined);
  });

  it('renders the default reindex button (current version)', async () => {
    render(<KbReindexDropdown docId="abc" processingStatus="ready" />, {
      wrapper: makeWrapper(),
    });

    expect(await screen.findByRole('button', { name: /re-index/i })).toBeInTheDocument();
  });

  it('disables the trigger while processing/queued', () => {
    render(<KbReindexDropdown docId="abc" processingStatus="processing" />, {
      wrapper: makeWrapper(),
    });

    expect(screen.getByRole('button', { name: /re-index/i })).toBeDisabled();
  });

  it('opens the version menu and triggers reindex with selected version', async () => {
    const user = userEvent.setup();
    render(<KbReindexDropdown docId="abc" processingStatus="ready" />, {
      wrapper: makeWrapper(),
    });

    await user.click(await screen.findByRole('button', { name: /scegli versione/i }));
    await user.click(await screen.findByRole('menuitem', { name: /v1\.0/i }));

    await waitFor(() =>
      expect(api.pdf.reindexDocument).toHaveBeenCalledWith('abc', { indexerVersion: 'v1.0' })
    );
  });

  it('default click reindexes without explicit version (server uses Current)', async () => {
    const user = userEvent.setup();
    render(<KbReindexDropdown docId="abc" processingStatus="ready" />, {
      wrapper: makeWrapper(),
    });

    await user.click(await screen.findByRole('button', { name: /^⟳ re-index$/i }));

    await waitFor(() =>
      expect(api.pdf.reindexDocument).toHaveBeenCalledWith('abc', undefined)
    );
  });
});
```

- [ ] **Step 2: Run failing tests**

```
cd apps/web && pnpm test --run src/components/admin/knowledge-base/explorer/actions/__tests__/KbReindexDropdown.test.tsx 2>&1 | tail -10
```

Expected: failure (component does not exist).

- [ ] **Step 3: Implement component**

```tsx
// apps/web/src/components/admin/knowledge-base/explorer/actions/KbReindexDropdown.tsx
'use client';

import { useState } from 'react';

import { toast } from 'sonner';

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { useReindexDoc } from '@/hooks/queries/useKbDocActions';
import { useIndexerVersions } from '@/hooks/queries/useIndexerVersions';

export interface KbReindexDropdownProps {
  readonly docId: string;
  readonly processingStatus: 'queued' | 'processing' | 'ready' | 'failed';
}

const FOCUS_RING =
  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2';

/**
 * KbReindexDropdown — split-button per il re-index versionato (Issue #1673).
 *
 * Layout:
 *   [ ⟳ Re-index ] [ ▾ ]
 *
 * Comportamento:
 *   - Click sul body → reindex con default server (`indexerVersion` omesso).
 *   - Click sul caret → menu con versioni selectable; selezione → reindex con versione esplicita.
 *   - Entrambi i bottoni si disabilitano quando il documento è in pipeline.
 */
export function KbReindexDropdown({ docId, processingStatus }: KbReindexDropdownProps) {
  const reindex = useReindexDoc(docId);
  const versionsQuery = useIndexerVersions();
  const [menuOpen, setMenuOpen] = useState(false);

  const disabled =
    processingStatus === 'processing' ||
    processingStatus === 'queued' ||
    reindex.isPending;

  const runReindex = (indexerVersion?: string) => {
    const payload = indexerVersion ? { indexerVersion } : undefined;
    reindex.mutate(payload, {
      onSuccess: () =>
        toast.success(
          indexerVersion
            ? `Re-index avviato (${indexerVersion})`
            : 'Re-index avviato'
        ),
      onError: (err: Error) => toast.error(`Re-index fallito: ${err.message}`),
    });
  };

  return (
    <div className="inline-flex">
      <button
        type="button"
        onClick={() => runReindex()}
        disabled={disabled}
        className={`rounded-l-md border border-r-0 border-border px-3 py-1.5 text-xs font-medium hover:bg-muted/70 disabled:cursor-not-allowed disabled:opacity-50 ${FOCUS_RING}`}
        aria-label="⟳ Re-index"
      >
        ⟳ Re-index
      </button>
      <DropdownMenu open={menuOpen} onOpenChange={setMenuOpen}>
        <DropdownMenuTrigger asChild>
          <button
            type="button"
            disabled={disabled || versionsQuery.isLoading}
            className={`rounded-r-md border border-border px-2 py-1.5 text-xs font-medium hover:bg-muted/70 disabled:cursor-not-allowed disabled:opacity-50 ${FOCUS_RING}`}
            aria-label="Scegli versione"
          >
            ▾
          </button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          {versionsQuery.data?.map((v) => (
            <DropdownMenuItem
              key={v.version}
              onSelect={() => runReindex(v.version)}
            >
              {v.displayName}
              {v.isCurrent ? ' · default' : ''}
            </DropdownMenuItem>
          ))}
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}
```

> **Nota componente `DropdownMenu`**: deve esistere in `@/components/ui/dropdown-menu`. Verifica con `ls apps/web/src/components/ui/dropdown-menu*`. Se manca, usa shadcn CLI: `cd apps/web && pnpm dlx shadcn@latest add dropdown-menu`. Se il design system ha già un'astrazione equivalente (es. `Menu` di Radix), prediligi quella e aggiorna l'import.

- [ ] **Step 4: Run component tests**

```
cd apps/web && pnpm test --run src/components/admin/knowledge-base/explorer/actions/__tests__/KbReindexDropdown.test.tsx 2>&1 | tail -15
```

Expected: 4 passed.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/explorer/actions/KbReindexDropdown.tsx apps/web/src/components/admin/knowledge-base/explorer/actions/__tests__/KbReindexDropdown.test.tsx
git commit -m "feat(web/admin-kb): #1673 KbReindexDropdown split-button"
```

---

### Task 12: FE Integration — swap button in `KbDocActions` + render hero metadata

**Files:**
- Modify: `apps/web/src/components/admin/knowledge-base/explorer/actions/KbDocActions.tsx`
- Modify: `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx`
- Modify (if present): `apps/web/src/components/admin/knowledge-base/explorer/actions/__tests__/KbDocActions.test.tsx`

- [ ] **Step 1: Swap the button in `KbDocActions`**

In `apps/web/src/components/admin/knowledge-base/explorer/actions/KbDocActions.tsx`:

1. **Rimuovi** l'import di `useReindexDoc` e il blocco `reindexMutation` + `handleReindex` + il bottone `⟳ Re-index` corrente (linee 12, 50, 64-69, 105-118).
2. **Aggiungi** l'import:

   ```typescript
   import { KbReindexDropdown } from './KbReindexDropdown';
   ```

3. **Sostituisci** il primo bottone con il componente:

   ```tsx
   {/* 1. Re-index (split-button con dropdown versione — Issue #1673) */}
   <KbReindexDropdown docId={docId} processingStatus={processingStatus} />
   ```

`isReindexDisabled` non serve più (gestito internamente dal dropdown).

- [ ] **Step 2: Update `KbDocActions` test if it referenced the old button**

```
grep -n "Re-index\|reindex" apps/web/src/components/admin/knowledge-base/explorer/actions/__tests__/KbDocActions.test.tsx
```

Se il test esistente click-a il vecchio bottone, sostituisci l'asserzione con uno spy sul mock di `KbReindexDropdown` (es. `vi.mock('../KbReindexDropdown', () => ({ KbReindexDropdown: ({ docId }: { docId: string }) => <button data-testid="reindex-dropdown">{docId}</button> }))`) per mantenere l'isolamento del unit test.

- [ ] **Step 3: Render `indexerVersion` nel hero metadata del detail panel**

Apri `apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx`. Cerca la sezione `doc-stats` o equivalente dove vengono renderizzati `fileSize` / `lastIngestedAt`:

```
grep -n "fileSize\|lastIngestedAt" apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx
```

Aggiungi, accanto agli altri stats e dopo `fileSize`:

```tsx
{doc.indexerVersion ? (
  <span className="inline-flex items-center gap-1 text-xs text-muted-foreground">
    📦 {doc.indexerVersion === 'v0' ? 'v0 (legacy)' : doc.indexerVersion}
  </span>
) : null}
```

Il guard `doc.indexerVersion ? … : null` evita di mostrare la riga per doc mai indicizzati.

- [ ] **Step 4: Run all admin/knowledge-base tests**

```
cd apps/web && pnpm test --run src/components/admin/knowledge-base 2>&1 | tail -15
```

Expected: all pass.

- [ ] **Step 5: Type-check + lint**

```
cd apps/web && pnpm typecheck && pnpm lint 2>&1 | tail -10
```

Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/explorer/actions/KbDocActions.tsx apps/web/src/components/admin/knowledge-base/explorer/KbDocDetailPanel.tsx apps/web/src/components/admin/knowledge-base/explorer/actions/__tests__/KbDocActions.test.tsx
git commit -m "feat(web/admin-kb): #1673 wire reindex dropdown + render indexerVersion in hero"
```

---

### Task 13: PR creation + issue close-out

**Files:**
- (no code changes)

- [ ] **Step 1: Final BE + FE smoke run**

```
cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "BoundedContext=DocumentProcessing" --no-build 2>&1 | tail -10
cd ../../../web && pnpm test --run src/components/admin/knowledge-base src/hooks/queries 2>&1 | tail -10
cd ../../../web && pnpm typecheck && pnpm lint 2>&1 | tail -5
```

Expected: tutti green.

- [ ] **Step 2: Verify branch is up-to-date with main-dev**

```
git fetch origin main-dev
git log --oneline origin/main-dev..HEAD | head -20
git log --oneline HEAD..origin/main-dev | head -5
```

Se ci sono commit nuovi su origin/main-dev, fare rebase: `git rebase origin/main-dev`. Risolvere conflitti (probabili su `MeepleAiDbContextModelSnapshot.cs` se altre migration sono atterrate nel frattempo; in tal caso rigenerare con `dotnet ef migrations remove && dotnet ef migrations add AddIndexerVersionToPdfDocuments`).

- [ ] **Step 3: Push branch + open PR**

```
git push -u origin feature/issue-1673-reindex-version-selector
gh pr create --base main-dev --title "feat(admin-kb): #1673 re-index with version selector" --body "$(cat <<'EOF'
## Summary

Implementa #1673 (P3) — selettore versione per il re-index dei documenti KB lato admin, in linea con il design doc `2026-06-01-sp5-admin-kb-fu4-spinouts-design.md` §3.2 (D-B: code-resident registry).

## Changes

### BE
- Nuovo value object \`IndexerVersion\` + registry code-resident (\`v0\` legacy + \`v1.0\` current selectable).
- Migration \`AddIndexerVersionToPdfDocuments\` con backfill \`'v0'\` per righe storiche.
- \`ReindexDocumentCommand\` esteso con \`IndexerVersion\` opzionale + \`[AuditableAction("DocumentReindex", "Document", Level=2)]\`.
- Handler: version resolution chain (explicit → stored → current), conflict guard 409 su stato in-flight, persistenza versione applicata.
- Nuovo endpoint \`GET /api/v1/admin/indexer/versions\` + estensione body \`POST /admin/pdfs/{id}/reindex\`.
- \`KbDocumentDto\` esteso con \`IndexerVersion\` (chiude #1676 sub-task F3).

### FE
- Zod schema \`indexer-versions.schemas.ts\` + \`indexerVersion\` opzionale in \`KbDocDetailSchema\`.
- \`pdfClient.reindexDocument(pdfId, body?)\` + \`pdfClient.getIndexerVersions()\`.
- \`useIndexerVersions()\` hook + \`useReindexDoc\` accetta payload.
- \`<KbReindexDropdown>\` split-button (default click reindex con server-current; caret = dropdown versione).
- Hero metadata del detail panel mostra \`📦 v1.0\` (o \`v0 (legacy)\`).

## Test plan
- [ ] BE unit \`IndexerVersionRegistryTests\` (8 tests)
- [ ] BE unit \`ReindexDocumentCommandValidatorTests\` (5)
- [ ] BE unit \`ReindexDocumentCommandHandlerTests\` (11, incl. 6 in-flight states + 2 terminal states)
- [ ] BE unit \`GetIndexerVersionRegistryHandlerTests\` (2)
- [ ] BE integration \`ReindexDocumentVersionIntegrationTests\` (4, incl. audit outbox + 409 in-flight)
- [ ] FE unit \`useIndexerVersions.test.tsx\` (2)
- [ ] FE unit \`KbReindexDropdown.test.tsx\` (4)
- [ ] \`pnpm typecheck && pnpm lint\` green
- [ ] Smoke manuale su \`/admin/knowledge-base\` per verificare che il default click resti backward-compatible

## Notes
- Strategy effettiva di pipeline NON aggiunta: oggi il registry contiene solo \`Current\`; quando atterrerà una vera divergenza, verrà aggiunta come nuovo entry in \`IndexerVersionRegistry\` + impl strategy separata (la firma del registry è preparata per quel caso).
- Foundation per #1675 (\`goldsetVersion\`) analoga: usa lo stesso pattern code-resident.
- OQ-2 (deprecation policy 18m) seguita: il registry resta entro 2 versioni per ora.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 4: After PR open — local issue tracker update**

```
gh issue comment 1673 --body "PR opened against \`main-dev\` (link sopra). DoD updates:
- [x] BE: IndexerVersion registry + endpoint + persistence
- [x] FE: dropdown + KbDocDetailPanel hero render
- [x] Audit: \`DocumentReindex\` level 2
- [x] Test: unit + integration green
Ready for code review."
```

- [ ] **Step 5: Update memory ledger (post-merge)**

Dopo merge, aggiornare `C:\Users\Utente\.claude\projects\D--Repositories-meepleai-monorepo-dev\memory\MEMORY.md` aggiungendo riga nella sezione "Executed Plans":

```markdown
- `sp5-admin-kb-1673-reindex-version-selector` ✅ PR#<NUMBER> (`<SHA>` <DATE>) — code-resident registry + persistence + 409 in-flight + audit; chiude anche #1676 sub-task F3
```

---

## Self-Review

### Spec coverage check
| Design doc §3.2 requirement | Task |
|-----------------------------|------|
| `IndexerVersion` value object + Registry (D-B code-resident) | Task 1 |
| `pdf_documents.indexer_version` column + backfill | Task 2 |
| `ReindexDocumentCommand` extended with `IndexerVersion` opzionale | Task 3 |
| `[AuditableAction("DocumentReindex", "Document", Level=2)]` | Task 3 |
| Handler resolution chain `explicit → stored → current` | Task 4 |
| Conflict guard su in-flight (Adzic edge case "race") | Task 4 |
| Persistenza versione su entity | Task 4 |
| `GET /admin/indexer/versions` | Task 5 + 6 |
| `POST /admin/pdfs/{id}/reindex` body extension | Task 6 |
| Integration E2E + audit outbox | Task 7 |
| FE dropdown versione | Task 11 + 12 |
| Render `indexerVersion` nel hero (chiude #1676 F3) | Task 12 |

### Placeholder scan
- [x] No "TBD" / "TODO" / "implement later" found.
- [x] No "add appropriate error handling" — explicit ConflictException + NotFoundException specified.
- [x] No "similar to Task N" without code repetition.
- [x] Every step has either code blocks, exact commands, or explicit edit diffs.

### Type consistency
| Symbol | Defined in | Used in |
|--------|-----------|---------|
| `IndexerVersion` (record) | Task 1 | Task 1 (registry) |
| `IndexerVersionRegistry.Current.Version` | Task 1 | Task 4 (resolution), Task 5 (DTO) |
| `IndexerVersionRegistry.TryGet` / `IsSelectable` / `Selectable` | Task 1 | Task 3 (validator), Task 5 (handler) |
| `ReindexDocumentCommand(Guid PdfId, string? IndexerVersion)` | Task 3 | Task 4 (handler), Task 6 (endpoint), Task 7 (integration) |
| `IndexerVersionDto(Version, DisplayName, IsCurrent)` | Task 5 | Task 6 (endpoint), Task 9 (Zod) |
| `IndexerVersion` (Zod / TS) | Task 9 | Task 10 (hook), Task 11 (component) |
| `KbDocumentDto.IndexerVersion` | Task 8 | Task 12 (KbDocDetailPanel render) |
| `KbDocDetailSchema.indexerVersion` | Task 9 | Task 12 (consumer) |
| `useReindexDoc` payload `{ indexerVersion?: string } \| void` | Task 10 | Task 11 (component dispatch) |
| `useIndexerVersions().data` returns `IndexerVersionList` | Task 10 | Task 11 (component) |
| `KbReindexDropdown` props `{ docId, processingStatus }` | Task 11 | Task 12 (wiring) |

Tutti i nomi sono coerenti tra task. Type signatures matched end-to-end.

---

## References
- Design doc consolidato: [`docs/superpowers/specs/2026-06-01-sp5-admin-kb-fu4-spinouts-design.md`](../specs/2026-06-01-sp5-admin-kb-fu4-spinouts-design.md) §3.2
- Issue: [#1673](https://github.com/meepleAi-app/meepleai-monorepo/issues/1673)
- Parent (closed): F3-FU-4 #1653, design `2026-05-29-sp5-admin-kb-f3-fu4-doc-actions-design.md`
- Spec-panel review 2026-06-01 (PR #1790): OQ-2 deprecation policy 18m, D-A/D-B confirmed
- Memoria sessione 2026-06-01: `memory/project_session_2026-06-01_admin_sequence.md`
- Memoria audit admin: `memory/project_sp5_admin_integration_audit.md`
- Pattern audit `[AuditableAction]`: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/DeleteKbDocumentCommand.cs`
- Pattern integration test: `apps/api/tests/Api.Tests/Integration/DocumentProcessing/DeleteKbDocumentCommandHandlerIntegrationTests.cs`
