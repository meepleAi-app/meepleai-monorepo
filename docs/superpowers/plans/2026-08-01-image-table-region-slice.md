# Image-Table Region Grounding — Vertical Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Capture hi_res `Image`/`FigureCaption` regions for one PDF, persist them in a dedicated `pdf_image_regions` table, expose them via `GET /api/v1/pdf/{id}/image-regions`, and draw them in the FE PDF viewer on open — to validate the mechanism + value before the full feature.

**Architecture:** New EF entity/table (BE) + a pure parser that extracts image-region bbox from the raw Unstructured hi_res JSON (which the normal pipeline drops) + a CQRS read query/endpoint + an admin seed command/endpoint + a FE overlay (twin of #3403's `PdfBBoxOverlay`) fetched on PDF open. No router, no async job, no citation linkage (all deferred to #3435).

**Tech Stack:** .NET 9 (ASP.NET Minimal APIs + MediatR CQRS + EF Core/Postgres), Next.js 16 / React 19 (react-pdf, Zod, Vitest).

## Global Constraints

- **Solution**: `apps/api/MeepleAI.Api.sln`. Migration cwd = `apps/api`, command `dotnet ef migrations add <Name> --project src/Api`.
- **CQRS**: endpoints use ONLY `IMediator.Send()` — zero direct service injection in endpoints.
- **Exceptions**: `NotFoundException`(404)/`ConflictException`(409) — never `InvalidOperationException`(500). A missing/unauthorized PDF read returns `null → Results.NotFound` (no info leak), mirroring `GetPdfTextQuery`.
- **Coordinates**: bbox normalized `[0,1]`, **top-left, y-down** (SP-B #3406 / #3403 DA-1). Clamp out-of-range defensively.
- **Naming**: C# PascalCase (public) / `_camelCase` (private); DB columns snake_case via `.HasColumnName`; index names `ix_<table>_<cols>`. TS PascalCase components/types, camelCase functions.
- **Copyright (deferred, documented)**: geometric regions are Full-gated in #3403 (DA-4). The slice's seed data is dev/admin-only + read endpoint is unauthenticated-by-owner like GetPdfText; user-facing rollout MUST add tier gating first. NOT implemented in this slice.
- **Windows dev note**: pre-push BE build can hang on stale `dotnet` DLL-locks — kill `dotnet/testhost/MSBuild` before pushing; docs-only pushes may use `--no-verify` if authorized. Kill testhost before running tests.

---

### Task 1: DB — `pdf_image_regions` entity + config + migration

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Entities/DocumentProcessing/PdfImageRegionEntity.cs`
- Create: `apps/api/src/Api/Infrastructure/EntityConfigurations/DocumentProcessing/PdfImageRegionEntityConfiguration.cs`
- Modify: `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs` (add DbSet ~line 122)
- Create (generated): `apps/api/src/Api/Infrastructure/Migrations/*_AddPdfImageRegions.cs`
- Test: `apps/api/tests/Api.Tests/Unit/DocumentProcessing/PdfImageRegionEntityConfigurationTests.cs`

**Interfaces:**
- Produces: `PdfImageRegionEntity { Guid Id; Guid PdfDocumentId; int PageNumber; double X; double Y; double Width; double Height; string ElementType; DateTime CreatedAt; }` (namespace `Api.Infrastructure.Entities`); `MeepleAiDbContext.PdfImageRegions` DbSet.

- [ ] **Step 1: Write the entity**

`PdfImageRegionEntity.cs`:
```csharp
namespace Api.Infrastructure.Entities;

/// &lt;summary&gt;
/// Image-table region (#3447): a hi_res Image/FigureCaption bbox for a PDF page, persisted so the
/// viewer can highlight table graphics. Normalized [0,1] top-left. See slice spec 2026-08-01.
/// &lt;/summary&gt;
public class PdfImageRegionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PdfDocumentId { get; set; }
    public PdfDocumentEntity PdfDocument { get; set; } = null!;
    public int PageNumber { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string ElementType { get; set; } = "Image";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: Write the Fluent config**

`PdfImageRegionEntityConfiguration.cs`:
```csharp
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

internal class PdfImageRegionEntityConfiguration : IEntityTypeConfiguration&lt;PdfImageRegionEntity&gt;
{
    public void Configure(EntityTypeBuilder&lt;PdfImageRegionEntity&gt; builder)
    {
        builder.ToTable("pdf_image_regions");
        builder.HasKey(e =&gt; e.Id);
        builder.Property(e =&gt; e.PdfDocumentId).HasColumnName("pdf_document_id");
        builder.Property(e =&gt; e.PageNumber).HasColumnName("page_number");
        builder.Property(e =&gt; e.X).HasColumnName("x");
        builder.Property(e =&gt; e.Y).HasColumnName("y");
        builder.Property(e =&gt; e.Width).HasColumnName("width");
        builder.Property(e =&gt; e.Height).HasColumnName("height");
        builder.Property(e =&gt; e.ElementType).HasColumnName("element_type").HasMaxLength(64).IsRequired();
        builder.Property(e =&gt; e.CreatedAt).HasColumnName("created_at");
        builder.HasOne(e =&gt; e.PdfDocument).WithMany()
            .HasForeignKey(e =&gt; e.PdfDocumentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e =&gt; e.PdfDocumentId).HasDatabaseName("ix_pdf_image_regions_pdf_document_id");
    }
}
```

- [ ] **Step 3: Register the DbSet**

In `MeepleAiDbContext.cs`, next to `public DbSet<PdfDocumentEntity> PdfDocuments => Set<PdfDocumentEntity>();`:
```csharp
public DbSet<PdfImageRegionEntity> PdfImageRegions => Set<PdfImageRegionEntity>();
```
(No `OnModelCreating` edit — configs auto-apply via `ApplyConfigurationsFromAssembly`.)

- [ ] **Step 4: Write the failing config test**

`PdfImageRegionEntityConfigurationTests.cs`:
```csharp
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3447")]
public sealed class PdfImageRegionEntityConfigurationTests
{
    [Fact]
    public async Task PdfImageRegions_RoundTrips_WithBboxAndElementType()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"imgregion_{Guid.NewGuid():N}");
        var region = new PdfImageRegionEntity
        {
            PdfDocumentId = Guid.NewGuid(), PageNumber = 4,
            X = 0.10, Y = 0.55, Width = 0.80, Height = 0.30, ElementType = "FigureCaption"
        };
        db.PdfImageRegions.Add(region);
        await db.SaveChangesAsync();

        var loaded = await db.PdfImageRegions.AsNoTracking().SingleAsync();
        loaded.PageNumber.Should().Be(4);
        loaded.ElementType.Should().Be("FigureCaption");
        loaded.X.Should().Be(0.10);
        loaded.Height.Should().Be(0.30);
    }
}
```

- [ ] **Step 5: Run the test — verify it passes** (kill testhost first)

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PdfImageRegionEntityConfigurationTests"`
Expected: PASS (InMemory provider honors the DbSet + config).

- [ ] **Step 6: Generate the migration**

Run (cwd `apps/api`): `dotnet ef migrations add AddPdfImageRegions --project src/Api`
Review the generated `CreateTable("pdf_image_regions", ...)` + FK to `pdf_documents` (Cascade) + index. It must NOT alter existing tables.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/DocumentProcessing/PdfImageRegionEntity.cs \
        apps/api/src/Api/Infrastructure/EntityConfigurations/DocumentProcessing/PdfImageRegionEntityConfiguration.cs \
        apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs \
        apps/api/src/Api/Infrastructure/Migrations/ \
        apps/api/tests/Api.Tests/Unit/DocumentProcessing/PdfImageRegionEntityConfigurationTests.cs
git commit -m "feat(rag): pdf_image_regions entity + migration (#3447)"
```

---

### Task 2: BE — `ImageRegionExtractor` (parse hi_res JSON → regions)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/ImageRegionExtractor.cs`
- Test: `apps/api/tests/Api.Tests/Unit/DocumentProcessing/ImageRegionExtractorTests.cs`

**Interfaces:**
- Produces: `static IReadOnlyList<ExtractedImageRegion> ImageRegionExtractor.FromHiResJson(string? hiResJson)`; `public sealed record ExtractedImageRegion(int Page, double X, double Y, double Width, double Height, string ElementType)` (namespace `Api.BoundedContexts.DocumentProcessing.Application.Services`).
- Rationale: the normal pipeline (`UnstructuredPdfTextExtractor.MapStructuredElements`, line 299) DROPS `Image`/`FigureCaption` (empty text). This parser keeps exactly those, from the raw Python wire format.

- [ ] **Step 1: Write the failing test**

`ImageRegionExtractorTests.cs`:
```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3447")]
public sealed class ImageRegionExtractorTests
{
    private const string HiResJson = """
    {"elements":[
      {"text":"Preparazione","page_number":1,"category":"Title","bbox":{"x":0.08,"y":0.10,"width":0.24,"height":0.05}},
      {"text":"","page_number":4,"category":"Image","bbox":{"x":0.10,"y":0.55,"width":0.80,"height":0.30}},
      {"text":"","page_number":5,"category":"FigureCaption","bbox":{"x":0.12,"y":0.20,"width":0.40,"height":0.06}},
      {"text":"","page_number":6,"category":"Image","bbox":null}
    ]}
    """;

    [Fact]
    public void FromHiResJson_KeepsImageAndFigureCaption_WithBbox_DropsOthers()
    {
        var regions = ImageRegionExtractor.FromHiResJson(HiResJson);

        regions.Should().HaveCount(2); // Image p4 + FigureCaption p5; Title dropped, bbox-null Image dropped
        regions.Should().ContainSingle(r => r.ElementType == "Image" && r.Page == 4 && r.Width == 0.80);
        regions.Should().ContainSingle(r => r.ElementType == "FigureCaption" && r.Page == 5);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json{")]
    [InlineData("{\"elements\":[]}")]
    public void FromHiResJson_NullEmptyInvalidOrNoElements_ReturnsEmpty(string? json)
    {
        ImageRegionExtractor.FromHiResJson(json).Should().BeEmpty();
    }

    [Fact]
    public void FromHiResJson_ClampsBboxToUnitRange()
    {
        var json = """{"elements":[{"text":"","page_number":2,"category":"Image","bbox":{"x":-0.1,"y":0.5,"width":1.5,"height":0.2}}]}""";
        var r = ImageRegionExtractor.FromHiResJson(json).Single();
        r.X.Should().Be(0.0);       // clamped from -0.1
        r.Width.Should().Be(1.0);   // clamped from 1.5
    }
}
```

- [ ] **Step 2: Run it — verify it fails**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ImageRegionExtractorTests"`
Expected: FAIL (does not compile — `ImageRegionExtractor` undefined).

- [ ] **Step 3: Write the implementation**

`ImageRegionExtractor.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// &lt;summary&gt;
/// Image-table slice (#3447): extracts Image/FigureCaption regions (with bbox) from the raw Unstructured
/// hi_res response. These are exactly the elements the normal pipeline drops (empty text), so we parse the
/// wire format directly. Safe on null/empty/invalid input → empty. bbox clamped to [0,1].
/// &lt;/summary&gt;
public static class ImageRegionExtractor
{
    private static readonly HashSet&lt;string&gt; RegionCategories = new(StringComparer.Ordinal) { "Image", "FigureCaption" };

    public static IReadOnlyList&lt;ExtractedImageRegion&gt; FromHiResJson(string? hiResJson)
    {
        if (string.IsNullOrWhiteSpace(hiResJson))
        {
            return Array.Empty&lt;ExtractedImageRegion&gt;();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize&lt;HiResEnvelope&gt;(hiResJson);
            if (parsed?.Elements is null)
            {
                return Array.Empty&lt;ExtractedImageRegion&gt;();
            }

            return parsed.Elements
                .Where(e =&gt; e.Category is not null && RegionCategories.Contains(e.Category) && e.Bbox is not null)
                .Select(e =&gt; new ExtractedImageRegion(
                    Page: e.PageNumber &gt; 0 ? e.PageNumber : 1,
                    X: Clamp01(e.Bbox!.X),
                    Y: Clamp01(e.Bbox!.Y),
                    Width: Clamp01(e.Bbox!.Width),
                    Height: Clamp01(e.Bbox!.Height),
                    ElementType: e.Category!))
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty&lt;ExtractedImageRegion&gt;();
        }
    }

    private static double Clamp01(double v) =&gt; Math.Clamp(v, 0.0, 1.0);

    private sealed record HiResEnvelope([property: JsonPropertyName("elements")] List&lt;HiResElement&gt;? Elements);
    private sealed record HiResElement(
        [property: JsonPropertyName("page_number")] int PageNumber,
        [property: JsonPropertyName("category")] string? Category,
        [property: JsonPropertyName("bbox")] HiResBbox? Bbox);
    private sealed record HiResBbox(
        [property: JsonPropertyName("x")] double X,
        [property: JsonPropertyName("y")] double Y,
        [property: JsonPropertyName("width")] double Width,
        [property: JsonPropertyName("height")] double Height);
}

public sealed record ExtractedImageRegion(int Page, double X, double Y, double Width, double Height, string ElementType);
```

- [ ] **Step 4: Run it — verify it passes**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ImageRegionExtractorTests"`
Expected: PASS (all 6 cases).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/ImageRegionExtractor.cs \
        apps/api/tests/Api.Tests/Unit/DocumentProcessing/ImageRegionExtractorTests.cs
git commit -m "feat(rag): ImageRegionExtractor — parse hi_res Image/FigureCaption regions (#3447)"
```

---

### Task 3: BE — `GetPdfImageRegionsQuery` + handler + GET endpoint

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetPdfImageRegionsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetPdfImageRegionsQueryHandler.cs`
- Modify: `apps/api/src/Api/Routing/Pdf/PdfRetrievalEndpoints.cs` (add `MapGet`)
- Test: `apps/api/tests/Api.Tests/Unit/DocumentProcessing/GetPdfImageRegionsQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `PdfImageRegionEntity`, `MeepleAiDbContext.PdfImageRegions` (Task 1).
- Produces: `internal sealed record GetPdfImageRegionsQuery(Guid PdfId) : IQuery<IReadOnlyList<ImageRegionDto>>`; `internal record ImageRegionDto(int Page, double X, double Y, double Width, double Height, string ElementType)`. Handler returns `[]` when the PDF has no regions. Per-user authz/copyright gating is **deferred** (S-4) — the endpoint only requires a logged-in session (`.RequireSession()`); regions are non-sensitive geometry and an empty list is the natural "no regions" state.

- [ ] **Step 1: Write the failing handler test**

`GetPdfImageRegionsQueryHandlerTests.cs`:
```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3447")]
public sealed class GetPdfImageRegionsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsRegionsForPdf_OrderedByPage()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"getimg_{Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();
        db.PdfImageRegions.AddRange(
            new PdfImageRegionEntity { PdfDocumentId = pdfId, PageNumber = 5, X = 0.1, Y = 0.2, Width = 0.3, Height = 0.1, ElementType = "Image" },
            new PdfImageRegionEntity { PdfDocumentId = pdfId, PageNumber = 4, X = 0.1, Y = 0.5, Width = 0.8, Height = 0.3, ElementType = "FigureCaption" },
            new PdfImageRegionEntity { PdfDocumentId = Guid.NewGuid(), PageNumber = 1, X = 0, Y = 0, Width = 1, Height = 1, ElementType = "Image" });
        await db.SaveChangesAsync();

        var handler = new GetPdfImageRegionsQueryHandler(db, NullLogger<GetPdfImageRegionsQueryHandler>.Instance);
        var result = await handler.Handle(new GetPdfImageRegionsQuery(pdfId), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(r => r.Page).Should().ContainInOrder(4, 5); // ordered by page
        result[0].ElementType.Should().Be("FigureCaption");
    }

    [Fact]
    public async Task Handle_UnknownPdf_ReturnsEmpty()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"getimg_{Guid.NewGuid():N}");
        var handler = new GetPdfImageRegionsQueryHandler(db, NullLogger<GetPdfImageRegionsQueryHandler>.Instance);
        var result = await handler.Handle(new GetPdfImageRegionsQuery(Guid.NewGuid()), CancellationToken.None);
        result.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run it — verify it fails** (compile error: types undefined).

- [ ] **Step 3: Write query + handler**

`GetPdfImageRegionsQuery.cs`:
```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

internal sealed record GetPdfImageRegionsQuery(Guid PdfId) : IQuery&lt;IReadOnlyList&lt;ImageRegionDto&gt;&gt;;

internal record ImageRegionDto(int Page, double X, double Y, double Width, double Height, string ElementType);
```

`GetPdfImageRegionsQueryHandler.cs`:
```csharp
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

internal class GetPdfImageRegionsQueryHandler : IQueryHandler&lt;GetPdfImageRegionsQuery, IReadOnlyList&lt;ImageRegionDto&gt;&gt;
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger&lt;GetPdfImageRegionsQueryHandler&gt; _logger;

    public GetPdfImageRegionsQueryHandler(MeepleAiDbContext dbContext, ILogger&lt;GetPdfImageRegionsQueryHandler&gt; logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task&lt;IReadOnlyList&lt;ImageRegionDto&gt;&gt; Handle(GetPdfImageRegionsQuery query, CancellationToken ct)
    {
        var regions = await _dbContext.PdfImageRegions
            .Where(r =&gt; r.PdfDocumentId == query.PdfId)
            .OrderBy(r =&gt; r.PageNumber)
            .AsNoTracking()
            .Select(r =&gt; new ImageRegionDto(r.PageNumber, r.X, r.Y, r.Width, r.Height, r.ElementType))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return regions;
    }
}
```

- [ ] **Step 4: Run it — verify it passes.**

- [ ] **Step 5: Register the GET endpoint**

In `PdfRetrievalEndpoints.cs`, inside `Map(...)` next to the other `/pdf/{pdfId:guid}/...` routes:
```csharp
group.MapGet("/pdf/{pdfId:guid}/image-regions", HandleGetImageRegions).RequireSession();
```
Add the handler method (mirror `HandleGetPdfText`):
```csharp
private static async Task&lt;IResult&gt; HandleGetImageRegions(Guid pdfId, IMediator mediator, CancellationToken ct)
{
    var regions = await mediator.Send(new GetPdfImageRegionsQuery(pdfId), ct).ConfigureAwait(false);
    return Results.Json(new { regions });
}
```
(`.RequireSession()` enforces a logged-in caller; the handler needs no session data. Add the `using` for `GetPdfImageRegionsQuery`.)

- [ ] **Step 6: Build the solution to confirm the endpoint compiles**

Run: `dotnet build apps/api/MeepleAI.Api.sln` → 0 errors. (Kill stray dotnet first if the build hangs.)

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetPdfImageRegionsQuery.cs \
        apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetPdfImageRegionsQueryHandler.cs \
        apps/api/src/Api/Routing/Pdf/PdfRetrievalEndpoints.cs \
        apps/api/tests/Api.Tests/Unit/DocumentProcessing/GetPdfImageRegionsQueryHandlerTests.cs
git commit -m "feat(rag): GET /api/v1/pdf/{id}/image-regions query + endpoint (#3447)"
```

---

### Task 4: BE — `SeedPdfImageRegionsCommand` + handler + admin POST endpoint

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/SeedPdfImageRegionsCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/SeedPdfImageRegionsCommandHandler.cs`
- Modify: `apps/api/src/Api/Routing/AdminPdfManagementEndpoints.cs` (add admin `MapPost`)
- Test: `apps/api/tests/Api.Tests/Unit/DocumentProcessing/SeedPdfImageRegionsCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `ImageRegionExtractor.FromHiResJson` (Task 2), `PdfImageRegionEntity` + DbSet (Task 1).
- Produces: `internal sealed record SeedPdfImageRegionsCommand(Guid PdfId, string HiResJson) : ICommand<int>` (returns count inserted). Handler is **idempotent**: deletes existing regions for the pdf, inserts the parsed ones, `SaveChangesAsync`.

- [ ] **Step 1: Write the failing handler test**

`SeedPdfImageRegionsCommandHandlerTests.cs`:
```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3447")]
public sealed class SeedPdfImageRegionsCommandHandlerTests
{
    private const string HiResJson = """
    {"elements":[
      {"text":"","page_number":4,"category":"Image","bbox":{"x":0.1,"y":0.5,"width":0.8,"height":0.3}},
      {"text":"t","page_number":1,"category":"Title","bbox":{"x":0.0,"y":0.0,"width":0.1,"height":0.1}}
    ]}
    """;

    [Fact]
    public async Task Handle_InsertsParsedRegions_AndIsIdempotent()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"seedimg_{Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();
        var handler = new SeedPdfImageRegionsCommandHandler(db, NullLogger<SeedPdfImageRegionsCommandHandler>.Instance);

        var count1 = await handler.Handle(new SeedPdfImageRegionsCommand(pdfId, HiResJson), CancellationToken.None);
        count1.Should().Be(1); // only the Image; Title dropped

        // idempotent: seeding again replaces, does not duplicate
        var count2 = await handler.Handle(new SeedPdfImageRegionsCommand(pdfId, HiResJson), CancellationToken.None);
        count2.Should().Be(1);
        (await db.PdfImageRegions.CountAsync(r => r.PdfDocumentId == pdfId)).Should().Be(1);
    }
}
```

- [ ] **Step 2: Run it — verify it fails.**

- [ ] **Step 3: Write command + handler**

`SeedPdfImageRegionsCommand.cs`:
```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

internal sealed record SeedPdfImageRegionsCommand(Guid PdfId, string HiResJson) : ICommand&lt;int&gt;;
```

`SeedPdfImageRegionsCommandHandler.cs`:
```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

internal class SeedPdfImageRegionsCommandHandler : ICommandHandler&lt;SeedPdfImageRegionsCommand, int&gt;
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger&lt;SeedPdfImageRegionsCommandHandler&gt; _logger;

    public SeedPdfImageRegionsCommandHandler(MeepleAiDbContext dbContext, ILogger&lt;SeedPdfImageRegionsCommandHandler&gt; logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task&lt;int&gt; Handle(SeedPdfImageRegionsCommand command, CancellationToken ct)
    {
        var regions = ImageRegionExtractor.FromHiResJson(command.HiResJson);

        var existing = await _dbContext.PdfImageRegions
            .Where(r =&gt; r.PdfDocumentId == command.PdfId).ToListAsync(ct).ConfigureAwait(false);
        if (existing.Count &gt; 0)
        {
            _dbContext.PdfImageRegions.RemoveRange(existing);
        }

        foreach (var r in regions)
        {
            _dbContext.PdfImageRegions.Add(new PdfImageRegionEntity
            {
                PdfDocumentId = command.PdfId, PageNumber = r.Page,
                X = r.X, Y = r.Y, Width = r.Width, Height = r.Height, ElementType = r.ElementType
            });
        }

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("Seeded {Count} image regions for PDF {PdfId}", regions.Count, command.PdfId);
        return regions.Count;
    }
}
```

- [ ] **Step 4: Run it — verify it passes.**

- [ ] **Step 5: Register the admin POST endpoint**

In `AdminPdfManagementEndpoints.cs` (group `/admin/pdfs`, already admin-gated), add:
```csharp
group.MapPost("/{pdfId:guid}/seed-image-regions", SeedImageRegions)
    .WithName("SeedPdfImageRegions")
    .WithSummary("#3447 slice: seed image-table regions from a raw Unstructured hi_res JSON body");
```
Handler + request DTO (co-located at file end, mirror `ReindexDocument`/`ReindexDocumentRequest`):
```csharp
private static async Task&lt;IResult&gt; SeedImageRegions(Guid pdfId, SeedImageRegionsRequest request, IMediator mediator, CancellationToken ct)
{
    var count = await mediator.Send(new SeedPdfImageRegionsCommand(pdfId, request.HiResJson), ct).ConfigureAwait(false);
    return Results.Ok(new { success = true, seeded = count });
}
// ...
internal record SeedImageRegionsRequest(string HiResJson);
```
(Endpoint path = `/api/v1/admin/pdfs/{id}/seed-image-regions`. Add the `using` for the command namespace.)

- [ ] **Step 6: Build the solution → 0 errors.**

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/SeedPdfImageRegionsCommand.cs \
        apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/SeedPdfImageRegionsCommandHandler.cs \
        apps/api/src/Api/Routing/AdminPdfManagementEndpoints.cs \
        apps/api/tests/Api.Tests/Unit/DocumentProcessing/SeedPdfImageRegionsCommandHandlerTests.cs
git commit -m "feat(rag): admin seed-image-regions command + endpoint (#3447)"
```

---

### Task 5: FE — `ImageRegion` schema + `pdfClient.getImageRegions`

**Files:**
- Modify: `apps/web/src/lib/api/schemas/pdf.schemas.ts` (add schema)
- Modify: `apps/web/src/lib/api/clients/pdfClient.ts` (add method)
- Test: `apps/web/src/lib/api/clients/__tests__/pdfClient.test.ts` (add case; create if absent following an existing client test)

**Interfaces:**
- Produces: `ImageRegionsResponseSchema` (zod) + `type ImageRegion = { page:number; x:number; y:number; width:number; height:number; elementType:string }`; `api.pdf.getImageRegions(pdfId: string): Promise<ImageRegion[] | null>`.

- [ ] **Step 1: Add the zod schema**

In `pdf.schemas.ts`:
```ts
export const ImageRegionSchema = z.object({
  page: z.number().int(),
  x: z.number(), y: z.number(), width: z.number(), height: z.number(),
  elementType: z.string(),
});
export const ImageRegionsResponseSchema = z.object({ regions: z.array(ImageRegionSchema) });
export type ImageRegion = z.infer<typeof ImageRegionSchema>;
```
Ensure it's re-exported via `schemas/index.ts` (the barrel already re-exports `pdf.schemas`).

- [ ] **Step 2: Write the failing client test**

In the pdfClient test (mirror how `getProcessingProgress` is tested — mock `httpClient.get`):
```ts
it('getImageRegions calls the endpoint and returns regions', async () => {
  const httpClient = { get: vi.fn().mockResolvedValue({ regions: [{ page: 4, x: 0.1, y: 0.5, width: 0.8, height: 0.3, elementType: 'Image' }] }) };
  const client = createPdfClient({ httpClient: httpClient as any });
  const regions = await client.getImageRegions('abc');
  expect(httpClient.get).toHaveBeenCalledWith('/api/v1/pdf/abc/image-regions', ImageRegionsResponseSchema);
  expect(regions).toEqual([{ page: 4, x: 0.1, y: 0.5, width: 0.8, height: 0.3, elementType: 'Image' }]);
});
```

- [ ] **Step 3: Run it — verify it fails** (`getImageRegions` undefined).

Run: `cd apps/web && pnpm test -- pdfClient`

- [ ] **Step 4: Implement `getImageRegions`**

In `pdfClient.ts` (mirror `getProcessingProgress`; note the endpoint returns `{ regions: [...] }`):
```ts
async getImageRegions(pdfId: string): Promise<ImageRegion[] | null> {
  const res = await httpClient.get(
    `/api/v1/pdf/${encodeURIComponent(pdfId)}/image-regions`,
    ImageRegionsResponseSchema
  );
  return res?.regions ?? null;
}
```
Import `ImageRegionsResponseSchema` + `ImageRegion` from `../schemas`. Add `getImageRegions` to the `PdfClient` type.

- [ ] **Step 5: Run it — verify it passes.**

- [ ] **Step 6: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
git add apps/web/src/lib/api/schemas/pdf.schemas.ts apps/web/src/lib/api/clients/pdfClient.ts apps/web/src/lib/api/clients/__tests__/
git commit -m "feat(rag): pdfClient.getImageRegions + zod schema (#3447)"
```

---

### Task 6: FE — `PdfImageRegionOverlay` component

**Files:**
- Create: `apps/web/src/components/pdf/PdfImageRegionOverlay.tsx`
- Test: `apps/web/src/components/pdf/__tests__/PdfImageRegionOverlay.test.tsx`

**Interfaces:**
- Consumes: `ImageRegion` (Task 5) — but the overlay only needs `{x,y,width,height}`.
- Produces: `PdfImageRegionOverlay({ rects }: { rects: readonly ImageRegion[] })` — draws `%`-based rects (already page-filtered by the caller), `data-testid="pdf-image-region-overlay"` wrapper + `data-testid="pdf-image-region-rect"` per rect, distinct className `pdf-image-region-highlight`. Returns `null` when empty. Mirrors `PdfBBoxOverlay`.

- [ ] **Step 1: Write the failing test** (clone `PdfBBoxOverlay.test.tsx`):
```tsx
import { render, screen } from '@testing-library/react';
import { PdfImageRegionOverlay } from '../PdfImageRegionOverlay';

const rect = (o: Partial<{ x:number;y:number;width:number;height:number }>) =>
  ({ page: 4, x: 0, y: 0, width: 0.1, height: 0.1, elementType: 'Image', ...o });

it('renders one %-positioned rect per region', () => {
  render(<PdfImageRegionOverlay rects={[rect({ x: 0.1, y: 0.2, width: 0.3, height: 0.05 })]} />);
  const el = screen.getByTestId('pdf-image-region-rect');
  expect(el.style.left).toBe('10%');
  expect(el.style.top).toBe('20%');
  expect(el.style.width).toBe('30%');
  expect(el.style.height).toBe('5%');
});

it('renders nothing for empty rects', () => {
  const { container } = render(<PdfImageRegionOverlay rects={[]} />);
  expect(container).toBeEmptyDOMElement();
});
```

- [ ] **Step 2: Run it — verify it fails.** `cd apps/web && pnpm test -- PdfImageRegionOverlay`

- [ ] **Step 3: Implement the overlay:**
```tsx
import type { ImageRegion } from '@/lib/api/schemas';

export interface PdfImageRegionOverlayProps {
  readonly rects: readonly ImageRegion[];
}

/** #3447 slice: draws hi_res table-image regions as %-based rects, child of react-pdf <Page>. */
export function PdfImageRegionOverlay({ rects }: PdfImageRegionOverlayProps): JSX.Element | null {
  if (rects.length === 0) return null;
  return (
    <div aria-hidden data-testid="pdf-image-region-overlay" className="pointer-events-none absolute inset-0">
      {rects.map((r, i) => (
        <div
          key={i}
          data-testid="pdf-image-region-rect"
          className="pdf-image-region-highlight absolute"
          style={{ left: `${r.x * 100}%`, top: `${r.y * 100}%`, width: `${r.width * 100}%`, height: `${r.height * 100}%` }}
        />
      ))}
    </div>
  );
}
```
Add a `.pdf-image-region-highlight` style to `apps/web/src/styles/globals.css` next to `.pdf-bbox-highlight` (dashed border, distinct from the citation highlight), e.g.:
```css
.pdf-image-region-highlight { border: 2px dashed rgb(245 158 11 / 0.9); border-radius: 2px; background: rgb(245 158 11 / 0.08); }
```

- [ ] **Step 4: Run it — verify it passes.**

- [ ] **Step 5: Commit**
```bash
git add apps/web/src/components/pdf/PdfImageRegionOverlay.tsx apps/web/src/components/pdf/__tests__/PdfImageRegionOverlay.test.tsx apps/web/src/styles/globals.css
git commit -m "feat(rag): PdfImageRegionOverlay component (#3447)"
```

---

### Task 7: FE — fetch image-regions on open in `PdfInlineViewer` + draw

**Files:**
- Modify: `apps/web/src/components/pdf/PdfInlineViewer.tsx`
- Test: `apps/web/src/components/pdf/__tests__/PdfInlineViewer.test.tsx` (extend)

**Interfaces:**
- Consumes: `api.pdf.getImageRegions` (Task 5), `PdfImageRegionOverlay` (Task 6).
- Produces: `PdfInlineViewer` now fetches image-regions on `documentId` change, filters to `currentPage`, and renders `<PdfImageRegionOverlay>` as an additional child of `<Page>` (alongside the existing `PdfBBoxOverlay`).

- [ ] **Step 1: Write the failing test** (extend `PdfInlineViewer.test.tsx`; mock adds `getImageRegions`):

Add to the `@/lib/api` mock: `getImageRegions: vi.fn().mockResolvedValue([{ page: 1, x: 0.1, y: 0.2, width: 0.3, height: 0.1, elementType: 'Image' }])`. Then:
```tsx
it('draws image-region overlay for the current page fetched on open', async () => {
  render(<PdfInlineViewer documentId="doc-1" />);
  await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
  await waitFor(() => expect(screen.getByTestId('pdf-image-region-rect')).toBeInTheDocument());
});

it('does not draw image regions for other pages', async () => {
  (api.pdf.getImageRegions as any).mockResolvedValueOnce([{ page: 3, x: 0.1, y: 0.2, width: 0.3, height: 0.1, elementType: 'Image' }]);
  render(<PdfInlineViewer documentId="doc-1" initialPage={1} />);
  await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
  expect(screen.queryByTestId('pdf-image-region-rect')).not.toBeInTheDocument();
});
```

- [ ] **Step 2: Run it — verify it fails.** `cd apps/web && pnpm test -- PdfInlineViewer`

- [ ] **Step 3: Implement fetch-on-open + render**

In `PdfInlineViewer.tsx`:
1. Add state `const [imageRegions, setImageRegions] = useState<readonly ImageRegion[]>([]);`
2. Add an effect keyed on `documentId` (mirror the blob-fetch effect ~lines 125-151):
```tsx
useEffect(() => {
  let cancelled = false;
  api.pdf.getImageRegions(documentId)
    .then((regions) => { if (!cancelled) setImageRegions(regions ?? []); })
    .catch(() => { if (!cancelled) setImageRegions([]); });
  return () => { cancelled = true; };
}, [documentId]);
```
3. `const pageImageRegions = useMemo(() => imageRegions.filter(r => r.page === currentPage), [imageRegions, currentPage]);`
4. Inside `<Page>`, alongside the existing `PdfBBoxOverlay`:
```tsx
{pageImageRegions.length > 0 ? <PdfImageRegionOverlay rects={pageImageRegions} /> : null}
```
Import `api`, `ImageRegion`, `PdfImageRegionOverlay`.

- [ ] **Step 4: Run it — verify it passes.**

- [ ] **Step 5: Typecheck + lint**

Run: `cd apps/web && pnpm typecheck && pnpm lint`. Fix any issues.

- [ ] **Step 6: Commit**
```bash
git add apps/web/src/components/pdf/PdfInlineViewer.tsx apps/web/src/components/pdf/__tests__/PdfInlineViewer.test.tsx
git commit -m "feat(rag): fetch + draw image regions on PDF open (#3447)"
```

---

### Task 8: Validation — seed agricola + visual check (manual)

**Files:** none (validation).

- [ ] **Step 1: Ensure the migration is applied** to the target DB (local `make dev`, or staging after deploy). Locally: `cd apps/api/src/Api && dotnet ef database update`.

- [ ] **Step 2: Get agricola's hi_res regions JSON** from the Unstructured service (as in the #3419 investigation):
```bash
# local (if unstructured up) or staging via SSH docker exec:
docker exec meepleai-api sh -c "curl -s -m 300 -F file=@/path/agricola-revised_rulebook.pdf -F strategy=hi_res -F language=ita http://unstructured-service:8001/api/v1/extract" > /tmp/hires.json
```

- [ ] **Step 3: Find agricola's pdfId** (DB query) and POST the seed (admin session cookie required):
```bash
curl -s -X POST "http://localhost:8080/api/v1/admin/pdfs/<agricola-pdfId>/seed-image-regions" \
  -H "Content-Type: application/json" -b <admin-cookie> \
  -d "{\"hiResJson\": $(cat /tmp/hires.json | jq -Rs .)}"
# expect {"success":true,"seeded":N} with N > 0
```

- [ ] **Step 4: Verify the read endpoint** returns regions:
`GET /api/v1/pdf/<agricola-pdfId>/image-regions` → `{ regions: [...] }` non-empty.

- [ ] **Step 5: Open agricola in the app** (a surface that mounts `PdfInlineViewer`) and **eyeball**: are the dashed rects on the tables? Is it useful? Record the verdict in issue #3447 — this is the decision gate for the full feature.

- [ ] **Step 6 (if not already): run the full affected suites**
- BE: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~DocumentProcessing&Category=Unit"` → 0 fail.
- FE: `cd apps/web && pnpm test -- pdf` → 0 fail.

---

## Deferred (tracked in #3435, NOT in this slice)
Productionized async hi_res enrichment job (trigger without the 120s timeout), the table-heavy PDF router (DC-B), citation→region per-page linkage (DC-F), copyright tier gating on the regions endpoint (prerequisite before user-facing rollout), corpus-scale seeding, and the Metà-C content extraction (VLM/Tesseract).
