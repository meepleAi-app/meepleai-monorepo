# RAG Copyright-Aware KB Cards — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add copyright-tiered references to AI agent chat responses — full verbatim citations for owned/free content, AI-paraphrased for protected publisher content.

**Architecture:** Backend `CopyrightTierResolver` annotates RAG chunks with copyright tier at runtime via a read-only cross-BC projection. System prompt instructs LLM to paraphrase protected content. Frontend `RuleSourceCard` renders per-citation teal (full) or amber (protected) styling.

**Tech Stack:** .NET 9 / EF Core / PostgreSQL / React 19 / TypeScript / SSE streaming

**Spec:** `docs/superpowers/specs/2026-03-21-rag-copyright-kb-cards-design.md`

---

## Task 1: LicenseType Enum + PdfDocument Domain Field

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Enums/LicenseType.cs`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Entities/PdfDocument.cs`
- Modify: `apps/api/src/Api/Infrastructure/Entities/DocumentProcessing/PdfDocumentEntity.cs`
- Test: `tests/Api.Tests/BoundedContexts/DocumentProcessing/Domain/Enums/LicenseTypeTests.cs`

- [ ] **Step 1: Write failing test for LicenseType enum values**

```csharp
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Enums;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public class LicenseTypeTests
{
    [Fact]
    public void LicenseType_Has_Three_Values()
    {
        var values = Enum.GetValues<LicenseType>();
        Assert.Equal(3, values.Length);
    }

    [Fact]
    public void LicenseType_Copyrighted_Is_Default_Zero()
    {
        Assert.Equal(0, (int)LicenseType.Copyrighted);
    }

    [Theory]
    [InlineData(LicenseType.CreativeCommons)]
    [InlineData(LicenseType.PublicDomain)]
    public void LicenseType_IsCopyrightFree_Returns_True_For_Free_Types(LicenseType type)
    {
        Assert.True(type.IsCopyrightFree());
    }

    [Fact]
    public void LicenseType_IsCopyrightFree_Returns_False_For_Copyrighted()
    {
        Assert.False(LicenseType.Copyrighted.IsCopyrightFree());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~LicenseTypeTests" --no-build 2>/dev/null; dotnet test --filter "FullyQualifiedName~LicenseTypeTests"`
Expected: FAIL — `LicenseType` not found

- [ ] **Step 3: Create LicenseType enum**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Enums/LicenseType.cs
namespace Api.BoundedContexts.DocumentProcessing.Domain.Enums;

/// <summary>
/// Copyright license type for PDF documents.
/// Determines how RAG citations are displayed (verbatim vs paraphrased).
/// </summary>
public enum LicenseType
{
    /// <summary>Default — publisher-owned, copyright protected.</summary>
    Copyrighted = 0,

    /// <summary>Creative Commons license — freely usable.</summary>
    CreativeCommons = 1,

    /// <summary>Public domain — no copyright restrictions.</summary>
    PublicDomain = 2
}

public static class LicenseTypeExtensions
{
    /// <summary>Returns true if content can be cited verbatim without ownership check.</summary>
    public static bool IsCopyrightFree(this LicenseType type)
        => type is LicenseType.CreativeCommons or LicenseType.PublicDomain;
}
```

- [ ] **Step 4: Add LicenseType to PdfDocument domain entity**

In `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Entities/PdfDocument.cs`, add property only (no setter — admin reclassification is out of scope):

```csharp
public LicenseType LicenseType { get; private set; } = LicenseType.Copyrighted;
```

- [ ] **Step 5: Add LicenseType to PdfDocumentEntity infrastructure**

In `apps/api/src/Api/Infrastructure/Entities/DocumentProcessing/PdfDocumentEntity.cs`, add:

```csharp
public int LicenseType { get; set; } = 0; // Default: Copyrighted
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~LicenseTypeTests"`
Expected: PASS (4/4)

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Enums/LicenseType.cs apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Entities/PdfDocument.cs apps/api/src/Api/Infrastructure/Entities/DocumentProcessing/PdfDocumentEntity.cs tests/Api.Tests/BoundedContexts/DocumentProcessing/Domain/Enums/LicenseTypeTests.cs
git commit -m "feat(doc-processing): add LicenseType enum and domain field on PdfDocument"
```

---

## Task 2: EF Core Migration — license_type Column

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/<timestamp>_AddLicenseType.cs` (generated)
- Modify: `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs` (EF config if needed)

- [ ] **Step 1: Add EF configuration for LicenseType column**

Check if `PdfDocumentEntity` configuration handles the new `LicenseType` int field. EF Core maps `int` properties by default, so no explicit config is needed. But verify the mapping in the entity configuration file.

- [ ] **Step 2: Generate migration**

Run: `cd apps/api/src/Api && dotnet ef migrations add AddLicenseTypeToPdfDocuments`

- [ ] **Step 3: Review generated SQL**

Verify the migration SQL contains:
```sql
ALTER TABLE pdf_documents ADD COLUMN license_type INTEGER NOT NULL DEFAULT 0;
```

- [ ] **Step 4: Apply migration locally**

Run: `cd apps/api/src/Api && dotnet ef database update`
Expected: Migration applied successfully

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Migrations/
git commit -m "chore(migration): add license_type column to pdf_documents"
```

---

## Task 3: CopyrightTier Enum + ChunkCitation Extension

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Enums/CopyrightTier.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Models/AssembledPrompt.cs`
- Test: `tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Enums/CopyrightTierTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Enums;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public class CopyrightTierTests
{
    [Fact]
    public void CopyrightTier_Has_Two_Values()
    {
        var values = Enum.GetValues<CopyrightTier>();
        Assert.Equal(2, values.Length);
    }

    [Fact]
    public void CopyrightTier_Protected_Is_Default_Safe()
    {
        Assert.Equal(CopyrightTier.Protected, default(CopyrightTier));
    }
}
```

- [ ] **Step 2: Run test — verify fail**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~CopyrightTierTests"`
Expected: FAIL

- [ ] **Step 3: Create CopyrightTier enum**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Enums/CopyrightTier.cs
namespace Api.BoundedContexts.KnowledgeBase.Domain.Enums;

/// <summary>
/// Copyright protection tier for RAG citations.
/// Determines whether citations are shown verbatim (Full) or paraphrased (Protected).
/// </summary>
public enum CopyrightTier
{
    /// <summary>Protected by default — safe fallback. AI paraphrases the content.</summary>
    Protected = 0,

    /// <summary>Full access — verbatim citation with PDF viewer access.</summary>
    Full = 1
}
```

- [ ] **Step 4: Extend ChunkCitation record with backward-compatible defaults**

Modify `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Models/AssembledPrompt.cs`:

```csharp
internal sealed record ChunkCitation(
    string DocumentId,
    int PageNumber,
    float RelevanceScore,
    string SnippetPreview,
    CopyrightTier CopyrightTier = CopyrightTier.Protected,
    string? ParaphrasedSnippet = null,
    bool IsPublic = false);
```

Add using: `using Api.BoundedContexts.KnowledgeBase.Domain.Enums;`

**Note**: `ChunkCitation` is `internal sealed record`. Tests in `Api.Tests` require `[assembly: InternalsVisibleTo("Api.Tests")]` in the `Api` project. Verify this attribute exists (check `Properties/AssemblyInfo.cs` or a `.csproj` `<InternalsVisibleTo>` entry). If absent, add it.

- [ ] **Step 5: Run tests — verify pass + existing tests still pass**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~CopyrightTierTests"`
Then: `cd apps/api/src/Api && dotnet test --filter "BoundedContext=KnowledgeBase" --no-build`
Expected: All pass (existing 4-arg ChunkCitation call sites still compile due to defaults)

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Enums/CopyrightTier.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Models/AssembledPrompt.cs tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Enums/
git commit -m "feat(kb): add CopyrightTier enum and extend ChunkCitation with defaults"
```

---

## Task 4: ICopyrightDataProjection Interface + Infrastructure Implementation

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Projections/ICopyrightDataProjection.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Projections/PdfCopyrightInfo.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Projections/CopyrightDataProjection.cs`
- Test: `tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/Projections/CopyrightDataProjectionTests.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs`

- [ ] **Step 1: Create projection interface and record**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Projections/ICopyrightDataProjection.cs
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Projections;

/// <summary>
/// Read-only projection for copyright tier resolution.
/// Phase 6 pattern: cross-BC data access without coupling to internal repositories.
/// </summary>
public interface ICopyrightDataProjection
{
    Task<IReadOnlyDictionary<string, PdfCopyrightInfo>> GetPdfCopyrightInfoAsync(
        IReadOnlyList<string> documentIds,
        CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, bool>> CheckOwnershipAsync(
        Guid userId,
        IReadOnlyList<Guid> gameIds,
        CancellationToken ct);
}
```

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Projections/PdfCopyrightInfo.cs
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Projections;

public sealed record PdfCopyrightInfo(
    string DocumentId,
    LicenseType LicenseType,
    DocumentCategory DocumentCategory,
    Guid UploadedByUserId,
    Guid? GameId,
    Guid? PrivateGameId,
    bool IsPublic);
```

- [ ] **Step 2: Write integration test for projection**

```csharp
// tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/Projections/CopyrightDataProjectionTests.cs
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Projections;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Projections;

[Trait("Category", "Integration")]
[Trait("BoundedContext", "KnowledgeBase")]
public class CopyrightDataProjectionTests : IClassFixture<TestDatabaseFixture>
{
    private readonly MeepleAiDbContext _db;
    private readonly CopyrightDataProjection _sut;

    public CopyrightDataProjectionTests(TestDatabaseFixture fixture)
    {
        _db = fixture.CreateContext();
        _sut = new CopyrightDataProjection(_db);
    }

    [Fact]
    public async Task GetPdfCopyrightInfoAsync_Returns_Empty_For_Unknown_Ids()
    {
        var result = await _sut.GetPdfCopyrightInfoAsync(
            new[] { "nonexistent-id" }, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task CheckOwnershipAsync_Returns_False_For_Unknown_User()
    {
        var result = await _sut.CheckOwnershipAsync(
            Guid.NewGuid(), new[] { Guid.NewGuid() }, CancellationToken.None);

        Assert.Single(result);
        Assert.False(result.Values.First());
    }
}
```

- [ ] **Step 3: Implement CopyrightDataProjection**

**IMPORTANT**: Before implementing, verify the format of `ChunkCitation.DocumentId` in `RagPromptAssemblyService.cs` — check whether it stores GUIDs with hyphens (`ToString()`) or without (`ToString("N")`). The query below must match that format exactly. The CLAUDE.md anti-pattern note says: "Guid.ToString() for Qdrant pdf_id — use ToString("N")".

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Projections/CopyrightDataProjection.cs
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Projections;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Projections;

internal sealed class CopyrightDataProjection : ICopyrightDataProjection
{
    private readonly MeepleAiDbContext _db;

    public CopyrightDataProjection(MeepleAiDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<string, PdfCopyrightInfo>> GetPdfCopyrightInfoAsync(
        IReadOnlyList<string> documentIds,
        CancellationToken ct)
    {
        // Parse string IDs back to Guids for the DB query
        var guidIds = documentIds
            .Select(id => Guid.TryParse(id, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToList();

        var pdfs = await _db.PdfDocuments
            .AsNoTracking()
            .Where(p => guidIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.LicenseType,
                Category = p.DocumentCategory,
                p.UploadedByUserId,
                p.GameId,
                p.PrivateGameId,
                p.IsPublic
            })
            .ToListAsync(ct);

        // Build lookup keyed by the SAME string format used in ChunkCitation.DocumentId
        // Verify this matches the format in RagPromptAssemblyService
        return pdfs.ToDictionary(
            p => p.Id.ToString(),
            p => new PdfCopyrightInfo(
                p.Id.ToString(),
                Enum.IsDefined(typeof(LicenseType), p.LicenseType)
                    ? (LicenseType)p.LicenseType
                    : LicenseType.Copyrighted,
                Enum.TryParse<DocumentCategory>(p.Category, out var dc)
                    ? dc : DocumentCategory.Other,
                p.UploadedByUserId,
                p.GameId,
                p.PrivateGameId,
                p.IsPublic));
    }

    public async Task<IReadOnlyDictionary<Guid, bool>> CheckOwnershipAsync(
        Guid userId,
        IReadOnlyList<Guid> gameIds,
        CancellationToken ct)
    {
        if (userId == Guid.Empty)
            return gameIds.ToDictionary(id => id, _ => false);

        var ownedGameIds = await _db.UserLibraryEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId
                && e.OwnershipDeclaredAt != null
                && (
                    (e.SharedGameId != null && gameIds.Contains(e.SharedGameId.Value))
                    || (e.PrivateGameId != null && gameIds.Contains(e.PrivateGameId.Value))
                ))
            .Select(e => e.SharedGameId ?? e.PrivateGameId!.Value)
            .ToListAsync(ct);

        var ownedSet = ownedGameIds.ToHashSet();
        return gameIds.ToDictionary(id => id, id => ownedSet.Contains(id));
    }
}
```

- [ ] **Step 4: Register in DI**

In `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs`, in `AddInfrastructureServices`:

```csharp
services.AddScoped<ICopyrightDataProjection, Projections.CopyrightDataProjection>();
```

Add using: `using Api.BoundedContexts.KnowledgeBase.Domain.Projections;`

- [ ] **Step 5: Run tests**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~CopyrightDataProjectionTests"`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Projections/ apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Projections/ apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/ tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/Projections/
git commit -m "feat(kb): add ICopyrightDataProjection with cross-BC read-only projection"
```

---

## Task 5: CopyrightTierResolver Service

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/ICopyrightTierResolver.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/CopyrightTierResolver.cs`
- Test: `tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/CopyrightTierResolverTests.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs`

- [ ] **Step 1: Write comprehensive failing tests**

```csharp
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Projections;
using NSubstitute;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public class CopyrightTierResolverTests
{
    private readonly ICopyrightDataProjection _projection = Substitute.For<ICopyrightDataProjection>();
    private readonly CopyrightTierResolver _sut;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();

    public CopyrightTierResolverTests()
    {
        _sut = new CopyrightTierResolver(_projection);
    }

    private ChunkCitation MakeCitation(string docId = "doc-1", int page = 1)
        => new(docId, page, 0.9f, "some text");

    private PdfCopyrightInfo MakeInfo(
        string docId = "doc-1",
        LicenseType license = LicenseType.Copyrighted,
        DocumentCategory category = DocumentCategory.Rulebook,
        Guid? uploadedBy = null,
        Guid? gameId = null)
        => new(docId, license, category, uploadedBy ?? Guid.NewGuid(), gameId ?? _gameId, null, false);

    [Fact]
    public async Task CreativeCommons_Returns_Full_Regardless_Of_Ownership()
    {
        var citation = MakeCitation();
        var info = MakeInfo(license: LicenseType.CreativeCommons);
        _projection.GetPdfCopyrightInfoAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, PdfCopyrightInfo> { ["doc-1"] = info });

        var result = await _sut.ResolveAsync(new[] { citation }, _userId, CancellationToken.None);

        Assert.Equal(CopyrightTier.Full, result[0].CopyrightTier);
    }

    [Fact]
    public async Task PublicDomain_Returns_Full()
    {
        var citation = MakeCitation();
        var info = MakeInfo(license: LicenseType.PublicDomain);
        _projection.GetPdfCopyrightInfoAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, PdfCopyrightInfo> { ["doc-1"] = info });

        var result = await _sut.ResolveAsync(new[] { citation }, _userId, CancellationToken.None);

        Assert.Equal(CopyrightTier.Full, result[0].CopyrightTier);
    }

    [Theory]
    [InlineData(DocumentCategory.QuickStart)]
    [InlineData(DocumentCategory.Reference)]
    [InlineData(DocumentCategory.PlayerAid)]
    [InlineData(DocumentCategory.Other)]
    public async Task NonProtectedCategory_Returns_Full(DocumentCategory category)
    {
        var citation = MakeCitation();
        var info = MakeInfo(category: category);
        _projection.GetPdfCopyrightInfoAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, PdfCopyrightInfo> { ["doc-1"] = info });

        var result = await _sut.ResolveAsync(new[] { citation }, _userId, CancellationToken.None);

        Assert.Equal(CopyrightTier.Full, result[0].CopyrightTier);
    }

    [Theory]
    [InlineData(DocumentCategory.Rulebook)]
    [InlineData(DocumentCategory.Expansion)]
    [InlineData(DocumentCategory.Errata)]
    public async Task ProtectedCategory_OwnerAndUploader_Returns_Full(DocumentCategory category)
    {
        var citation = MakeCitation();
        var info = MakeInfo(category: category, uploadedBy: _userId, gameId: _gameId);
        _projection.GetPdfCopyrightInfoAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, PdfCopyrightInfo> { ["doc-1"] = info });
        _projection.CheckOwnershipAsync(_userId, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, bool> { [_gameId] = true });

        var result = await _sut.ResolveAsync(new[] { citation }, _userId, CancellationToken.None);

        Assert.Equal(CopyrightTier.Full, result[0].CopyrightTier);
    }

    [Fact]
    public async Task Rulebook_NotOwned_Returns_Protected()
    {
        var citation = MakeCitation();
        var info = MakeInfo(category: DocumentCategory.Rulebook);
        _projection.GetPdfCopyrightInfoAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, PdfCopyrightInfo> { ["doc-1"] = info });
        _projection.CheckOwnershipAsync(_userId, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, bool> { [_gameId] = false });

        var result = await _sut.ResolveAsync(new[] { citation }, _userId, CancellationToken.None);

        Assert.Equal(CopyrightTier.Protected, result[0].CopyrightTier);
    }

    [Fact]
    public async Task Rulebook_OwnedButNotUploader_Returns_Protected()
    {
        var citation = MakeCitation();
        var otherUser = Guid.NewGuid();
        var info = MakeInfo(category: DocumentCategory.Rulebook, uploadedBy: otherUser, gameId: _gameId);
        _projection.GetPdfCopyrightInfoAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, PdfCopyrightInfo> { ["doc-1"] = info });
        _projection.CheckOwnershipAsync(_userId, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, bool> { [_gameId] = true });

        var result = await _sut.ResolveAsync(new[] { citation }, _userId, CancellationToken.None);

        Assert.Equal(CopyrightTier.Protected, result[0].CopyrightTier);
    }

    [Fact]
    public async Task Rulebook_UploaderButNotOwner_Returns_Protected()
    {
        var citation = MakeCitation();
        var info = MakeInfo(category: DocumentCategory.Rulebook, uploadedBy: _userId, gameId: _gameId);
        _projection.GetPdfCopyrightInfoAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, PdfCopyrightInfo> { ["doc-1"] = info });
        _projection.CheckOwnershipAsync(_userId, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, bool> { [_gameId] = false }); // uploaded but NOT owner

        var result = await _sut.ResolveAsync(new[] { citation }, _userId, CancellationToken.None);

        Assert.Equal(CopyrightTier.Protected, result[0].CopyrightTier);
    }

    [Fact]
    public async Task AnonymousUser_Always_Returns_Protected()
    {
        var citation = MakeCitation();
        var info = MakeInfo(category: DocumentCategory.Rulebook);
        _projection.GetPdfCopyrightInfoAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, PdfCopyrightInfo> { ["doc-1"] = info });

        var result = await _sut.ResolveAsync(new[] { citation }, Guid.Empty, CancellationToken.None);

        Assert.Equal(CopyrightTier.Protected, result[0].CopyrightTier);
    }

    [Fact]
    public async Task UnknownDocument_Defaults_To_Protected()
    {
        var citation = MakeCitation("unknown-doc");
        _projection.GetPdfCopyrightInfoAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, PdfCopyrightInfo>());

        var result = await _sut.ResolveAsync(new[] { citation }, _userId, CancellationToken.None);

        Assert.Equal(CopyrightTier.Protected, result[0].CopyrightTier);
    }

    [Fact]
    public async Task MultiGame_Chunks_Resolved_Independently()
    {
        var gameA = Guid.NewGuid();
        var gameB = Guid.NewGuid();
        var citation1 = MakeCitation("doc-a");
        var citation2 = MakeCitation("doc-b");
        var infoA = MakeInfo("doc-a", category: DocumentCategory.Rulebook, uploadedBy: _userId, gameId: gameA);
        var infoB = MakeInfo("doc-b", category: DocumentCategory.Expansion, gameId: gameB);

        _projection.GetPdfCopyrightInfoAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, PdfCopyrightInfo> { ["doc-a"] = infoA, ["doc-b"] = infoB });
        _projection.CheckOwnershipAsync(_userId, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, bool> { [gameA] = true, [gameB] = false });

        var result = await _sut.ResolveAsync(new[] { citation1, citation2 }, _userId, CancellationToken.None);

        Assert.Equal(CopyrightTier.Full, result[0].CopyrightTier);      // owned + uploaded
        Assert.Equal(CopyrightTier.Protected, result[1].CopyrightTier); // not owned
    }
}
```

- [ ] **Step 2: Run tests — verify fail**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~CopyrightTierResolverTests"`
Expected: FAIL — `CopyrightTierResolver` not found

- [ ] **Step 3: Create interface**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/ICopyrightTierResolver.cs
using Api.BoundedContexts.KnowledgeBase.Application.Models;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

public interface ICopyrightTierResolver
{
    Task<IReadOnlyList<ChunkCitation>> ResolveAsync(
        IReadOnlyList<ChunkCitation> citations,
        Guid userId,
        CancellationToken ct);
}
```

- [ ] **Step 4: Implement resolver**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/CopyrightTierResolver.cs
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Projections;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

internal sealed class CopyrightTierResolver : ICopyrightTierResolver
{
    private static readonly HashSet<DocumentCategory> ProtectedCategories = new()
    {
        DocumentCategory.Rulebook,
        DocumentCategory.Expansion,
        DocumentCategory.Errata
    };

    private readonly ICopyrightDataProjection _projection;

    public CopyrightTierResolver(ICopyrightDataProjection projection)
        => _projection = projection;

    public async Task<IReadOnlyList<ChunkCitation>> ResolveAsync(
        IReadOnlyList<ChunkCitation> citations,
        Guid userId,
        CancellationToken ct)
    {
        if (citations.Count == 0) return citations;

        var docIds = citations.Select(c => c.DocumentId).Distinct().ToList();
        var pdfInfos = await _projection.GetPdfCopyrightInfoAsync(docIds, ct);

        // Collect all game IDs for batch ownership check
        var gameIds = pdfInfos.Values
            .Select(p => p.GameId ?? p.PrivateGameId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var ownership = userId != Guid.Empty && gameIds.Count > 0
            ? await _projection.CheckOwnershipAsync(userId, gameIds, ct)
            : new Dictionary<Guid, bool>();

        return citations.Select(c => ResolveSingle(c, userId, pdfInfos, ownership)).ToList();
    }

    private static ChunkCitation ResolveSingle(
        ChunkCitation citation,
        Guid userId,
        IReadOnlyDictionary<string, PdfCopyrightInfo> pdfInfos,
        IReadOnlyDictionary<Guid, bool> ownership)
    {
        // Always propagate IsPublic from projection
        var isPublic = pdfInfos.TryGetValue(citation.DocumentId, out var info) && info.IsPublic;

        if (info is null)
            return citation with { CopyrightTier = CopyrightTier.Protected, IsPublic = isPublic };

        // Rule 1: Copyright-free license
        if (info.LicenseType.IsCopyrightFree())
            return citation with { CopyrightTier = CopyrightTier.Full, IsPublic = isPublic };

        // Rule 2: Non-protected category
        if (!ProtectedCategories.Contains(info.DocumentCategory))
            return citation with { CopyrightTier = CopyrightTier.Full, IsPublic = isPublic };

        // Rule 3: User uploaded AND owns the game (BOTH required)
        var gameId = info.GameId ?? info.PrivateGameId;
        if (gameId.HasValue
            && info.UploadedByUserId == userId
            && ownership.TryGetValue(gameId.Value, out var isOwned)
            && isOwned)
        {
            return citation with { CopyrightTier = CopyrightTier.Full, IsPublic = isPublic };
        }

        return citation with { CopyrightTier = CopyrightTier.Protected, IsPublic = isPublic };
    }
}
```

- [ ] **Step 5: Register in DI**

In `KnowledgeBaseServiceExtensions.cs`, in `AddApplicationServices`:

```csharp
services.AddScoped<ICopyrightTierResolver, CopyrightTierResolver>();
```

- [ ] **Step 6: Run tests**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~CopyrightTierResolverTests"`
Expected: PASS (10/10)

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/ICopyrightTierResolver.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/CopyrightTierResolver.cs tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/CopyrightTierResolverTests.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs
git commit -m "feat(kb): add CopyrightTierResolver with per-chunk tier resolution"
```

---

## Task 6: RagPromptAssemblyService — Copyright Annotation in Prompt

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs`
- Test: `tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyServiceCopyrightTests.cs`

- [ ] **Step 1: Write test for copyright annotation in assembled prompt**

```csharp
namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public class RagPromptAssemblyServiceCopyrightTests
{
    [Fact]
    public void FormatChunkWithCopyright_Protected_Includes_Annotation()
    {
        var chunk = new ChunkCitation("doc-1", 14, 0.92f, "Original text", CopyrightTier.Protected);
        var formatted = RagPromptAssemblyService.FormatChunkForPrompt(chunk, "chunk content here");

        Assert.Contains("Copyright: PROTECTED", formatted);
        Assert.Contains("chunk content here", formatted);
    }

    [Fact]
    public void FormatChunkWithCopyright_Full_Includes_Annotation()
    {
        var chunk = new ChunkCitation("doc-1", 14, 0.92f, "Original text", CopyrightTier.Full);
        var formatted = RagPromptAssemblyService.FormatChunkForPrompt(chunk, "chunk content here");

        Assert.Contains("Copyright: FULL", formatted);
    }

    [Fact]
    public void CopyrightSystemInstruction_Contains_Paraphrase_Directive()
    {
        var instruction = RagPromptAssemblyService.GetCopyrightInstruction("it");

        Assert.Contains("PROTECTED", instruction);
        Assert.Contains("[ref:", instruction);
    }

    [Fact]
    public void CopyrightSystemInstruction_English_Contains_Paraphrase_Directive()
    {
        var instruction = RagPromptAssemblyService.GetCopyrightInstruction("en");

        Assert.Contains("PROTECTED", instruction);
        Assert.Contains("paraphrase", instruction, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 2: Run — verify fail**

- [ ] **Step 3: Add static methods to RagPromptAssemblyService**

In `RagPromptAssemblyService.cs`, add these public static methods (they will be called from the existing prompt assembly flow):

```csharp
public static string FormatChunkForPrompt(ChunkCitation citation, string chunkText)
{
    var tierLabel = citation.CopyrightTier == CopyrightTier.Full ? "FULL" : "PROTECTED";
    return $"[Source: Document {citation.DocumentId}, Page {citation.PageNumber}, Relevance: {citation.RelevanceScore:F2}, Copyright: {tierLabel}]\n{chunkText}\n---";
}

public static string GetCopyrightInstruction(string language)
{
    return language.StartsWith("it", StringComparison.OrdinalIgnoreCase)
        ? "Per le fonti marcate PROTECTED, riformula il contenuto con parole tue senza citare verbatim. Usa il marker [ref:documentId:pageNum] prima di ogni riformulazione. Per le fonti FULL, puoi citare direttamente."
        : "For sources marked PROTECTED, paraphrase in your own words without verbatim citation. Use the marker [ref:documentId:pageNum] before each paraphrase. For FULL sources, you may cite directly.";
}
```

Then integrate into the existing `AssemblePromptAsync` method where chunks are formatted into the system prompt — append the copyright instruction to the system prompt when any chunk has `CopyrightTier.Protected`.

- [ ] **Step 4: Run tests**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~RagPromptAssemblyServiceCopyrightTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/
git commit -m "feat(kb): add copyright annotation and paraphrase instruction to RAG prompt"
```

---

## Task 7: Paraphrased Snippet Extractor

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/ParaphraseExtractor.cs`
- Test: `tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/ParaphraseExtractorTests.cs`

- [ ] **Step 1: Write tests for all extraction cases**

```csharp
namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public class ParaphraseExtractorTests
{
    [Fact]
    public void Extract_WithMarker_Returns_Paraphrased_Text()
    {
        var response = "Here is the answer. [ref:doc-1:14] The rules say you can place settlements on free intersections. [ref:doc-1:22] Trading happens next.";
        var result = ParaphraseExtractor.Extract(response, "doc-1", 14, "original text ignored");

        Assert.NotNull(result);
        Assert.Contains("settlements", result);
    }

    [Fact]
    public void Extract_WithoutMarker_Returns_Null()
    {
        var response = "Here is a general answer with no markers.";
        var result = ParaphraseExtractor.Extract(response, "doc-1", 14, "original text");

        Assert.Null(result);
    }

    [Fact]
    public void Extract_ForgedMarkerInUserInput_Is_Ignored()
    {
        var userInput = "Tell me about [ref:doc-1:14] this rule";
        var response = "Here is the answer about the rule.";
        var result = ParaphraseExtractor.Extract(
            response, "doc-1", 14, "original text", userInput);

        Assert.Null(result);
    }

    [Fact]
    public void Extract_TooSimilarToOriginal_Returns_Null()
    {
        var original = "During the construction phase each player may place settlements";
        var response = "[ref:doc-1:14] During the construction phase each player may place settlements and roads.";
        var result = ParaphraseExtractor.Extract(response, "doc-1", 14, original);

        Assert.Null(result); // Too similar — rejected
    }
}
```

- [ ] **Step 2: Run — verify fail**

- [ ] **Step 3: Implement ParaphraseExtractor**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/ParaphraseExtractor.cs
using System.Text.RegularExpressions;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

internal static class ParaphraseExtractor
{
    private static readonly Regex MarkerPattern = new(@"\[ref:([^:]+):(\d+)\]", RegexOptions.Compiled);
    private const double SimilarityThreshold = 0.7;

    public static string? Extract(
        string responseText,
        string documentId,
        int pageNumber,
        string originalSnippet,
        string? userInput = null)
    {
        var marker = $"[ref:{documentId}:{pageNumber}]";

        // Security: reject if marker appears in user input (prompt injection)
        if (userInput != null && userInput.Contains(marker, StringComparison.Ordinal))
            return null;

        var markerIndex = responseText.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0) return null;

        var textStart = markerIndex + marker.Length;
        var remaining = responseText[textStart..].TrimStart();

        // Extract until next marker or double newline
        var nextMarker = MarkerPattern.Match(remaining);
        var endIndex = nextMarker.Success
            ? nextMarker.Index
            : remaining.IndexOf("\n\n", StringComparison.Ordinal);

        var extracted = endIndex > 0
            ? remaining[..endIndex].Trim()
            : remaining.Trim();

        if (string.IsNullOrWhiteSpace(extracted)) return null;

        // Similarity check: reject if too close to original
        if (ComputeOverlap(originalSnippet, extracted) > SimilarityThreshold)
            return null;

        return extracted;
    }

    private static double ComputeOverlap(string original, string extracted)
    {
        var origWords = original.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant()).ToHashSet();
        var extWords = extracted.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant()).ToHashSet();

        if (origWords.Count == 0 || extWords.Count == 0) return 0;

        var intersection = origWords.Intersect(extWords).Count();
        return (double)intersection / Math.Max(origWords.Count, extWords.Count);
    }
}
```

- [ ] **Step 4: Run tests**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~ParaphraseExtractorTests"`
Expected: PASS (4/4)

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/ParaphraseExtractor.cs tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/ParaphraseExtractorTests.cs
git commit -m "feat(kb): add ParaphraseExtractor with similarity check and injection protection"
```

---

## Task 8: ChatWithSessionAgentCommandHandler Integration

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentCommandHandler.cs`
- Modify: `apps/api/src/Api/Models/Contracts.cs` (StreamingComplete)

- [ ] **Step 1: Add optional Citations field to StreamingComplete**

In `apps/api/src/Api/Models/Contracts.cs`, **append** `Citations` as the LAST parameter. **Do NOT reorder or remove existing fields** — this is a positional record with 8+ call sites.

```csharp
internal record StreamingComplete(
    int estimatedReadingTimeMinutes,
    int promptTokens,
    int completionTokens,
    int totalTokens,
    double? confidence,
    Guid? chatThreadId = null,
    string? routingIntent = null,
    double? routingLatencyMs = null,
    string? strategyTier = null,
    Guid? executionId = null,
    IReadOnlyList<CitationDto>? Citations = null);  // NEW — appended last

internal record CitationDto(
    string DocumentId,
    int PageNumber,
    float RelevanceScore,
    string? SnippetPreview,
    string CopyrightTier,
    string? ParaphrasedSnippet = null,
    bool IsPublic = false);
```

- [ ] **Step 2: Inject ICopyrightTierResolver into handler constructor**

Add `ICopyrightTierResolver copyrightTierResolver` parameter to the handler constructor.

- [ ] **Step 3: Add copyright resolution after RAG retrieval**

After the prompt is assembled and citations are collected, before LLM streaming:

```csharp
// Resolve copyright tiers
var resolvedCitations = await _copyrightTierResolver.ResolveAsync(
    assembledPrompt.Citations, request.UserId, ct);
```

- [ ] **Step 4: Fix CitationsJson persistence gap**

After stream completes, serialize and persist citations:

```csharp
// Extract paraphrased snippets for protected citations
var finalCitations = resolvedCitations.Select(c =>
{
    if (c.CopyrightTier == CopyrightTier.Protected)
    {
        var paraphrase = ParaphraseExtractor.Extract(
            responseText, c.DocumentId, c.PageNumber,
            c.SnippetPreview, request.Message);
        return c with { ParaphrasedSnippet = paraphrase };
    }
    return c;
}).ToList();

var citationsJson = JsonSerializer.Serialize(finalCitations);

thread.AddAssistantMessageWithMetadata(
    content: responseText,
    agentType: typology.Name,
    citationsJson: citationsJson,  // NOW PERSISTED
    tokenCount: totalTokens);
```

- [ ] **Step 5: Include citations in StreamingComplete event**

```csharp
var citationDtos = finalCitations.Select(c => new CitationDto(
    c.DocumentId, c.PageNumber, c.RelevanceScore,
    c.CopyrightTier == CopyrightTier.Full ? c.SnippetPreview : null,
    c.CopyrightTier.ToString().ToLowerInvariant(),
    c.ParaphrasedSnippet,
    c.IsPublic)).ToList();

// Use named parameter to avoid positional confusion with existing 10 fields
yield return new RagStreamingEvent(StreamingEventType.Complete,
    new StreamingComplete(
        estimatedReadingTimeMinutes: ...,
        // ... keep existing field assignments ...
        Citations: citationDtos));
```

- [ ] **Step 6: Run full KB test suite**

Run: `cd apps/api/src/Api && dotnet test --filter "BoundedContext=KnowledgeBase"`
Expected: All pass

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentCommandHandler.cs apps/api/src/Api/Models/Contracts.cs
git commit -m "feat(kb): integrate copyright tier resolution into chat handler pipeline"
```

---

## Task 9: Frontend — Citation Type Extension

**Files:**
- Modify: `apps/web/src/types/domain.ts`
- Modify: `apps/web/src/lib/api/schemas/streaming.schemas.ts`
- Test: `apps/web/src/__tests__/types/citation.test.ts`

- [ ] **Step 1: Write test for extended Citation type**

```typescript
// apps/web/src/__tests__/types/citation.test.ts
import { describe, it, expect } from 'vitest';
import type { Citation } from '@/types/domain';

describe('Citation type', () => {
  it('accepts full tier citation', () => {
    const citation: Citation = {
      documentId: 'doc-1',
      pageNumber: 14,
      snippet: 'Original text',
      relevanceScore: 0.92,
      copyrightTier: 'full',
    };
    expect(citation.copyrightTier).toBe('full');
    expect(citation.paraphrasedSnippet).toBeUndefined();
  });

  it('accepts protected tier citation with paraphrase', () => {
    const citation: Citation = {
      documentId: 'doc-1',
      pageNumber: 14,
      snippet: 'Original text',
      relevanceScore: 0.92,
      copyrightTier: 'protected',
      paraphrasedSnippet: 'Reworded text',
    };
    expect(citation.copyrightTier).toBe('protected');
    expect(citation.paraphrasedSnippet).toBe('Reworded text');
  });
});
```

- [ ] **Step 2: Extend Citation interface in domain.ts**

In `apps/web/src/types/domain.ts`, modify:

```typescript
export interface Citation {
  documentId: string;
  pageNumber: number;
  snippet: string;
  relevanceScore: number;
  // NEW
  copyrightTier: 'full' | 'protected';
  paraphrasedSnippet?: string;
  isPublic?: boolean;
}
```

- [ ] **Step 3: Update streaming schema**

In `apps/web/src/lib/api/schemas/streaming.schemas.ts`, update the `CitationSchema` to include:

```typescript
copyrightTier: z.enum(['full', 'protected']).default('protected'),
paraphrasedSnippet: z.string().optional().nullable(),
isPublic: z.boolean().optional().default(false),
```

And update the `CompleteEventSchema` to include optional `citations` array.

- [ ] **Step 4: Run tests**

Run: `cd apps/web && pnpm test -- --run src/__tests__/types/citation.test.ts`
Expected: PASS

- [ ] **Step 5: Update SSE event consumer**

Find the hook/function that handles the `Complete` SSE event and passes data to chat components. This is likely in a custom hook (e.g., `useAgentChat`, `useChatStream`, or similar in `apps/web/src/hooks/` or `apps/web/src/lib/api/`). Search for `StreamingEventType.Complete` or `type === 'Complete'` in the frontend.

When the `Complete` event contains `citations`, pass them to the chat message state so `RuleSourceCard` receives them. Update the handler:

```typescript
// In the Complete event handler:
if (data.citations) {
  // Store citations with copyright tier info on the message
  updateMessage(threadId, messageId, { citations: data.citations });
}
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/types/domain.ts apps/web/src/lib/api/schemas/streaming.schemas.ts apps/web/src/__tests__/types/
git commit -m "feat(web): extend Citation type with copyrightTier and paraphrasedSnippet"
```

---

## Task 10: Frontend — RuleSourceCard Tier-Aware Rendering

**Files:**
- Modify: `apps/web/src/components/chat-unified/RuleSourceCard.tsx`
- Test: `apps/web/src/components/chat-unified/__tests__/RuleSourceCard.test.tsx`

- [ ] **Step 1: Write tests for both tiers**

```typescript
// apps/web/src/components/chat-unified/__tests__/RuleSourceCard.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { RuleSourceCard } from '../RuleSourceCard';
import type { Citation } from '@/types/domain';

const fullCitation: Citation = {
  documentId: 'doc-1', pageNumber: 14, snippet: 'Original verbatim text',
  relevanceScore: 0.92, copyrightTier: 'full',
};

const protectedCitation: Citation = {
  documentId: 'doc-2', pageNumber: 22, snippet: 'Original text',
  relevanceScore: 0.88, copyrightTier: 'protected',
  paraphrasedSnippet: 'Reworded version of the rule',
};

describe('RuleSourceCard', () => {
  it('renders Full tier with verbatim quote and PDF button', () => {
    render(<RuleSourceCard citations={[fullCitation]} />);
    // Expand
    screen.getByTestId('rule-source-header').click();

    expect(screen.getByText(/Citazione originale/)).toBeInTheDocument();
    expect(screen.getByText(/Original verbatim text/)).toBeInTheDocument();
    expect(screen.getByTestId('view-pdf-btn')).toBeInTheDocument();
  });

  it('renders Protected tier with paraphrased quote and no PDF button', () => {
    render(<RuleSourceCard citations={[protectedCitation]} />);
    screen.getByTestId('rule-source-header').click();

    expect(screen.getByText(/Riformulazione AI/)).toBeInTheDocument();
    expect(screen.getByText(/Reworded version/)).toBeInTheDocument();
    expect(screen.queryByTestId('view-pdf-btn')).not.toBeInTheDocument();
  });

  it('shows lock icon in header when any citation is protected', () => {
    render(<RuleSourceCard citations={[fullCitation, protectedCitation]} />);

    expect(screen.getByTestId('copyright-lock-icon')).toBeInTheDocument();
  });

  it('shows upsell CTA for protected citation', () => {
    render(<RuleSourceCard citations={[protectedCitation]} />);
    screen.getByTestId('rule-source-header').click();

    expect(screen.getByTestId('upsell-cta')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run — verify fail**

Run: `cd apps/web && pnpm test -- --run src/components/chat-unified/__tests__/RuleSourceCard.test.tsx`
Expected: FAIL

- [ ] **Step 3: Implement tier-aware rendering in RuleSourceCard**

Modify `apps/web/src/components/chat-unified/RuleSourceCard.tsx`:

Key changes:
1. **Header**: Add 🔒 icon if `citations.some(c => c.copyrightTier === 'protected')`
2. **CitationChip**: Use teal styling for `full`, amber for `protected`
3. **Quote block**: Render `snippet` (italic) for full, `paraphrasedSnippet` or page-only fallback for protected
4. **Actions**: Show "Vedi nel PDF" only for `full` tier active citation
5. **Upsell CTA**: Show for `protected` tier active citation

The active citation's tier drives the rendering — when user clicks between chips, the quote/actions update.

```tsx
// Key rendering logic inside CollapsibleContent
const activeCitation = citations[activeIndex] ?? citations[0];
const isFull = activeCitation.copyrightTier === 'full';
const hasAnyProtected = citations.some(c => c.copyrightTier === 'protected');

// Quote block
<blockquote
  className={cn(
    'border-l-2 pl-3 py-1 text-sm font-nunito',
    isFull
      ? 'border-l-[hsl(174,60%,40%)] italic text-stone-700 dark:text-stone-300'
      : 'border-l-amber-500 text-stone-700 dark:text-stone-300'
  )}
>
  <p className="text-[9px] uppercase font-bold font-quicksand mb-1"
     style={{ color: isFull ? 'hsl(174, 60%, 40%)' : 'hsl(38, 70%, 38%)' }}>
    {isFull ? 'Citazione originale' : 'Riformulazione AI'}
  </p>
  {isFull ? (
    <p>&ldquo;{activeCitation.snippet}&rdquo;</p>
  ) : activeCitation.paraphrasedSnippet ? (
    <p>{activeCitation.paraphrasedSnippet}</p>
  ) : (
    <p className="not-italic text-stone-500">
      Vedi pagina {activeCitation.pageNumber} del regolamento
    </p>
  )}
</blockquote>

// Actions
{isFull && (
  <button data-testid="view-pdf-btn" onClick={() => setPdfModalOpen(true)} ...>
    Vedi nel PDF
  </button>
)}

{!isFull && (
  <span data-testid="upsell-cta" className="text-xs text-stone-500 flex items-center gap-1">
    <Lock className="h-3 w-3" />
    {activeCitation.isPublic
      ? 'Dichiara possesso per accesso completo'
      : 'Carica il regolamento per la versione completa'}
  </span>
)}
```

- [ ] **Step 4: Run tests**

Run: `cd apps/web && pnpm test -- --run src/components/chat-unified/__tests__/RuleSourceCard.test.tsx`
Expected: PASS

- [ ] **Step 5: Run full frontend test suite**

Run: `cd apps/web && pnpm test`
Expected: All pass

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/chat-unified/RuleSourceCard.tsx apps/web/src/components/chat-unified/__tests__/
git commit -m "feat(web): add copyright tier rendering to RuleSourceCard (teal/amber)"
```

---

## Task 11: Integration Test — Full Pipeline

**Files:**
- Test: `tests/Api.Tests/BoundedContexts/KnowledgeBase/Integration/CopyrightTierPipelineTests.cs`

- [ ] **Step 1: Write integration test covering the full flow**

```csharp
using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Projections;
using NSubstitute;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Integration;

[Trait("Category", "Integration")]
[Trait("BoundedContext", "KnowledgeBase")]
public class CopyrightTierPipelineTests
{
    [Fact]
    public async Task FullPipeline_MixedTiers_ProducesCorrectCitationsJson()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var projection = Substitute.For<ICopyrightDataProjection>();

        var ccInfo = new PdfCopyrightInfo("doc-cc", LicenseType.CreativeCommons,
            DocumentCategory.Rulebook, Guid.NewGuid(), gameId, null, true);
        var protectedInfo = new PdfCopyrightInfo("doc-protected", LicenseType.Copyrighted,
            DocumentCategory.Rulebook, Guid.NewGuid(), gameId, null, false);

        projection.GetPdfCopyrightInfoAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, PdfCopyrightInfo>
            {
                ["doc-cc"] = ccInfo,
                ["doc-protected"] = protectedInfo
            });
        projection.CheckOwnershipAsync(userId, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, bool> { [gameId] = false });

        var resolver = new CopyrightTierResolver(projection);
        var citations = new List<ChunkCitation>
        {
            new("doc-cc", 5, 0.95f, "CC licensed text"),
            new("doc-protected", 14, 0.88f, "Copyrighted rulebook text")
        };

        // Act
        var resolved = await resolver.ResolveAsync(citations, userId, CancellationToken.None);

        // Assert tiers
        Assert.Equal(CopyrightTier.Full, resolved[0].CopyrightTier);
        Assert.Equal(CopyrightTier.Protected, resolved[1].CopyrightTier);
        Assert.True(resolved[0].IsPublic);
        Assert.False(resolved[1].IsPublic);

        // Assert CitationsJson serialization
        var json = JsonSerializer.Serialize(resolved);
        var deserialized = JsonSerializer.Deserialize<List<ChunkCitation>>(json);
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized!.Count);
        Assert.Equal(CopyrightTier.Full, deserialized[0].CopyrightTier);
        Assert.Equal(CopyrightTier.Protected, deserialized[1].CopyrightTier);
    }

    [Fact]
    public void ParaphraseExtraction_WithMarker_PopulatesSnippet()
    {
        var responseText = "The game works like this. [ref:doc-1:14] Players take turns placing tokens on the board. [ref:doc-1:22] Next phase begins.";
        var originalSnippet = "During the construction phase each player may place settlements";

        var result = ParaphraseExtractor.Extract(responseText, "doc-1", 14, originalSnippet);

        Assert.NotNull(result);
        Assert.Contains("Players take turns", result);
    }

    [Fact]
    public void ParaphraseExtraction_FallbackNoMarker_ReturnsNull_NotOriginalText()
    {
        var responseText = "A general answer with no markers.";
        var originalSnippet = "The original copyrighted text that must not leak";

        var result = ParaphraseExtractor.Extract(responseText, "doc-1", 14, originalSnippet);

        Assert.Null(result);
        // Critical: verify no part of original text is returned
    }
}
```

- [ ] **Step 2: Run integration test**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~CopyrightTierPipelineTests"`
Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add tests/Api.Tests/BoundedContexts/KnowledgeBase/Integration/
git commit -m "test(kb): add integration test for copyright tier pipeline"
```

---

## Task 12: Final Verification + Cleanup

- [ ] **Step 1: Run full backend test suite**

Run: `cd apps/api/src/Api && dotnet test`
Expected: All pass

- [ ] **Step 2: Run full frontend test suite**

Run: `cd apps/web && pnpm test && pnpm typecheck && pnpm lint`
Expected: All pass

- [ ] **Step 3: Build both apps**

Run: `cd apps/api/src/Api && dotnet build` and `cd apps/web && pnpm build`
Expected: No errors

- [ ] **Step 4: Final commit with all remaining changes**

```bash
git add -A
git status  # verify no unexpected files
git commit -m "chore: final cleanup for RAG copyright-aware KB cards"
```
