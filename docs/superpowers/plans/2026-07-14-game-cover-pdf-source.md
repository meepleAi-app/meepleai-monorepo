# Cover-da-PDF (SharedGame) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permettere a un utente di impostare la cover di uno SharedGame scegliendo una pagina di un PDF caricato, via proposta con approvazione admin (L4); consolidare la deduplicazione PDF in un servizio unico; chiudere i canali di URL esterni per compliance BGG.

**Architecture:** DocumentProcessing *produce* l'artefatto (render pagina → WebP → R2 con key deterministica). SharedGameCatalog *possiede* la cover pubblica L4 e la scrittura passa dal proposal-system esistente (`ShareRequest` esteso con `ContributionType.CoverChange`). La dedup viene estratta da due handler divergenti in un `IPdfDeduplicationService` unico invocato da tutti i path di ingest.

**Tech Stack:** .NET 9, ASP.NET Minimal APIs + MediatR (CQRS), EF Core + PostgreSQL, xUnit + Moq + FluentAssertions (unit), Testcontainers (integration); Next.js 16 + React 19 + Vitest (FE).

## Global Constraints

- **CQRS**: endpoint usano SOLO `IMediator.Send()` — mai injection diretta di servizi. (#2567)
- **Eccezioni**: `ConflictException` (409), `NotFoundException` (404), `ForbiddenException` (403) — mai `InvalidOperationException` (500). (#2568)
- **DDD**: entity con private setter + factory; value object immutabili; interfacce repository in Domain, implementazione in Infrastructure.
- **DI**: registrare sia `IService` sia implementazione. (#2565)
- **Compliance BGG (#2123 / ADR-059 §5)**: nessun URL esterno arbitrario come cover; solo R2/Wikimedia. Vietato passare host BGG a `<Image>`.
- **Convenzione key cover L4**: `SharedGame.PdfCoverR2Key` memorizza la key **senza** suffisso; l'oggetto fisico R2 sta a `{key}-preview.webp` (il resolver appende `-preview.webp`, `CoverUrlResolver.cs:72`).
- **Materializzazione sincrona**; su fallimento SmolDocling (503/404) l'operazione è rifiutata in modo non-bloccante, nessuno stato "cover a metà".
- **Dedup**: SHA-256 (`ContentHash`); riuso trasparente via `EntityLink`; scope **globale sul catalogo** / **per-utente sui privati**.
- **Test**: `[Trait("Category", TestCategories.Unit)]` (unit), Testcontainers (integration). Kill testhost prima di lanciare i test (#2593).
- **Commit frequenti**, TDD stretto.

---

## File map

**Backend — creati:**
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IPdfDeduplicationService.cs`
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfDeduplicationService.cs`
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IPdfCoverUploadPipeline.cs`
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Services/PdfCoverUploadPipeline.cs`
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/MaterializePdfCoverCommand.cs` (+ Handler)
- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/ProposeCoverChange/ProposeCoverChangeCommand.cs` (+ Handler + Validator)
- EF migration per i nuovi campi `ShareRequest`

**Backend — modificati:**
- `.../DocumentProcessing/Domain/Repositories/IPdfDocumentRepository.cs` (+ `FindByContentHashForUserAsync`)
- `.../DocumentProcessing/Infrastructure/Persistence/PdfDocumentRepository.cs`
- `.../DocumentProcessing/Application/Commands/CompleteChunkedUploadCommandHandler.cs`
- `.../DocumentProcessing/Application/Commands/AddRulebookCommandHandler.cs` (usa il service)
- `.../SharedGameCatalog/Domain/ValueObjects/ContributionType.cs`
- `.../SharedGameCatalog/Domain/Entities/ShareRequest.cs`
- `.../SharedGameCatalog/Domain/Enums/ProposalApprovalAction.cs`
- `.../SharedGameCatalog/Application/Commands/ApproveGameProposal/ApproveGameProposalCommandHandler.cs`
- `apps/api/src/Api/Routing/PrivateGameEndpoints.cs` + `.../UserLibrary/Application/Commands/PrivateGames/AddPrivateGameCommand.cs` (+ Handler)
- DI registration + endpoint routing

**Frontend — creati/modificati:**
- `apps/web/src/components/shared-games/CoverPagePicker.tsx` (wrapper riusabile)
- `apps/web/src/lib/api/clients/sharedGamesClient.ts` (+ endpoint propose-cover)
- test Vitest associati

---

## Task 1: `IPdfDeduplicationService` — regola dedup centralizzata

**Files:**
- Create: `.../DocumentProcessing/Application/Services/IPdfDeduplicationService.cs`
- Create: `.../DocumentProcessing/Application/Services/PdfDeduplicationService.cs`
- Modify: `.../DocumentProcessing/Domain/Repositories/IPdfDocumentRepository.cs`
- Modify: `.../DocumentProcessing/Infrastructure/Persistence/PdfDocumentRepository.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Services/PdfDeduplicationServiceTests.cs`

**Interfaces:**
- Consumes: `IPdfDocumentRepository.FindByContentHashAsync(string, CancellationToken)` (globale), nuovo `FindByContentHashForUserAsync(string, Guid, CancellationToken)`.
- Produces: `IPdfDeduplicationService.EvaluateAsync(string contentHash, Guid? sharedGameId, Guid? privateGameId, Guid userId, CancellationToken)` → `PdfDedupResult(PdfDedupDecision Decision, Guid? ExistingPdfDocumentId, string ContentHash)`; `ComputeContentHashAsync(Stream, CancellationToken)`.

- [ ] **Step 1: Write the failing test**

```csharp
// PdfDeduplicationServiceTests.cs
[Trait("Category", TestCategories.Unit)]
public class PdfDeduplicationServiceTests
{
    private readonly Mock<IPdfDocumentRepository> _repo = new();
    private PdfDeduplicationService Sut() => new(_repo.Object);

    [Fact]
    public async Task Evaluate_CatalogHashKnownAndReady_ReturnsReuseExisting()
    {
        var existing = PdfDocumentTestFactory.Ready(); // helper: state Ready
        _repo.Setup(r => r.FindByContentHashAsync("h", It.IsAny<CancellationToken>()))
             .ReturnsAsync(existing);

        var result = await Sut().EvaluateAsync("h", sharedGameId: Guid.NewGuid(),
            privateGameId: null, userId: Guid.NewGuid(), CancellationToken.None);

        result.Decision.Should().Be(PdfDedupDecision.ReuseExisting);
        result.ExistingPdfDocumentId.Should().Be(existing.Id);
    }

    [Fact]
    public async Task Evaluate_CatalogHashKnownButFailed_ReturnsNewUpload()
    {
        var existing = PdfDocumentTestFactory.Failed();
        _repo.Setup(r => r.FindByContentHashAsync("h", It.IsAny<CancellationToken>()))
             .ReturnsAsync(existing);

        var result = await Sut().EvaluateAsync("h", Guid.NewGuid(), null, Guid.NewGuid(), CancellationToken.None);

        result.Decision.Should().Be(PdfDedupDecision.NewUpload);
    }

    [Fact]
    public async Task Evaluate_PrivateGame_UsesPerUserLookupNotGlobal()
    {
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.FindByContentHashForUserAsync("h", userId, It.IsAny<CancellationToken>()))
             .ReturnsAsync((PdfDocument?)null);

        var result = await Sut().EvaluateAsync("h", sharedGameId: null,
            privateGameId: Guid.NewGuid(), userId: userId, CancellationToken.None);

        result.Decision.Should().Be(PdfDedupDecision.NewUpload);
        _repo.Verify(r => r.FindByContentHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(r => r.FindByContentHashForUserAsync("h", userId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2: Run test → verify FAIL**

Run: `cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~PdfDeduplicationServiceTests"`
Expected: FAIL — `PdfDeduplicationService` / `FindByContentHashForUserAsync` non esistono.

- [ ] **Step 3: Add repo method (interface + impl)**

```csharp
// IPdfDocumentRepository.cs — aggiungere
Task<PdfDocument?> FindByContentHashForUserAsync(string contentHash, Guid userId, CancellationToken cancellationToken = default);
```

```csharp
// PdfDocumentRepository.cs — aggiungere (per-user: isola i PDF privati)
public async Task<PdfDocument?> FindByContentHashForUserAsync(string contentHash, Guid userId, CancellationToken cancellationToken = default)
{
    var entity = await DbContext.PdfDocuments
        .AsNoTracking()
        .Where(p => p.ContentHash == contentHash && p.UploadedByUserId == userId)
        .OrderByDescending(p => p.UploadedAt)
        .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    return entity != null ? MapToDomain(entity) : null;
}
```

- [ ] **Step 4: Implement the service**

```csharp
// IPdfDeduplicationService.cs
public enum PdfDedupDecision { NewUpload, ReuseExisting }
public sealed record PdfDedupResult(PdfDedupDecision Decision, Guid? ExistingPdfDocumentId, string ContentHash);

public interface IPdfDeduplicationService
{
    Task<string> ComputeContentHashAsync(Stream content, CancellationToken cancellationToken);
    Task<PdfDedupResult> EvaluateAsync(string contentHash, Guid? sharedGameId, Guid? privateGameId, Guid userId, CancellationToken cancellationToken);
}
```

```csharp
// PdfDeduplicationService.cs
internal sealed class PdfDeduplicationService : IPdfDeduplicationService
{
    private readonly IPdfDocumentRepository _repo;
    public PdfDeduplicationService(IPdfDocumentRepository repo) => _repo = repo;

    public async Task<string> ComputeContentHashAsync(Stream content, CancellationToken ct)
    {
        var hash = await SHA256.HashDataAsync(content, ct).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    public async Task<PdfDedupResult> EvaluateAsync(string contentHash, Guid? sharedGameId,
        Guid? privateGameId, Guid userId, CancellationToken ct)
    {
        // Catalog (shared): dedup GLOBALE. Private: dedup PER-UTENTE (isolamento).
        var existing = sharedGameId.HasValue
            ? await _repo.FindByContentHashAsync(contentHash, ct).ConfigureAwait(false)
            : await _repo.FindByContentHashForUserAsync(contentHash, userId, ct).ConfigureAwait(false);

        if (existing is null || existing.ProcessingState == PdfProcessingState.Failed)
            return new PdfDedupResult(PdfDedupDecision.NewUpload, null, contentHash);

        return new PdfDedupResult(PdfDedupDecision.ReuseExisting, existing.Id, contentHash);
    }
}
```

- [ ] **Step 5: Register in DI**

In `DocumentProcessingServiceExtensions.cs` accanto agli altri `AddScoped`:
```csharp
services.AddScoped<IPdfDeduplicationService, PdfDeduplicationService>();
```

- [ ] **Step 6: Run test → verify PASS**

Run: `cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~PdfDeduplicationServiceTests"`
Expected: PASS (3 test).

> Nota: se non esiste `PdfDocumentTestFactory`, crearlo nello stesso file test come helper statico che costruisce `PdfDocument` via il factory di dominio, impostando lo stato con i metodi di transizione esistenti (`Ready()` avanza fino a `PdfProcessingState.Ready`, `Failed()` fino a `Failed`).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Services/PdfDeduplicationServiceTests.cs
git commit -m "feat(pdf): centralizza regola dedup in IPdfDeduplicationService"
```

---

## Task 2: Allineare il path chunked al riuso trasparente

**Files:**
- Modify: `.../DocumentProcessing/Application/Commands/CompleteChunkedUploadCommandHandler.cs:112-143`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Commands/CompleteChunkedUploadDedupTests.cs`

**Interfaces:**
- Consumes: `IPdfDeduplicationService.EvaluateAsync(...)` (Task 1), `IMediator` per `CreateEntityLinkCommand`.
- Produces: `CompleteChunkedUploadResult` con `Success: true, DocumentId: existingId` (riuso) invece del rigetto.

- [ ] **Step 1: Write the failing test**

```csharp
[Trait("Category", TestCategories.Unit)]
public class CompleteChunkedUploadDedupTests
{
    [Fact]
    public async Task Complete_HashKnownReady_ReusesExistingInsteadOfRejecting()
    {
        var existingId = Guid.NewGuid();
        var dedup = new Mock<IPdfDeduplicationService>();
        dedup.Setup(d => d.EvaluateAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new PdfDedupResult(PdfDedupDecision.ReuseExisting, existingId, "h"));
        var handler = ChunkedHandlerBuilder.WithDedup(dedup.Object);

        var result = await handler.Handle(ChunkedHandlerBuilder.ValidCommand(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.DocumentId.Should().Be(existingId);
        result.ErrorMessage.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run test → verify FAIL**

Run: `cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~CompleteChunkedUploadDedupTests"`
Expected: FAIL — l'handler oggi ritorna `Success: false, ErrorMessage: DuplicateContentErrorMessage`.

- [ ] **Step 3: Replace the reject block**

Sostituire il blocco `if (contentHash != null) { var isDuplicate = ... AnyAsync ...; if (isDuplicate) { ... return Success:false ... } }` (righe ~112-143) con:

```csharp
if (contentHash != null)
{
    var dedup = await _pdfDeduplicationService
        .EvaluateAsync(contentHash, session.GameId, session.PrivateGameId, session.UserId, cancellationToken)
        .ConfigureAwait(false);

    if (dedup.Decision == PdfDedupDecision.ReuseExisting)
    {
        // Cleanup del file appena assemblato (best effort): non serve, riusiamo l'esistente.
        if (storageResult!.FileId != null)
        {
            try
            {
                await _blobStorageService.DeleteAsync(storageResult.FileId, BlobCategory.Pdf,
                    (session.PrivateGameId ?? session.GameId)?.ToString() ?? string.Empty, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch { /* best effort */ }
        }

        // Riuso trasparente via EntityLink verso il gioco.
        var linkTargetGameId = session.GameId ?? session.PrivateGameId ?? Guid.Empty;
        if (linkTargetGameId != Guid.Empty)
        {
            try
            {
                await _mediator.Send(new CreateEntityLinkCommand(
                    SourceEntityType: MeepleEntityType.Game,
                    SourceEntityId: linkTargetGameId,
                    TargetEntityType: MeepleEntityType.KbCard,
                    TargetEntityId: dedup.ExistingPdfDocumentId!.Value,
                    LinkType: EntityLinkType.RelatedTo,
                    Scope: EntityLinkScope.User,
                    OwnerUserId: session.UserId), cancellationToken).ConfigureAwait(false);
            }
            catch (DuplicateEntityLinkException) { /* idempotent */ }
        }

        return new CompleteChunkedUploadResult(
            Success: true,
            DocumentId: dedup.ExistingPdfDocumentId,
            FileName: sanitizedFileName,
            ErrorMessage: null,
            MissingChunks: null);
    }
}
```

Iniettare `IPdfDeduplicationService _pdfDeduplicationService` e `IMediator _mediator` nel costruttore dell'handler (se `_mediator` non è già presente).

- [ ] **Step 4: Run test → verify PASS**

Run: `cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~CompleteChunkedUploadDedupTests"`
Expected: PASS.

- [ ] **Step 5: Rimuovere la costante morta**

Se `DuplicateContentErrorMessage` (riga 36) non è più referenziata, rimuoverla. Verificare: `cd apps/api && dotnet build`.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/CompleteChunkedUploadCommandHandler.cs apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Commands/CompleteChunkedUploadDedupTests.cs
git commit -m "fix(pdf): il path chunked riusa il PDF duplicato invece di rigettarlo"
```

---

## Task 3: `MaterializePdfCoverCommand` — render pagina → WebP → R2

**Files:**
- Create: `.../DocumentProcessing/Application/Services/IPdfCoverUploadPipeline.cs`
- Create: `.../DocumentProcessing/Infrastructure/Services/PdfCoverUploadPipeline.cs`
- Create: `.../DocumentProcessing/Application/Commands/MaterializePdfCoverCommand.cs` (+ Handler)
- Test: `.../tests/Api.Tests/.../MaterializePdfCoverCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `IMediator` per `GetPdfPageImageQuery(pdfDocumentId, pageNumber)` → `byte[]`; `WebpVariantGenerator.GenerateWebpAsync(byte[], int, int, ct)`; nuovo `IPdfCoverUploadPipeline.UploadAsync(string dbKey, byte[] webp, ct)`.
- Produces: `MaterializePdfCoverCommand(Guid PdfDocumentId, int PageNumber, string DbKey) : ICommand<string>` → ritorna la **dbKey** (senza suffisso); handler chiama `PdfDocument.MarkCoverGenerated(dbKey, pageIndex)`.

- [ ] **Step 1: Write the failing test**

```csharp
[Trait("Category", TestCategories.Unit)]
public class MaterializePdfCoverCommandHandlerTests
{
    [Fact]
    public async Task Handle_RendersPageEncodesWebpUploadsAndMarks()
    {
        var pdfId = Guid.NewGuid();
        var pdf = PdfDocumentTestFactory.Ready(pdfId);
        var repo = new Mock<IPdfDocumentRepository>();
        repo.Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>())).ReturnsAsync(pdf);
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetPdfPageImageQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new byte[] { 0xFF, 0xD8 }); // JPEG magic
        var webp = new Mock<IWebpVariantGenerator>();
        webp.Setup(w => w.GenerateWebpAsync(It.IsAny<byte[]>(), 200, 300, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 0x52, 0x49, 0x46, 0x46 }); // RIFF
        var pipeline = new Mock<IPdfCoverUploadPipeline>();
        pipeline.Setup(p => p.UploadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string k, byte[] _, CancellationToken _) => k);
        var uow = new Mock<IUnitOfWork>();

        var handler = new MaterializePdfCoverCommandHandler(repo.Object, mediator.Object, webp.Object, pipeline.Object, uow.Object);
        var cmd = new MaterializePdfCoverCommand(pdfId, PageNumber: 3, DbKey: "covers/g/pdf-cover");

        var key = await handler.Handle(cmd, CancellationToken.None);

        key.Should().Be("covers/g/pdf-cover");
        pdf.CoverR2Key.Should().Be("covers/g/pdf-cover");
        pdf.CoverPageIndex.Should().Be(3);
        pdf.CoverGenerationStatus.Should().Be(PdfCoverGenerationStatus.Generated);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SmolDoclingFails_ThrowsCoverMaterializationExceptionAndDoesNotMark()
    {
        var pdfId = Guid.NewGuid();
        var pdf = PdfDocumentTestFactory.Ready(pdfId);
        var repo = new Mock<IPdfDocumentRepository>();
        repo.Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>())).ReturnsAsync(pdf);
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetPdfPageImageQuery>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("503"));
        var handler = new MaterializePdfCoverCommandHandler(repo.Object, mediator.Object,
            Mock.Of<IWebpVariantGenerator>(), Mock.Of<IPdfCoverUploadPipeline>(), Mock.Of<IUnitOfWork>());

        var act = () => handler.Handle(new MaterializePdfCoverCommand(pdfId, 3, "k"), CancellationToken.None);

        await act.Should().ThrowAsync<CoverMaterializationException>();
        pdf.CoverGenerationStatus.Should().Be(PdfCoverGenerationStatus.Pending); // non toccato
    }
}
```

- [ ] **Step 2: Run test → verify FAIL**

Run: `cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~MaterializePdfCoverCommandHandlerTests"`
Expected: FAIL — tipi non esistono.

- [ ] **Step 3: Create the upload pipeline** (convenzione suffisso `-preview.webp`)

```csharp
// IPdfCoverUploadPipeline.cs
public interface IPdfCoverUploadPipeline
{
    // Carica il WebP all'oggetto fisico "{dbKey}-preview.webp"; ritorna dbKey (senza suffisso).
    Task<string> UploadAsync(string dbKey, byte[] webpBytes, CancellationToken cancellationToken);
}
```

```csharp
// PdfCoverUploadPipeline.cs — modellato su CoverR2UploadPipeline.cs, suffisso -preview.webp
internal sealed class PdfCoverUploadPipeline : IPdfCoverUploadPipeline
{
    private const string ImmutableCacheControl = "public, max-age=31536000, immutable";
    private const string WebpContentType = "image/webp";
    private readonly IAmazonS3 _s3Client;
    private readonly S3StorageOptions _options;
    private readonly ILogger<PdfCoverUploadPipeline> _logger;

    public PdfCoverUploadPipeline(IAmazonS3 s3Client, S3StorageOptions options, ILogger<PdfCoverUploadPipeline> logger)
    { _s3Client = s3Client; _options = options; _logger = logger; }

    public async Task<string> UploadAsync(string dbKey, byte[] webpBytes, CancellationToken ct)
    {
        if (webpBytes is null || webpBytes.Length == 0)
            throw new ArgumentException("WebP bytes must be non-null and non-empty.", nameof(webpBytes));

        var objectKey = $"{dbKey}-preview.webp"; // il resolver L4 (CoverUrlResolver.cs:72) appende -preview.webp
        using var stream = new MemoryStream(webpBytes, writable: false);
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            InputStream = stream,
            ContentType = WebpContentType,
            AutoCloseStream = false,
            DisablePayloadSigning = true,
        };
        request.Headers.CacheControl = ImmutableCacheControl;
        await _s3Client.PutObjectAsync(request, ct).ConfigureAwait(false);
        return dbKey;
    }
}
```

- [ ] **Step 4: Create the command + handler**

```csharp
// MaterializePdfCoverCommand.cs
public sealed record MaterializePdfCoverCommand(Guid PdfDocumentId, int PageNumber, string DbKey) : ICommand<string>;

public sealed class CoverMaterializationException : Exception
{
    public CoverMaterializationException(string message, Exception inner) : base(message, inner) { }
}

internal sealed class MaterializePdfCoverCommandHandler : ICommandHandler<MaterializePdfCoverCommand, string>
{
    private readonly IPdfDocumentRepository _repo;
    private readonly IMediator _mediator;
    private readonly IWebpVariantGenerator _webp;
    private readonly IPdfCoverUploadPipeline _pipeline;
    private readonly IUnitOfWork _uow;

    public MaterializePdfCoverCommandHandler(IPdfDocumentRepository repo, IMediator mediator,
        IWebpVariantGenerator webp, IPdfCoverUploadPipeline pipeline, IUnitOfWork uow)
    { _repo = repo; _mediator = mediator; _webp = webp; _pipeline = pipeline; _uow = uow; }

    public async Task<string> Handle(MaterializePdfCoverCommand cmd, CancellationToken ct)
    {
        var pdf = await _repo.GetByIdAsync(cmd.PdfDocumentId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException($"PdfDocument {cmd.PdfDocumentId} not found.");

        byte[] jpeg;
        try
        {
            jpeg = await _mediator.Send(new GetPdfPageImageQuery(cmd.PdfDocumentId, cmd.PageNumber), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // SmolDocling down/404: non-bloccante, nessuno stato "a metà".
            throw new CoverMaterializationException("Rendering pagina PDF non disponibile.", ex);
        }

        var webpBytes = await _webp.GenerateWebpAsync(jpeg, 200, 300, ct).ConfigureAwait(false);
        var dbKey = await _pipeline.UploadAsync(cmd.DbKey, webpBytes, ct).ConfigureAwait(false);

        pdf.MarkCoverGenerated(dbKey, cmd.PageNumber);
        await _repo.UpdateAsync(pdf, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        return dbKey;
    }
}
```

> `WebpVariantGenerator` è oggi `internal sealed` senza interfaccia: in questo step estrai `IWebpVariantGenerator` (metodo `GenerateWebpAsync(byte[], int, int, CancellationToken)`), fallo implementare alla classe esistente, e registralo in DI. Se `_repo.UpdateAsync`/`GetByIdAsync` non hanno la firma attesa, adegua al repository reale (verifica `IPdfDocumentRepository`).

- [ ] **Step 5: Register DI**

```csharp
services.AddScoped<IPdfCoverUploadPipeline, PdfCoverUploadPipeline>();
services.AddScoped<IWebpVariantGenerator, WebpVariantGenerator>();
services.AddScoped<MaterializePdfCoverCommandHandler>(); // se non auto-registrato via MediatR scan
```

- [ ] **Step 6: Run test → verify PASS**

Run: `cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~MaterializePdfCoverCommandHandlerTests"`
Expected: PASS (2 test).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing apps/api/tests/Api.Tests
git commit -m "feat(pdf): MaterializePdfCover render pagina -> WebP -> R2 (suffisso -preview.webp)"
```

---

## Task 4: `ContributionType.CoverChange` + campi pending su ShareRequest + migration

**Files:**
- Modify: `.../SharedGameCatalog/Domain/ValueObjects/ContributionType.cs`
- Modify: `.../SharedGameCatalog/Domain/Entities/ShareRequest.cs` (campi + validazione `Create`)
- Modify: `.../Infrastructure/Configurations/SharedGameCatalog/ShareRequestEntityConfiguration.cs`
- Create: EF migration
- Test: `.../tests/Api.Tests/.../ShareRequestCoverChangeTests.cs`

**Interfaces:**
- Produces: `ShareRequest.CreateCoverChange(Guid userId, Guid targetSharedGameId, Guid sourcePdfDocumentId, string pendingCoverR2Key, int coverPageIndex, string? userNotes)`; nuove proprietà `PendingCoverR2Key`, `CoverPageIndex`, `SourcePdfDocumentId`.

- [ ] **Step 1: Write the failing test**

```csharp
[Trait("Category", "Unit")]
public sealed class ShareRequestCoverChangeTests
{
    [Fact]
    public void CreateCoverChange_WithValidData_SetsCoverChangeType()
    {
        var target = Guid.NewGuid();
        var pdf = Guid.NewGuid();
        var req = ShareRequest.CreateCoverChange(
            userId: Guid.NewGuid(), targetSharedGameId: target, sourcePdfDocumentId: pdf,
            pendingCoverR2Key: "covers/g/pdf-cover", coverPageIndex: 3, userNotes: null);

        req.ContributionType.Should().Be(ContributionType.CoverChange);
        req.TargetSharedGameId.Should().Be(target);
        req.PendingCoverR2Key.Should().Be("covers/g/pdf-cover");
        req.CoverPageIndex.Should().Be(3);
        req.SourcePdfDocumentId.Should().Be(pdf);
        req.Status.Should().Be(ShareRequestStatus.Pending);
    }

    [Fact]
    public void CreateCoverChange_WithEmptyPendingKey_Throws()
    {
        var act = () => ShareRequest.CreateCoverChange(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "  ", 0, null);
        act.Should().Throw<ArgumentException>();
    }
}
```

- [ ] **Step 2: Run test → verify FAIL**

Run: `cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~ShareRequestCoverChangeTests"`
Expected: FAIL — `ContributionType.CoverChange` / `CreateCoverChange` non esistono.

- [ ] **Step 3: Extend enum + entity**

```csharp
// ContributionType.cs
public enum ContributionType
{
    NewGame = 0,
    AdditionalContent = 1,
    NewGameProposal = 2,
    CoverChange = 3,
}
```

```csharp
// ShareRequest.cs — nuove proprietà (private setter)
public string? PendingCoverR2Key { get; private set; }
public int? CoverPageIndex { get; private set; }
public Guid? SourcePdfDocumentId { get; private set; }

// factory dedicato
public static ShareRequest CreateCoverChange(Guid userId, Guid targetSharedGameId,
    Guid sourcePdfDocumentId, string pendingCoverR2Key, int coverPageIndex, string? userNotes = null)
{
    if (targetSharedGameId == Guid.Empty) throw new ArgumentException("TargetSharedGameId required", nameof(targetSharedGameId));
    if (string.IsNullOrWhiteSpace(pendingCoverR2Key)) throw new ArgumentException("Pending cover key required", nameof(pendingCoverR2Key));
    if (coverPageIndex < 0) throw new ArgumentException("Page index must be non-negative", nameof(coverPageIndex));

    var req = Create(userId, sourceGameId: targetSharedGameId, ContributionType.CoverChange, userNotes, targetSharedGameId);
    req.PendingCoverR2Key = pendingCoverR2Key;
    req.CoverPageIndex = coverPageIndex;
    req.SourcePdfDocumentId = sourcePdfDocumentId;
    return req;
}
```

- [ ] **Step 4: EF configuration + migration**

Aggiungere in `ShareRequestEntityConfiguration.cs`:
```csharp
builder.Property(x => x.PendingCoverR2Key).HasMaxLength(512);
builder.Property(x => x.CoverPageIndex);
builder.Property(x => x.SourcePdfDocumentId);
```

Run:
```bash
cd apps/api/src/Api && dotnet ef migrations add AddShareRequestCoverChangeFields
```
Review dello SQL generato (solo `ADD COLUMN` nullable, nessun drop).

- [ ] **Step 5: Run test + build → verify PASS**

Run: `cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~ShareRequestCoverChangeTests" && dotnet build`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog apps/api/src/Api/Infrastructure/Configurations apps/api/src/Api/Infrastructure/Migrations apps/api/tests/Api.Tests
git commit -m "feat(catalog): ContributionType.CoverChange + campi pending cover su ShareRequest"
```

---

## Task 5: Approvazione `UpdateCover` → promuove pending → L4

**Files:**
- Modify: `.../SharedGameCatalog/Domain/Enums/ProposalApprovalAction.cs`
- Modify: `.../Application/Commands/ApproveGameProposal/ApproveGameProposalCommandHandler.cs`
- Test: `.../tests/Api.Tests/.../ApproveCoverChangeTests.cs`

**Interfaces:**
- Consumes: `ShareRequest.PendingCoverR2Key`, `SharedGame.SetPdfCoverR2Key(string)`, `ShareRequest.Approve(adminId, targetSharedGameId, feedback)`.
- Produces: nuovo `ProposalApprovalAction.UpdateCover`; alla sua gestione, `SharedGame.PdfCoverR2Key` = `shareRequest.PendingCoverR2Key`.

- [ ] **Step 1: Write the failing test**

```csharp
[Trait("Category", TestCategories.Unit)]
public class ApproveCoverChangeTests
{
    [Fact]
    public async Task Approve_UpdateCover_SetsL4KeyOnSharedGameAndApproves()
    {
        var target = Guid.NewGuid();
        var sharedGame = SharedGameTestFactory.Existing(target);
        var req = ShareRequest.CreateCoverChange(Guid.NewGuid(), target, Guid.NewGuid(), "covers/g/pdf-cover", 3, null);
        var (handler, repos) = ApproveHandlerBuilder.For(req, sharedGame);

        var cmd = new ApproveGameProposalCommand(ShareRequestId: req.Id, AdminId: Guid.NewGuid(),
            ApprovalAction: ProposalApprovalAction.UpdateCover, TargetSharedGameId: target, AdminNotes: null);

        await handler.Handle(cmd, CancellationToken.None);

        sharedGame.PdfCoverR2Key.Should().Be("covers/g/pdf-cover");
        req.Status.Should().Be(ShareRequestStatus.Approved);
    }
}
```

- [ ] **Step 2: Run test → verify FAIL**

Run: `cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~ApproveCoverChangeTests"`
Expected: FAIL — `ProposalApprovalAction.UpdateCover` non esiste.

- [ ] **Step 3: Extend enum + handler switch**

```csharp
// ProposalApprovalAction.cs — aggiungere
UpdateCover,
```

Nel `switch` di `ApproveGameProposalCommandHandler.Handle` (righe ~104-141), aggiungere il caso:
```csharp
ProposalApprovalAction.UpdateCover => await ApproveCoverChangeAsync(shareRequest, command, cancellationToken),
```

E il metodo privato:
```csharp
private async Task<Guid> ApproveCoverChangeAsync(ShareRequest shareRequest, ApproveGameProposalCommand command, CancellationToken ct)
{
    var targetId = command.TargetSharedGameId ?? shareRequest.TargetSharedGameId
        ?? throw new ConflictException("CoverChange senza target shared game.");
    var sharedGame = await _sharedGameRepository.GetByIdAsync(targetId, ct).ConfigureAwait(false)
        ?? throw new NotFoundException($"SharedGame {targetId} not found.");

    if (string.IsNullOrWhiteSpace(shareRequest.PendingCoverR2Key))
        throw new ConflictException("Pending cover key mancante sulla proposal.");

    sharedGame.SetPdfCoverR2Key(shareRequest.PendingCoverR2Key);
    _sharedGameRepository.Update(sharedGame);
    return targetId;
}
```

(la chiamata a `shareRequest.Approve(...)` + `SaveChangesAsync` avviene già a valle nello switch, righe ~133-137, riusando quel path.)

- [ ] **Step 4: Run test → verify PASS**

Run: `cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~ApproveCoverChangeTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog apps/api/tests/Api.Tests
git commit -m "feat(catalog): approvazione UpdateCover promuove la cover pending a L4"
```

---

## Task 6: `ProposeCoverChangeCommand` + endpoint utente (materializza + crea proposal)

**Files:**
- Create: `.../SharedGameCatalog/Application/Commands/ProposeCoverChange/ProposeCoverChangeCommand.cs` (+ Handler + Validator)
- Modify: routing (nuovo endpoint `POST /api/v1/games/{gameId}/cover/propose-from-pdf`)
- Test: `.../tests/Api.Tests/Integration/.../ProposeCoverChangeIntegrationTests.cs`

**Interfaces:**
- Consumes: `MaterializePdfCoverCommand` (Task 3), `ShareRequest.CreateCoverChange` (Task 4), `IShareRequestRepository`.
- Produces: `ProposeCoverChangeCommand(Guid UserId, Guid SharedGameId, Guid PdfDocumentId, int PageNumber) : ICommand<Guid>` → ritorna `shareRequestId`.

- [ ] **Step 1: Write the failing integration test**

```csharp
[Trait("Category", TestCategories.Integration)]
[Collection("Integration")]
public class ProposeCoverChangeIntegrationTests
{
    // Usa il fixture Testcontainers Postgres del progetto.
    [Fact]
    public async Task Propose_MaterializesPendingCoverAndCreatesPendingShareRequest()
    {
        var (mediator, ctx, seed) = await ArrangeAsync(); // seed: SharedGame + PdfDocument Ready con SharedGameId
        var cmd = new ProposeCoverChangeCommand(seed.UserId, seed.SharedGameId, seed.PdfDocumentId, PageNumber: 2);

        var shareRequestId = await mediator.Send(cmd, CancellationToken.None);

        var sr = await ctx.ShareRequests.FindAsync(shareRequestId);
        sr!.ContributionType.Should().Be(ContributionType.CoverChange);
        sr.Status.Should().Be(ShareRequestStatus.Pending);
        sr.PendingCoverR2Key.Should().NotBeNullOrWhiteSpace();
        sr.CoverPageIndex.Should().Be(2);
    }
}
```

- [ ] **Step 2: Run test → verify FAIL**

Run: `cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~ProposeCoverChangeIntegrationTests"`
Expected: FAIL — `ProposeCoverChangeCommand` non esiste. (Richiede Docker per Testcontainers.)

- [ ] **Step 3: Command + Validator + Handler**

```csharp
// ProposeCoverChangeCommand.cs
public sealed record ProposeCoverChangeCommand(Guid UserId, Guid SharedGameId, Guid PdfDocumentId, int PageNumber) : ICommand<Guid>;

public sealed class ProposeCoverChangeCommandValidator : AbstractValidator<ProposeCoverChangeCommand>
{
    public ProposeCoverChangeCommandValidator()
    {
        RuleFor(x => x.SharedGameId).NotEmpty();
        RuleFor(x => x.PdfDocumentId).NotEmpty();
        RuleFor(x => x.PageNumber).GreaterThan(0);
    }
}

internal sealed class ProposeCoverChangeCommandHandler : ICommandHandler<ProposeCoverChangeCommand, Guid>
{
    private readonly IMediator _mediator;
    private readonly IShareRequestRepository _shareRequests;
    private readonly IUnitOfWork _uow;

    public ProposeCoverChangeCommandHandler(IMediator mediator, IShareRequestRepository shareRequests, IUnitOfWork uow)
    { _mediator = mediator; _shareRequests = shareRequests; _uow = uow; }

    public async Task<Guid> Handle(ProposeCoverChangeCommand cmd, CancellationToken ct)
    {
        // dbKey deterministica per la pending cover della proposal
        var dbKey = $"covers/{cmd.SharedGameId:D}/pdf-cover-pending";
        var pendingKey = await _mediator.Send(
            new MaterializePdfCoverCommand(cmd.PdfDocumentId, cmd.PageNumber, dbKey), ct).ConfigureAwait(false);

        var req = ShareRequest.CreateCoverChange(cmd.UserId, cmd.SharedGameId, cmd.PdfDocumentId, pendingKey, cmd.PageNumber);
        await _shareRequests.AddAsync(req, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
        return req.Id;
    }
}
```

- [ ] **Step 4: Endpoint (CQRS, solo IMediator)**

Nel routing degli SharedGame (seguire il pattern degli endpoint esistenti):
```csharp
app.MapPost("/api/v1/games/{gameId:guid}/cover/propose-from-pdf",
    async (Guid gameId, ProposeCoverFromPdfRequest body, HttpContext http, IMediator m, CancellationToken ct) =>
    {
        var userId = http.GetUserId(); // helper esistente per l'utente autenticato
        var id = await m.Send(new ProposeCoverChangeCommand(userId, gameId, body.PdfDocumentId, body.PageNumber), ct);
        return Results.Ok(new { shareRequestId = id });
    }).RequireAuthorization();

internal record ProposeCoverFromPdfRequest(Guid PdfDocumentId, int PageNumber);
```

- [ ] **Step 5: Run test → verify PASS**

Kill testhost, poi:
Run: `cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~ProposeCoverChangeIntegrationTests"`
Expected: PASS (richiede Docker attivo).

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog apps/api/src/Api/Routing apps/api/tests/Api.Tests
git commit -m "feat(catalog): endpoint utente propone cover-da-PDF (materializza + ShareRequest CoverChange)"
```

---

## Task 7: Chiudere i canali URL esterni (`ImageUrl`/`ThumbnailUrl`)

**Files:**
- Modify: `apps/api/src/Api/Routing/PrivateGameEndpoints.cs` (`AddPrivateGameRequest:442-455`)
- Modify: `.../UserLibrary/Application/Commands/PrivateGames/AddPrivateGameCommand.cs` + Handler
- Test: `.../tests/Api.Tests/.../AddPrivateGameNoExternalUrlTests.cs`

**Interfaces:**
- Produces: `AddPrivateGameRequest` e `AddPrivateGameCommand` **senza** `ImageUrl`/`ThumbnailUrl`; il DTO risultante non espone più URL esterni impostati dall'utente.

- [ ] **Step 1: Write the failing test**

```csharp
[Trait("Category", TestCategories.Unit)]
public class AddPrivateGameNoExternalUrlTests
{
    [Fact]
    public void AddPrivateGameCommand_HasNoExternalUrlFields()
    {
        var props = typeof(AddPrivateGameCommand).GetProperties().Select(p => p.Name).ToArray();
        props.Should().NotContain("ImageUrl");
        props.Should().NotContain("ThumbnailUrl");
    }
}
```

- [ ] **Step 2: Run test → verify FAIL**

Run: `cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~AddPrivateGameNoExternalUrlTests"`
Expected: FAIL — i campi esistono ancora.

- [ ] **Step 3: Remove the fields**

- In `AddPrivateGameRequest` (`PrivateGameEndpoints.cs:453-454`) rimuovere `string? ImageUrl = null` e `string? ThumbnailUrl = null`.
- In `AddPrivateGameCommand` rimuovere gli stessi due parametri.
- Nel handler / mapping (`AddPrivateGameCommandHandler` `MapToDto:149-171`) rimuovere il pass-through `ImageUrl:`/`ThumbnailUrl:` — impostare la cover del `PrivateGame` a null (placeholder) alla creazione; la cover-da-PDF si imposta poi via il flusso di materializzazione.
- Aggiornare il costruttore dell'endpoint che mappava request→command.

> Il campo di dominio `PrivateGame.ImageUrl` NON viene rimosso qui (evita migration distruttiva); smette solo di essere popolato da input utente esterno. La sua valorizzazione via cover-da-PDF è un follow-up (vedi Coverage).

- [ ] **Step 4: Run test + build → verify PASS**

Run: `cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~AddPrivateGameNoExternalUrlTests" && dotnet build`
Expected: PASS. Correggere eventuali call-site che passavano i due campi.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Routing/PrivateGameEndpoints.cs apps/api/src/Api/BoundedContexts/UserLibrary apps/api/tests/Api.Tests
git commit -m "fix(compliance): rimuove ImageUrl/ThumbnailUrl esterni da AddPrivateGame (freeze BGG #2123)"
```

---

## Task 8: FE `CoverPagePicker` + client endpoint + due ingressi

**Files:**
- Create: `apps/web/src/components/shared-games/CoverPagePicker.tsx`
- Modify: `apps/web/src/lib/api/clients/sharedGamesClient.ts` (+ `proposeCoverFromPdf`)
- Test: `apps/web/src/components/shared-games/__tests__/CoverPagePicker.test.tsx`

**Interfaces:**
- Consumes: `CoverImagePicker` (componente presentazionale esistente, props `{ pdfDocumentId, value, onChange }`), endpoint `POST /api/v1/games/{gameId}/cover/propose-from-pdf`.
- Produces: `<CoverPagePicker gameId pdfDocumentId onProposed />` — al conferma chiama `proposeCoverFromPdf` e invoca `onProposed(shareRequestId)`.

- [ ] **Step 1: Add the client method**

```typescript
// sharedGamesClient.ts
async proposeCoverFromPdf(gameId: string, pdfDocumentId: string, pageNumber: number): Promise<{ shareRequestId: string }> {
  const Schema = z.object({ shareRequestId: z.string() });
  const result = await httpClient.post<z.infer<typeof Schema>>(
    `/api/v1/games/${encodeURIComponent(gameId)}/cover/propose-from-pdf`,
    { pdfDocumentId, pageNumber },
    Schema
  );
  return result!;
}
```

- [ ] **Step 2: Write the failing test**

```tsx
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';
import { CoverPagePicker } from '../CoverPagePicker';

const proposeMock = vi.fn();
vi.mock('@/lib/api/clients/sharedGamesClient', () => ({
  api: { sharedGames: {
    getPdfPageImageUrl: (id: string, p: number) => `/img/${id}/${p}`,
    proposeCoverFromPdf: (...a: unknown[]) => proposeMock(...a),
  } },
}));

beforeEach(() => { vi.clearAllMocks(); proposeMock.mockResolvedValue({ shareRequestId: 'sr-1' }); });

describe('CoverPagePicker', () => {
  it('proposes the selected page and calls onProposed', async () => {
    const onProposed = vi.fn();
    const user = userEvent.setup();
    renderWithQuery(<CoverPagePicker gameId="g-1" pdfDocumentId="pdf-1" onProposed={onProposed} />);

    await user.type(screen.getByLabelText(/pagina/i), '3');
    await user.click(screen.getByRole('button', { name: /proponi cover/i }));

    await waitFor(() => expect(proposeMock).toHaveBeenCalledWith('g-1', 'pdf-1', 3));
    await waitFor(() => expect(onProposed).toHaveBeenCalledWith('sr-1'));
  });
});
```

- [ ] **Step 3: Run test → verify FAIL**

Run: `cd apps/web && pnpm test -- CoverPagePicker`
Expected: FAIL — componente non esiste.

- [ ] **Step 4: Implement the component**

```tsx
// CoverPagePicker.tsx
'use client';
import { useState } from 'react';
import { api } from '@/lib/api/clients/sharedGamesClient';

interface CoverPagePickerProps {
  gameId: string;
  pdfDocumentId: string;
  onProposed: (shareRequestId: string) => void;
}

export function CoverPagePicker({ gameId, pdfDocumentId, onProposed }: CoverPagePickerProps): JSX.Element {
  const [page, setPage] = useState(1);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function propose() {
    setBusy(true); setError(null);
    try {
      const { shareRequestId } = await api.sharedGames.proposeCoverFromPdf(gameId, pdfDocumentId, page);
      onProposed(shareRequestId);
    } catch {
      setError('Impossibile proporre la cover in questo momento.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      {pdfDocumentId && (
        <img src={api.sharedGames.getPdfPageImageUrl(pdfDocumentId, page)} alt={`Anteprima pagina ${page}`} />
      )}
      <label>
        Pagina
        <input type="number" min={1} value={page} onChange={(e) => setPage(Number(e.target.value))} />
      </label>
      <button type="button" disabled={busy} onClick={propose}>Proponi cover</button>
      {error && <p role="alert">{error}</p>}
    </div>
  );
}
```

- [ ] **Step 5: Run test → verify PASS**

Run: `cd apps/web && pnpm test -- CoverPagePicker`
Expected: PASS.

- [ ] **Step 6: Wire the two entry points**

- Ingresso 1 (post-upload): dopo che un PDF diventa `Ready`, montare `<CoverPagePicker>` in un prompt.
- Ingresso 2: azione "Imposta cover" sulla pagina del gioco che elenca i PDF `Ready` e apre lo stesso `<CoverPagePicker>`.

Verificare typecheck/lint: `cd apps/web && pnpm typecheck && pnpm lint`.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/shared-games/CoverPagePicker.tsx apps/web/src/lib/api/clients/sharedGamesClient.ts apps/web/src/components/shared-games/__tests__/CoverPagePicker.test.tsx
git commit -m "feat(web): CoverPagePicker + propose-cover-from-pdf (due ingressi)"
```

---

## Coverage vs spec (self-review)

**Coperto dal Piano 1** (SharedGame end-to-end + consolidamenti trasversali):
- WP1 (dedup consolidamento): Task 1-2 ✅
- WP2 (materializzazione + chiusura URL): Task 3, Task 7 ✅
- WP3 (picker FE, due ingressi): Task 8 ✅
- WP4 (governance ShareRequest CoverChange): Task 4-6 ✅
- WP7 parziale: metrica `r2_pdf` è **già** emessa dal resolver esistente (`CoverUrlResolver.cs:78`) → nessun lavoro. Attribuzione/cascade-clear non pertinenti a L4 PDF (riguardano L2 Wikidata) → Piano 2.

**Rimandato esplicitamente** (non è un buco silenzioso):
- **PrivateGame cover-da-PDF end-to-end**: il primitivo `MaterializePdfCover` (Task 3) è pronto, ma il PrivateGame non ha un resolver né un campo cover R2 dedicato. Wiring completo (nuovo campo cover R2 su PrivateGame + esposizione presigned) → **piano dedicato**. Task 7 chiude comunque il canale URL esterno subito.
- **Reference-counting al delete del PDF** (DEC-6, "eliminabile solo all'ultimo EntityLink"): richiede di individuare il call-site di eliminazione PDF e agganciarvi `IEntityLinkRepository.GetCountForEntityAsync`. → piano dedicato (evita di inventare un delete-flow non ancora identificato).
- **Wikidata cover on-demand + metadati (WP5, WP6)** + attribuzione/cascade-clear (resto di WP7): → **Piano 2**.
- **Auto-selezione pagina-cover** (#1852 Gap A): fuori scope (scelta esplicita utente).

**Type-consistency check**: `PdfDedupResult`/`PdfDedupDecision` (Task 1) usati coerenti in Task 2; `MaterializePdfCoverCommand(Guid, int, string)` (Task 3) invocato con la stessa firma in Task 6; `ShareRequest.CreateCoverChange(...)` (Task 4) consumato in Task 6; `ProposalApprovalAction.UpdateCover` (Task 5) coerente col command in Task 5-test; `PdfCoverR2Key` senza suffisso + physical `-preview.webp` coerente tra Task 3 (upload) e resolver (`CoverUrlResolver.cs:72`). Nessuna incoerenza.
