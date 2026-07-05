# Issue #2689 — Embedding Stuck Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rendere onesto il recovery dei PDF stuck (Layer A) e garantire che uno stallo in uno stato di processing non-terminale degradi a `Failed` — visibile e recuperabile — invece di restare invisibile per sempre (Layer B).

**Architecture:** Due fix indipendenti. **Layer A**: `StalePdfRecoveryService` rilegge `ProcessingState` dopo `ProcessAsync` e conta/logga l'esito reale (`recovered` solo se `Ready`). **Layer B**: un nuovo `DegradeStuckJobCommand` (CQRS/MediatR) marca un job `Processing` stuck oltre la soglia recovery — e il suo `PdfDocument` — come `Failed` (via i metodi di dominio `ProcessingJob.Fail` + `PdfDocument.MarkAsFailed`, TimeProvider-safe), con `ErrorCategory.Service` transitoria così l'infrastruttura di retry esistente (`RetryFailedPdfsJob`) lo ri-tenta con backoff bounded. `ProcessingQueueMonitorService` invia il command oltre la soglia recovery. Nessun re-queue custom nel monitor (evita il `Processing→Queued` che ha fatto revertire #2684).

**Tech Stack:** .NET 9, ASP.NET Minimal APIs + MediatR (CQRS), EF Core + PostgreSQL (pgvector), xUnit + Testcontainers, FluentValidation. Background services via Quartz.NET + `BackgroundService`.

## Global Constraints

- **CQRS**: la logica di degrade DEVE stare in un command handler MediatR, non inline nel BackgroundService (progetto: endpoints/servizi orchestrano via `IMediator.Send`). — CLAUDE.md § CQRS.
- **Eccezioni tipizzate**: usare `ConflictException` (409) / `NotFoundException` (404), mai `InvalidOperationException` (500) per errori attesi. — Issue #2568.
- **DateTime Kind (regression guard #2684)**: OGNI scrittura di `DateTime`/`DateTimeOffset` su entità con xmin DEVE usare `TimeProvider` (`_timeProvider.GetUtcNow()` per `DateTimeOffset`, `.UtcDateTime` per `DateTime`). MAI `DateTimeOffset.UtcNow`/`DateTime.UtcNow` diretti in questi percorsi. I metodi di dominio `ProcessingJob.Fail(msg, timeProvider)` e `PdfDocument.MarkAsFailed(...)` incapsulano già i timestamp — usare quelli, non assegnazioni dirette ai campi entity.
- **xmin optimistic concurrency**: `PdfDocumentEntity` (`PdfDocumentEntityConfiguration.cs:185-188`, `.IsRowVersion()`) e i job usano concurrency; ogni handler che scrive DEVE catturare `DbUpdateConcurrencyException` e trattarla come skip best-effort (no re-throw nel BackgroundService).
- **Culture-independent**: nessuna formattazione locale-dipendente nei messaggi/log (usare invariant). — Issue #2593.
- **Baseline test**: la PR non deve far crescere il fail-count dei unit test sopra la baseline (attualmente zero). — CLAUDE.md § Known Flaky Tests.
- **Trait**: i nuovi test backend devono avere `[Trait("Category","Unit")]` o `[Trait("Category","Integration")]` + `[Trait("BoundedContext","DocumentProcessing")]` per essere inclusi nei gate CI.

---

## Contesto della root cause (per l'implementer)

I 4 PDF (`carcassone`, `terra-mystica`, `skytear`, `roll-player`) sono orfani: durante il re-bake RAG (#2670/#2671/#2679) un restart dell'API (deploy) ha ucciso il worker Quartz mentre `PdfProcessingPipelineService.ProcessAsync` era nello step embedding (`PdfProcessingPipelineService.cs:387`). Il `processing_jobs` è rimasto `Processing`, il `PdfDocument` in `Embedding`. I chunks si persistono solo DOPO l'embedding (`...:409`), quindi `chunks=0`.

Due difetti sistemici:
1. **Log fuorviante** (`StalePdfRecoveryService.cs:102`): `recovered++` incondizionato, non verifica `ProcessingState==Ready`. → Layer A.
2. **Nessuno stato terminale garantito**: uno stallo in `Embedding` non degrada mai a `Failed` → invisibile e non-auto-recuperabile. → Layer B.

Il fix #2684 (auto-recovery re-queue nel monitor) è stato **revertito** (#2686/`c82d27299`) per: (a) DateTime `Kind=Unspecified` → no-op loop, (b) ridondanza/race con StalePdfRecovery. Questo piano NON re-introduce il re-queue: degrada solo a `Failed` (terminale, no loop, no race) e delega il retry a `RetryFailedPdfsJob` (esistente, bounded `RetryCount<3`).

**Limite noto documentato** (fuori scope, tracciare separatamente se necessario): la catena `RetryFailedPdfsJob → RetryPdfProcessingCommand → PdfDocument.Retry()` porta il PDF a `Extracting` senza creare un job `Queued`; il percorso affidabile verso `Ready` passa da `ProcessingJob.Retry()` → job `Queued` (come `BulkReindexFailedCommandHandler:79`). Il Layer B garantisce lo stato terminale/visibile; il ritorno automatico a `Ready` dei 4 PDF dipende dai meccanismi di reindex esistenti o da un reindex admin manuale.

---

## File Structure

**Layer A**
- Modify: `apps/api/src/Api/Infrastructure/BackgroundServices/StalePdfRecoveryService.cs` — rilettura stato post-`ProcessAsync` + conteggi onesti.
- Test: `tests/Api.Tests/Unit/DocumentProcessing/StalePdfRecoveryServiceTests.cs` (esistente o nuovo).

**Layer B**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/Queue/DegradeStuckJobCommand.cs` — command + result record.
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/Queue/DegradeStuckJobCommandHandler.cs` — logica di degrade (domain methods).
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Services/ProcessingQueueMonitorService.cs` — soglia recovery + invio command via `IMediator`.
- Test: `tests/Api.Tests/Unit/DocumentProcessing/DegradeStuckJobCommandHandlerTests.cs` (nuovo).
- Test: `tests/Api.Tests/Integration/DocumentProcessing/DegradeStuckJobIntegrationTests.cs` (nuovo, Testcontainers — regression xmin/DateTime).
- Test: `tests/Api.Tests/Unit/DocumentProcessing/ProcessingQueueMonitorServiceTests.cs` (esistente o nuovo) — soglia.

**Prima di iniziare** — l'implementer DEVE leggere per confermare firme esatte:
- `ProcessingJob.cs:149` (`Fail(string, TimeProvider?)`), `PdfDocument.cs:368-390` (`MarkAsFailed` overloads).
- `IProcessingJobRepository` + `IPdfDocumentRepository` (metodi `GetByIdAsync`, `UpdateAsync`) — pattern in `BulkReindexFailedCommandHandler.cs` e `RetryPdfProcessingCommandHandler.cs`.
- Un command handler `Queue` esistente (es. `RetryJobCommandHandler.cs`) per il pattern `ICommandHandler<,>`, registrazione DI e namespace.
- `ProcessingJobEntity.cs` (mapping `Status` string values: `"Processing"`, `"Failed"`).

---

## Task 1 — Layer A: log onesto in StalePdfRecoveryService

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/BackgroundServices/StalePdfRecoveryService.cs:75-123`
- Test: `tests/Api.Tests/Unit/DocumentProcessing/StalePdfRecoveryServiceTests.cs`

**Interfaces:**
- Consumes: `IPdfProcessingPipelineService.ProcessAsync`, `MeepleAiDbContext.PdfDocuments`, `PdfProcessingState` enum.
- Produces: log `Recovery complete: {recovered} recovered, {failed} failed, {stillStuck} still stuck, {total} total` dove i conteggi riflettono lo stato reale riletto.

- [ ] **Step 1: Leggere il file corrente e il test esistente**

Leggere `StalePdfRecoveryService.cs` (loop righe 78-118) e cercare un file di test esistente:
Run: `git ls-files "tests/**/*StalePdfRecovery*"`
Se non esiste, crearne uno nuovo nel path indicato con `[Trait("Category","Unit")]` + `[Trait("BoundedContext","DocumentProcessing")]`.

- [ ] **Step 2: Scrivere il test che fallisce — un PDF che resta non-Ready NON è "recovered"**

Il test costruisce uno `StalePdfRecoveryService` con un `IPdfProcessingPipelineService` mock il cui `ProcessAsync` lascia il `PdfDocument` in stato `Embedding` (non lo cambia). Un `PdfDocument` stale è pre-seedato in un `MeepleAiDbContext` in-memory (o via scope factory mock). Dopo `StartAsync`/l'esecuzione del recovery, il test asserisce che il log riporta `0 recovered, 0 failed, 1 still stuck`.

```csharp
[Fact]
[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public async Task Recovery_WhenPdfStaysInEmbedding_CountsAsStillStuck_NotRecovered()
{
    // Arrange: seed 1 stale PDF in Embedding (UploadedAt older than ProcessingStaleness);
    // pipeline mock ProcessAsync leaves state unchanged (simulates claim-skip / concurrency return).
    // Use a captured ILogger (e.g. a FakeLogger / list sink) to assert the summary line.
    // ... (build service with IServiceScopeFactory over the in-memory context)

    // Act
    await service.StartAsync(CancellationToken.None);
    await service.StopAsync(CancellationToken.None); // let ExecuteAsync run

    // Assert
    Assert.Contains(logSink.Entries, e =>
        e.Contains("0 recovered") && e.Contains("1 still stuck"));
}
```

Nota: `ExecuteAsync` attende `StartupDelay = 30s` (riga 16). Per rendere il test rapido, l'implementer DEVE rendere `StartupDelay` iniettabile/overridabile (es. costante → campo con default, settabile in test via `internal` + `InternalsVisibleTo=Api.Tests`) OPPURE testare direttamente un metodo estratto `RecoverAllAsync(stoppingToken)` (preferito — vedi Step 3).

- [ ] **Step 3: Refactor + implementazione — estrarre il loop e rileggere lo stato**

Estrarre il corpo del recovery (righe 78-122) in un metodo `internal async Task<(int recovered, int failed, int stillStuck)> RecoverAllAsync(IReadOnlyList<StalePdfInfo> stalePdfs, CancellationToken stoppingToken)` per testabilità. Dentro il loop, DOPO `ProcessAsync`, rileggere lo stato in un nuovo scope e classificare:

```csharp
// after: await pipeline.ProcessAsync(...);
var finalState = await ReadProcessingStateAsync(pdf.Id, stoppingToken).ConfigureAwait(false);

if (string.Equals(finalState, nameof(PdfProcessingState.Ready), StringComparison.Ordinal))
{
    recovered++;
    _logger.LogInformation("[StalePdfRecovery] Recovered PDF {PdfId} → Ready", pdf.Id);
}
else if (string.Equals(finalState, nameof(PdfProcessingState.Failed), StringComparison.Ordinal))
{
    failed++;
    _logger.LogWarning("[StalePdfRecovery] PDF {PdfId} ended Failed after reprocessing (will be retried by RetryFailedPdfsJob)", pdf.Id);
}
else
{
    stillStuck++;
    _logger.LogWarning("[StalePdfRecovery] PDF {PdfId} did NOT progress (state={State}); recovery ineffective", pdf.Id, finalState);
}
```

Aggiungere il metodo helper:

```csharp
private async Task<string?> ReadProcessingStateAsync(Guid pdfDocumentId, CancellationToken ct)
{
    using var scope = _scopeFactory.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
    return await db.PdfDocuments
        .Where(p => p.Id == pdfDocumentId)
        .Select(p => p.ProcessingState)
        .FirstOrDefaultAsync(ct)
        .ConfigureAwait(false);
}
```

Aggiornare il `catch (Exception ex)` (riga 112): l'incremento `failed++` per eccezione resta (è un fallimento reale, diverso da `stillStuck`). Aggiornare la riga di summary (righe 120-122):

```csharp
_logger.LogInformation(
    "[StalePdfRecovery] Recovery complete: {Recovered} recovered, {Failed} failed, {StillStuck} still stuck, {Total} total",
    recovered, failed, stillStuck, stalePdfs.Count);
```

- [ ] **Step 4: Eseguire il test — verificare che passi**

Run: `cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~StalePdfRecoveryServiceTests"`
Expected: PASS (il PDF che resta in Embedding è contato `still stuck`, non `recovered`).

- [ ] **Step 5: Test aggiuntivo — un PDF che raggiunge Ready È recovered**

Aggiungere un secondo test con pipeline mock che porta il PDF a `Ready`; asserire `1 recovered, 0 still stuck`. Eseguire entrambi.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Infrastructure/BackgroundServices/StalePdfRecoveryService.cs tests/Api.Tests/Unit/DocumentProcessing/StalePdfRecoveryServiceTests.cs
git commit -m "fix(processing): #2689 StalePdfRecoveryService logs real outcome, not unconditional 'recovered'"
```

---

## Task 2 — Layer B: DegradeStuckJobCommand + handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/Queue/DegradeStuckJobCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/Queue/DegradeStuckJobCommandHandler.cs`
- Test: `tests/Api.Tests/Unit/DocumentProcessing/DegradeStuckJobCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `IProcessingJobRepository` (`GetByIdAsync(Guid, CancellationToken)`, `UpdateAsync`), `IPdfDocumentRepository` (`GetByIdAsync`, `UpdateAsync`), `IUnitOfWork.SaveChangesAsync`, `TimeProvider`, domain `ProcessingJob.Fail(string, TimeProvider?)`, `PdfDocument.MarkAsFailed(string, ErrorCategory, PdfProcessingState)`.
- Produces: `DegradeStuckJobCommand(Guid JobId, double StuckMinutes) : ICommand<DegradeStuckJobResult>`; `DegradeStuckJobResult(bool Degraded, string Reason)`. Usato dal monitor (Task 3).

- [ ] **Step 1: Leggere i pattern di riferimento**

Leggere `BulkReindexFailedCommandHandler.cs` (repository + UnitOfWork + concurrency), `RetryJobCommandHandler.cs` (pattern `ICommandHandler<,>` + record command + DI in namespace `...Commands.Queue`), `ProcessingJob.cs:149` (`Fail`), `PdfDocument.cs:368-390` (`MarkAsFailed`). Confermare le firme esatte dei metodi repository (`GetByIdAsync` ritorna il domain model o null).

- [ ] **Step 2: Scrivere il test che fallisce — degrade marca job+PDF Failed**

```csharp
[Fact]
[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public async Task Handle_JobStuckInProcessing_MarksJobAndPdfFailed_WithServiceCategory()
{
    // Arrange: domain ProcessingJob in Processing (StartedAt old); PdfDocument in Embedding.
    // Repos mocked to return them; capture the entities passed to UpdateAsync.
    var handler = new DegradeStuckJobCommandHandler(jobRepo, pdfRepo, unitOfWork, timeProvider, logger);

    // Act
    var result = await handler.Handle(new DegradeStuckJobCommand(jobId, 42.0), CancellationToken.None);

    // Assert
    Assert.True(result.Degraded);
    Assert.Equal(nameof(JobStatus.Failed), capturedJob.Status);          // or domain equivalent
    Assert.Equal(PdfProcessingState.Failed, capturedPdf.ProcessingState);
    Assert.Equal(ErrorCategory.Service, capturedPdf.ErrorCategory);       // transient → RetryFailedPdfsJob eligible
    Assert.Equal(PdfProcessingState.Embedding, capturedPdf.FailedAtState);
}
```

- [ ] **Step 3: Implementare il command**

`DegradeStuckJobCommand.cs`:

```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;

/// <summary>
/// Degrades a job stuck in Processing (past the recovery threshold) — and its PdfDocument —
/// to Failed, so it becomes visible/terminal and eligible for the existing bounded retry
/// (RetryFailedPdfsJob). Issue #2689. Deliberately does NOT re-queue (that re-queue was
/// reverted in #2686); recovery to Ready is the existing reindex path's responsibility.
/// </summary>
public sealed record DegradeStuckJobCommand(Guid JobId, double StuckMinutes)
    : ICommand<DegradeStuckJobResult>;

public sealed record DegradeStuckJobResult(bool Degraded, string Reason);
```

- [ ] **Step 4: Implementare l'handler**

`DegradeStuckJobCommandHandler.cs`. Logica: ricarica il job; se non più `Processing` → no-op (`Degraded=false`, race già risolta). Altrimenti `job.Fail(...)` (TimeProvider-safe), poi ricarica il PDF; se non terminale → `pdf.MarkAsFailed(..., ErrorCategory.Service, pdf.ProcessingState)`. Salvare. Catturare `DbUpdateConcurrencyException` → `Degraded=false` (best-effort). Usare `System.Globalization.CultureInfo.InvariantCulture` per `StuckMinutes` nel messaggio.

```csharp
internal sealed class DegradeStuckJobCommandHandler
    : ICommandHandler<DegradeStuckJobCommand, DegradeStuckJobResult>
{
    private readonly IProcessingJobRepository _jobRepository;
    private readonly IPdfDocumentRepository _pdfRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DegradeStuckJobCommandHandler> _logger;

    // ctor: assign all, null-guarded

    public async Task<DegradeStuckJobResult> Handle(DegradeStuckJobCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var job = await _jobRepository.GetByIdAsync(command.JobId, ct).ConfigureAwait(false);
        if (job is null)
            return new DegradeStuckJobResult(false, "Job not found");

        // Double-check status: it may have completed between detection and now.
        if (!string.Equals(job.Status.ToString(), nameof(JobStatus.Processing), StringComparison.Ordinal))
            return new DegradeStuckJobResult(false, $"Job no longer Processing (was {job.Status})");

        var minutes = command.StuckMinutes.ToString("F0", CultureInfo.InvariantCulture);
        var message = $"Processing stalled for {minutes} min past the recovery threshold; degraded to Failed (Issue #2689).";

        try
        {
            job.Fail(message, _timeProvider);
            await _jobRepository.UpdateAsync(job, ct).ConfigureAwait(false);

            var pdf = await _pdfRepository.GetByIdAsync(job.PdfDocumentId, ct).ConfigureAwait(false);
            if (pdf is not null && pdf.ProcessingState != PdfProcessingState.Ready
                                && pdf.ProcessingState != PdfProcessingState.Failed)
            {
                // ErrorCategory.Service is transient → RetryFailedPdfsJob will pick it up (RetryCount<3).
                pdf.MarkAsFailed(message, ErrorCategory.Service, pdf.ProcessingState);
                await _pdfRepository.UpdateAsync(pdf, ct).ConfigureAwait(false);
            }

            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogWarning("[DegradeStuckJob] Degraded stuck job {JobId} (PDF {PdfId}) to Failed after {Minutes} min",
                command.JobId, job.PdfDocumentId, minutes);
            return new DegradeStuckJobResult(true, "Degraded to Failed");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            MeepleAiMetrics.RecordPdfConcurrencyConflict(
                nameof(DegradeStuckJobCommandHandler),
                MeepleAiMetrics.PdfConcurrencyCategories.C);
            _logger.LogWarning(ex, "[DegradeStuckJob] Concurrency conflict degrading job {JobId}; skipping", command.JobId);
            return new DegradeStuckJobResult(false, "Concurrency conflict — skipped");
        }
    }
}
```

Nota: confermare i namespace/using esatti (`Api.BoundedContexts.DocumentProcessing.Domain.Enums` per `ErrorCategory`/`PdfProcessingState`/`JobStatus`, `Api.Observability` per `MeepleAiMetrics`, `System.Globalization`). Confermare la firma di `job.Status` (enum vs string) leggendo `ProcessingJob.cs`.

- [ ] **Step 5: Eseguire i test — verificare pass**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~DegradeStuckJobCommandHandlerTests"`
Expected: PASS.

- [ ] **Step 6: Test idempotenza + concurrency**

Aggiungere: (a) job già `Failed` → `Degraded=false`, nessuna scrittura; (b) `SaveChangesAsync` che lancia `DbUpdateConcurrencyException` → `Degraded=false`, nessun re-throw. Eseguire.

- [ ] **Step 7: Registrare l'handler nel DI (se non auto-registrato)**

Verificare come sono registrati gli altri `Queue` handler (probabilmente scan MediatR automatico in `DocumentProcessingServiceExtensions.cs`). Se serve registrazione esplicita, aggiungerla. Build:
Run: `cd apps/api/src/Api && dotnet build`
Expected: build OK.

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/Queue/DegradeStuckJobCommand.cs apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/Queue/DegradeStuckJobCommandHandler.cs tests/Api.Tests/Unit/DocumentProcessing/DegradeStuckJobCommandHandlerTests.cs
git commit -m "feat(processing): #2689 DegradeStuckJobCommand marks stuck job+PDF Failed (transient category)"
```

---

## Task 3 — Layer B: wire del monitor (soglia recovery → invia command)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Services/ProcessingQueueMonitorService.cs`
- Test: `tests/Api.Tests/Unit/DocumentProcessing/ProcessingQueueMonitorServiceTests.cs`

**Interfaces:**
- Consumes: `DegradeStuckJobCommand` (Task 2), `IMediator`, config `ProcessingQueueMonitor:StuckJobRecoveryTimeoutMinutes`.
- Produces: comportamento — job stuck oltre soglia recovery → `IMediator.Send(new DegradeStuckJobCommand(jobId, stuckMinutes))`; sotto soglia → solo alert (invariato).

- [ ] **Step 1: Scrivere il test che fallisce — oltre soglia recovery invia il command**

Il monitor risolve `IMediator` dallo scope. Il test seeda un `ProcessingJobEntity` `Processing` con `StartedAt` > soglia recovery (default 30 min), esegue `RunChecksAsync` (rendere `internal` + `InternalsVisibleTo` se non lo è), e verifica che `IMediator.Send` sia stato chiamato con un `DegradeStuckJobCommand` per quel job. Un secondo job stuck 15 min (oltre alert 10, sotto recovery 30) → NON deve inviare il command (solo alert).

```csharp
[Fact]
[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public async Task CheckStuckJobs_JobStuckPastRecoveryThreshold_SendsDegradeCommand()
{
    // Arrange: seed job Processing StartedAt = now - 40min; mediator mock.
    // Act: await monitor.RunChecksAsync(ct)  (or the internal CheckStuckJobsAsync)
    // Assert:
    mediatorMock.Verify(m => m.Send(
        It.Is<DegradeStuckJobCommand>(c => c.JobId == jobId), It.IsAny<CancellationToken>()),
        Times.Once);
}
```

- [ ] **Step 2: Implementare — soglia + invio**

Aggiungere la proprietà soglia (mirror di #2684, ma per invio command non re-queue):

```csharp
// Recovery threshold — higher than the alert threshold (10 min) so the early "stuck"
// warning fires first, but a merely-slow job is never degraded. #2689.
private TimeSpan StuckJobRecoveryTimeout =>
    TimeSpan.FromMinutes(_configuration.GetValue("ProcessingQueueMonitor:StuckJobRecoveryTimeoutMinutes", 30));
```

In `CheckStuckJobsAsync`, dopo l'alert (dopo riga 119), risolvere `IMediator` dallo scope e inviare il command quando oltre soglia:

```csharp
if (stuckMinutes >= StuckJobRecoveryTimeout.TotalMinutes)
{
    var mediator = /* resolved from the same scope as db/streamService in RunChecksAsync */;
    var result = await mediator.Send(new DegradeStuckJobCommand(job.Id, stuckMinutes), ct).ConfigureAwait(false);
    if (result.Degraded)
    {
        _logger.LogWarning(
            "Auto-degraded stuck job {JobId} (PDF: {FileName}) to Failed after {Minutes:F1} min (Issue #2689)",
            job.Id, job.FileName, stuckMinutes);
    }
}
```

Per risolvere `IMediator`: passarlo come parametro a `CheckStuckJobsAsync` da `RunChecksAsync` (che ha già lo scope, righe 77-83) — aggiungere `var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();` e propagarlo. Aggiungere `using MediatR;`.

- [ ] **Step 3: Eseguire i test — verificare pass**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~ProcessingQueueMonitorServiceTests"`
Expected: PASS (oltre soglia → command inviato; sotto soglia → nessun command, solo alert).

- [ ] **Step 4: Test — sotto soglia NON degrada**

Aggiungere il caso 15-min (nessun `Send`). Verificare che gli alert SSE esistenti restino invariati (non rompere i test esistenti del monitor). Eseguire l'intera classe.

- [ ] **Step 5: Build + commit**

```bash
cd apps/api/src/Api && dotnet build
```
```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Services/ProcessingQueueMonitorService.cs tests/Api.Tests/Unit/DocumentProcessing/ProcessingQueueMonitorServiceTests.cs
git commit -m "feat(processing): #2689 monitor auto-degrades stuck jobs past recovery threshold via DegradeStuckJobCommand"
```

---

## Task 4 — Regression guard: degrade persiste sotto xmin (Testcontainers)

Difesa esplicita contro il bug DateTime `Kind=Unspecified` che ha affossato #2684. Prova empirica che il degrade persiste su Postgres reale senza `ArgumentException`.

**Files:**
- Test: `tests/Api.Tests/Integration/DocumentProcessing/DegradeStuckJobIntegrationTests.cs`

**Interfaces:**
- Consumes: `DegradeStuckJobCommandHandler` (Task 2), Testcontainers Postgres fixture esistente, `MeepleAiDbContext`, repository reali.

- [ ] **Step 1: Leggere una integration test esistente del contesto**

Run: `git ls-files "tests/Api.Tests/Integration/DocumentProcessing/*"`
Leggere una classe esistente (es. una che usa la fixture Testcontainers Postgres) per il pattern di setup DB reale + risoluzione repository/handler reali.

- [ ] **Step 2: Scrivere il test — degrade end-to-end su Postgres reale**

```csharp
[Fact]
[Trait("Category", "Integration")]
[Trait("BoundedContext", "DocumentProcessing")]
public async Task Degrade_StuckJobAndPdf_PersistsFailedState_NoDateTimeKindError()
{
    // Arrange: insert a PdfDocument in Embedding + a ProcessingJob Processing (StartedAt old)
    // into the real Testcontainers Postgres. Load them once so xmin is populated
    // (reproduces the UPDATE...WHERE xmin=Y RETURNING xmin path that broke #2684).

    // Act: resolve the real handler + real repositories + real DbContext and run:
    var result = await handler.Handle(new DegradeStuckJobCommand(jobId, 45.0), CancellationToken.None);

    // Assert: no exception thrown, and a fresh read shows Failed on BOTH rows.
    Assert.True(result.Degraded);
    var reloadedPdf = await db.PdfDocuments.AsNoTracking().FirstAsync(p => p.Id == pdfId);
    Assert.Equal(nameof(PdfProcessingState.Failed), reloadedPdf.ProcessingState);
    Assert.Equal(nameof(ErrorCategory.Service), reloadedPdf.ErrorCategory); // or enum, per mapping
    var reloadedJob = await db.Set<ProcessingJobEntity>().AsNoTracking().FirstAsync(j => j.Id == jobId);
    Assert.Equal("Failed", reloadedJob.Status);
    Assert.NotNull(reloadedJob.CompletedAt); // written via TimeProvider — must persist without ArgumentException
}
```

- [ ] **Step 3: Eseguire — verificare pass (richiede Docker)**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~DegradeStuckJobIntegrationTests"`
Expected: PASS. Se fallisse con `ArgumentException` su un campo DateTime → è la regressione #2684; trovare il campo scritto senza `TimeProvider` e correggerlo (nel domain method o nel mapper).

- [ ] **Step 4: Commit**

```bash
git add tests/Api.Tests/Integration/DocumentProcessing/DegradeStuckJobIntegrationTests.cs
git commit -m "test(processing): #2689 Testcontainers regression guard — degrade persists under xmin, no DateTime Kind error"
```

---

## Task 5 — Verifica finale + issue update

- [ ] **Step 1: Eseguire l'intero cluster DocumentProcessing**

Run: `dotnet test tests/Api.Tests --filter "BoundedContext=DocumentProcessing"`
Expected: tutti PASS, nessuna regressione sui test esistenti del monitor/recovery.

- [ ] **Step 2: Build completo backend**

Run: `cd apps/api/src/Api && dotnet build`
Expected: 0 warning nuovi, 0 error.

- [ ] **Step 3: Kill testhost (Windows) se blocca**

Run (se necessario): `tasklist | grep testhost` → `taskkill //PID <PID> //F`

- [ ] **Step 4: Aggiornare la issue #2689 (DoD)**

Documentare nel corpo/commento della issue: Layer A (log onesto) + Layer B (degrade-to-Failed via DegradeStuckJobCommand) implementati; limite noto sul ritorno a `Ready` (dipende dal reindex esistente); i 4 PDF di staging andranno reindicizzati (bulk-reindex-failed o admin) una volta degradati a `Failed`.

---

## Self-Review

**Spec coverage:**
- Bug secondario (log fuorviante `StalePdfRecoveryService`) → Task 1. ✅
- Root cause (stallo Embedding invisibile) → Task 2+3 (degrade a Failed terminale/visibile). ✅
- Regression #2684 (DateTime Kind / no-op loop) → Task 4 (Testcontainers) + Global Constraints (TimeProvider). ✅
- Rispetto del revert #2686 (no re-queue custom nel monitor) → design degrade-only, documentato. ✅
- Retry bounded → delegato a `RetryFailedPdfsJob` esistente (`ErrorCategory.Service` + `RetryCount<3`). ✅

**Placeholder scan:** i corpi dei metodi di dominio (`ProcessingJob.Fail`, `PdfDocument.MarkAsFailed`) e le firme repository sono indicati come "da confermare leggendo file X" perché esistono già nel codebase — questo è lettura di codice esistente, non un placeholder di logica nuova. Tutta la logica NUOVA (handler, wire monitor, test) ha codice concreto.

**Type consistency:** `DegradeStuckJobCommand(Guid JobId, double StuckMinutes)` / `DegradeStuckJobResult(bool Degraded, string Reason)` usati coerentemente in Task 2/3/4. `ErrorCategory.Service` + `FailedAtState` coerenti. Attenzione (nota per l'implementer): `ProcessingJobEntity.Status` è `string` ("Processing"/"Failed") mentre il domain `ProcessingJob.Status` potrebbe essere enum `JobStatus` — confermare e usare la forma giusta in ciascun layer (query entity = string; domain method = enum).

**Rischio residuo dichiarato:** il meccanismo esatto dell'hang originale (E8) resta non confermato senza dati di staging; il design è difensivo (garantisce stato terminale indipendentemente dalla causa). Il ritorno automatico dei 4 PDF a `Ready` non è garantito da questo fix (limite noto documentato) — l'obiettivo raggiunto è visibilità + terminazione + eleggibilità al retry esistente.
