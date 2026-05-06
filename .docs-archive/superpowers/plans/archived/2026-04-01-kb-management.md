# KB Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementare la gestione completa del Knowledge Base (KB) dei giochi: vista admin per-gioco, feedback utente sulle risposte chat, coverage score, domande suggerite pre-calcolate, e impostazioni KB per-gioco.

**Architecture:** Ogni feature segue il pattern CQRS esistente (Commands/Queries via MediatR). Il feedback utente è in una nuova entity `KbUserFeedback` nella KB BC. Il coverage score e le domande suggerite sono pre-calcolate da Quartz jobs e salvate in `SystemConfiguration` con chiavi gerarchiche `KB:*`.

**Tech Stack:** .NET 9 · MediatR · EF Core + pgvector · Quartz.NET · Next.js 16 · React Query · Zustand

---

## File Structure

### Backend — nuovi file
| File | Responsabilità |
|------|----------------|
| `BoundedContexts/KnowledgeBase/Domain/Entities/KbUserFeedback.cs` | Aggregate root per feedback utente su risposta chat |
| `BoundedContexts/KnowledgeBase/Domain/Repositories/IKbUserFeedbackRepository.cs` | Interfaccia repository |
| `BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Configurations/KbUserFeedbackConfiguration.cs` | EF config → `knowledge_base.kb_user_feedback` |
| `BoundedContexts/KnowledgeBase/Infrastructure/Persistence/KbUserFeedbackRepository.cs` | Implementazione repository |
| `BoundedContexts/KnowledgeBase/Application/Queries/GetAdminGameKbDocuments/GetAdminGameKbDocumentsQuery.cs` | Query lista documenti per-gioco (admin) |
| `BoundedContexts/KnowledgeBase/Application/Queries/GetAdminGameKbDocuments/GetAdminGameKbDocumentsQueryHandler.cs` | Handler |
| `BoundedContexts/KnowledgeBase/Application/Commands/RemoveDocumentFromKb/RemoveDocumentFromKbCommand.cs` | Command rimozione documento KB (admin) |
| `BoundedContexts/KnowledgeBase/Application/Commands/RemoveDocumentFromKb/RemoveDocumentFromKbCommandHandler.cs` | Handler |
| `BoundedContexts/KnowledgeBase/Application/Commands/RemoveDocumentFromKb/RemoveDocumentFromKbCommandValidator.cs` | Validazione |
| `BoundedContexts/KnowledgeBase/Application/Queries/GetUserGameKbStatus/GetUserGameKbStatusQuery.cs` | Query stato KB per utente |
| `BoundedContexts/KnowledgeBase/Application/Queries/GetUserGameKbStatus/GetUserGameKbStatusQueryHandler.cs` | Handler |
| `BoundedContexts/KnowledgeBase/Application/Commands/SubmitKbFeedback/SubmitKbFeedbackCommand.cs` | Command feedback utente |
| `BoundedContexts/KnowledgeBase/Application/Commands/SubmitKbFeedback/SubmitKbFeedbackCommandHandler.cs` | Handler |
| `BoundedContexts/KnowledgeBase/Application/Commands/SubmitKbFeedback/SubmitKbFeedbackCommandValidator.cs` | Validazione |
| `BoundedContexts/KnowledgeBase/Application/Queries/GetAdminKbFeedback/GetAdminKbFeedbackQuery.cs` | Query lista feedback (admin) |
| `BoundedContexts/KnowledgeBase/Application/Queries/GetAdminKbFeedback/GetAdminKbFeedbackQueryHandler.cs` | Handler |
| `BoundedContexts/KnowledgeBase/Application/Commands/SetGameKbSettings/SetGameKbSettingsCommand.cs` | Command impostazioni KB per-gioco (admin) |
| `BoundedContexts/KnowledgeBase/Application/Commands/SetGameKbSettings/SetGameKbSettingsCommandHandler.cs` | Handler |
| `BoundedContexts/KnowledgeBase/Application/Queries/GetGameKbSettings/GetGameKbSettingsQuery.cs` | Query impostazioni KB per-gioco (admin) |
| `BoundedContexts/KnowledgeBase/Application/Queries/GetGameKbSettings/GetGameKbSettingsQueryHandler.cs` | Handler |
| `BoundedContexts/KnowledgeBase/Infrastructure/Scheduling/KbCoverageComputeJob.cs` | Quartz job calcolo coverage score |
| `BoundedContexts/KnowledgeBase/Infrastructure/Scheduling/KbSuggestedQuestionsJob.cs` | Quartz job domande suggerite |
| `Routing/AdminGameKbEndpoints.cs` | Endpoint admin per-gioco KB |
| `Routing/UserGameKbEndpoints.cs` | Endpoint utente KB |
| `Infrastructure/Migrations/YYYYMMDD_AddKbUserFeedback.cs` | Migrazione EF |

### Backend — file modificati
| File | Modifica |
|------|----------|
| `BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs` | Registra 2 nuovi job Quartz + repository |
| `Infrastructure/MeepleAiDbContext.cs` | Aggiunge `DbSet<KbUserFeedback>` |
| `Routing/EndpointRegistration.cs` (o `Program.cs`) | Registra nuovi route group |

### Frontend — nuovi file
| File | Responsabilità |
|------|----------------|
| `src/app/admin/(dashboard)/shared-games/[id]/knowledge-base/page.tsx` | Pagina admin KB per-gioco |
| `src/components/admin/knowledge-base/game-kb-documents.tsx` | Lista documenti per-gioco |
| `src/components/admin/knowledge-base/game-kb-settings.tsx` | Form impostazioni KB per-gioco |
| `src/app/admin/(dashboard)/knowledge-base/feedback/page.tsx` | Pagina admin review feedback |
| `src/components/admin/knowledge-base/kb-feedback-panel.tsx` | Pannello feedback con filtri |
| `src/hooks/use-game-kb-status.ts` | Hook React per stato KB gioco |

### Frontend — file modificati
| File | Modifica |
|------|----------|
| `src/lib/api/clients/knowledgeBaseClient.ts` | Aggiunge `getUserGameKbStatus`, `submitKbFeedback`, `getAdminGameKbDocuments`, `removeKbDocument`, `getAdminKbFeedback`, `getGameKbSettings`, `setGameKbSettings` |
| `src/components/ui/meeple/chat-message.tsx` | Connette `onFeedbackChange` a `submitKbFeedback` |
| `src/app/admin/(dashboard)/shared-games/[id]/page.tsx` | Aggiunge tab "Knowledge Base" |

---

## Task 1: KB-05 — Coverage Score Batch Job

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Scheduling/KbCoverageComputeJob.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/Scheduling/KbCoverageComputeJobTests.cs`

- [ ] **Step 1: Scrivi il test fallente**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/Scheduling/KbCoverageComputeJobTests.cs
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;
using Api.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class KbCoverageComputeJobTests
{
    [Fact]
    public void ComputeCoverageScore_NoDocuments_ReturnsZero()
    {
        var score = KbCoverageComputeJob.ComputeScore(
            hasRulebook: false, hasErrata: false, hasFaq: false,
            completedChunks: 0, totalChunks: 0, daysSinceLastIndex: 999);

        Assert.Equal(0, score);
    }

    [Fact]
    public void ComputeCoverageScore_OnlyRulebook_Returns50()
    {
        var score = KbCoverageComputeJob.ComputeScore(
            hasRulebook: true, hasErrata: false, hasFaq: false,
            completedChunks: 0, totalChunks: 0, daysSinceLastIndex: 999);

        Assert.Equal(50, score);
    }

    [Fact]
    public void ComputeCoverageScore_FullCoverage_Returns100()
    {
        var score = KbCoverageComputeJob.ComputeScore(
            hasRulebook: true, hasErrata: true, hasFaq: true,
            completedChunks: 300, totalChunks: 300, daysSinceLastIndex: 10);

        Assert.Equal(100, score);
    }

    [Theory]
    [InlineData(0, "None")]
    [InlineData(24, "None")]
    [InlineData(25, "Basic")]
    [InlineData(49, "Basic")]
    [InlineData(50, "Standard")]
    [InlineData(74, "Standard")]
    [InlineData(75, "Complete")]
    [InlineData(100, "Complete")]
    public void CoverageLevel_ReturnsCorrectLabel(int score, string expected)
    {
        Assert.Equal(expected, KbCoverageComputeJob.ScoreToLevel(score));
    }
}
```

- [ ] **Step 2: Esegui test — verifica fallimento**

```bash
cd apps/api
dotnet test tests/Api.Tests/Api.Tests.csproj \
  --filter "FullyQualifiedName~KbCoverageComputeJobTests" \
  --nologo -v q
```
Expected: FAIL — `KbCoverageComputeJob` not found

- [ ] **Step 3: Implementa il job**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Scheduling/KbCoverageComputeJob.cs
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;

/// <summary>
/// Quartz job: calcola il coverage score per ogni gioco con documenti indicizzati
/// e lo persiste in SystemConfiguration con chiave "KB:Coverage:{gameId}".
/// KB-05: Coverage Score Batch Job.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class KbCoverageComputeJob : IJob
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ISystemConfigurationRepository _configRepo;
    private readonly ILogger<KbCoverageComputeJob> _logger;

    public KbCoverageComputeJob(
        MeepleAiDbContext dbContext,
        ISystemConfigurationRepository configRepo,
        ILogger<KbCoverageComputeJob> logger)
    {
        _dbContext = dbContext;
        _configRepo = configRepo;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;

        // Raggruppa VectorDocuments completati per SharedGameId
        var docs = await _dbContext.VectorDocuments
            .AsNoTracking()
            .Where(v => v.SharedGameId.HasValue)
            .Select(v => new
            {
                v.SharedGameId,
                v.ChunkCount,
                v.IndexingStatus,
                v.IndexedAt,
                PdfCategory = _dbContext.Set<object>() // placeholder — see note below
            })
            .ToListAsync(ct).ConfigureAwait(false);

        // Per ottenere DocumentCategory, join tramite PdfDocumentId
        var vectorWithCategory = await (
            from vd in _dbContext.VectorDocuments.AsNoTracking()
            join pdf in _dbContext.PdfDocuments.AsNoTracking()
                on vd.PdfDocumentId equals pdf.Id
            where vd.SharedGameId.HasValue
            select new
            {
                vd.SharedGameId,
                vd.ChunkCount,
                vd.IndexingStatus,
                vd.IndexedAt,
                pdf.DocumentCategory
            }
        ).ToListAsync(ct).ConfigureAwait(false);

        var grouped = vectorWithCategory
            .GroupBy(x => x.SharedGameId!.Value);

        int updated = 0;

        foreach (var group in grouped)
        {
            var gameId = group.Key;
            var items = group.ToList();

            var hasRulebook = items.Any(x =>
                x.DocumentCategory == "Rulebook" &&
                x.IndexingStatus == "completed");
            var hasErrata = items.Any(x =>
                x.DocumentCategory == "Errata" &&
                x.IndexingStatus == "completed");
            var hasFaq = items.Any(x =>
                x.DocumentCategory is "QuickStart" or "Reference" &&
                x.IndexingStatus == "completed");

            var completedChunks = items
                .Where(x => x.IndexingStatus == "completed")
                .Sum(x => x.ChunkCount);
            var totalChunks = items.Sum(x => x.ChunkCount);

            var lastIndexed = items.Max(x => x.IndexedAt);
            var daysSinceLastIndex = (int)(DateTime.UtcNow - lastIndexed).TotalDays;

            var score = ComputeScore(hasRulebook, hasErrata, hasFaq,
                completedChunks, totalChunks, daysSinceLastIndex);
            var level = ScoreToLevel(score);

            var key = $"KB:Coverage:{gameId}";
            var value = $"{{\"score\":{score},\"level\":\"{level}\"}}";

            var existing = await _configRepo
                .GetByKeyAsync(key, cancellationToken: ct)
                .ConfigureAwait(false);

            if (existing is null)
            {
                await _configRepo.AddAsync(
                    SystemConfigurationAggregate.Create(key, value, "KB"),
                    ct).ConfigureAwait(false);
            }
            else
            {
                existing.UpdateValue(value);
                await _configRepo.UpdateAsync(existing, ct).ConfigureAwait(false);
            }

            updated++;
        }

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "KB coverage compute job completed: {Count} games updated", updated);

        context.Result = new { Success = true, GamesUpdated = updated };
    }

    /// <summary>
    /// Formula coverage: HasRulebook(50) + HasFAQ(15) + HasErrata(5)
    ///   + ChunkDensity(20, max a 200 chunk) + Freshness(10, entro 90gg).
    /// Metodo internal per testabilità.
    /// </summary>
    internal static int ComputeScore(
        bool hasRulebook, bool hasErrata, bool hasFaq,
        int completedChunks, int totalChunks, int daysSinceLastIndex)
    {
        int score = 0;
        if (hasRulebook) score += 50;
        if (hasFaq)      score += 15;
        if (hasErrata)   score += 5;

        // Densità: 0-20 pts proporzionale (saturazione a 200 chunk)
        if (totalChunks > 0)
        {
            var density = Math.Min(completedChunks / 200.0, 1.0);
            score += (int)Math.Round(density * 20);
        }

        // Freschezza: 10 pts se indicizzato negli ultimi 90 giorni
        if (daysSinceLastIndex <= 90) score += 10;

        return Math.Min(score, 100);
    }

    internal static string ScoreToLevel(int score) => score switch
    {
        >= 75 => "Complete",
        >= 50 => "Standard",
        >= 25 => "Basic",
        _     => "None"
    };
}
```

> **Nota implementativa:** `_dbContext.PdfDocuments` è il DbSet del DocumentProcessing BC. Verifica che esista in `MeepleAiDbContext.cs`. Se il campo `DocumentCategory` è un enum, convertilo a stringa con `.ToString()` nella query LINQ.

- [ ] **Step 4: Esegui test — verifica passaggio**

```bash
cd apps/api
dotnet test tests/Api.Tests/Api.Tests.csproj \
  --filter "FullyQualifiedName~KbCoverageComputeJobTests" \
  --nologo -v q
```
Expected: PASS (3 tests)

- [ ] **Step 5: Registra il job in DependencyInjection**

In `KnowledgeBaseServiceExtensions.cs`, all'interno del blocco `services.AddQuartz(q => { ... })`, aggiungi dopo gli altri job:

```csharp
// KB-05: Coverage score compute — runs daily at 2:00 AM UTC
q.AddJob<KbCoverageComputeJob>(opts => opts
    .WithIdentity("kb-coverage-compute-job", "knowledge-base")
    .StoreDurably(true));

q.AddTrigger(opts => opts
    .ForJob("kb-coverage-compute-job", "knowledge-base")
    .WithIdentity("kb-coverage-compute-trigger", "knowledge-base")
    .WithCronSchedule("0 0 2 * * ?")
    .WithDescription("Calcola daily il coverage score KB per ogni gioco"));
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Scheduling/KbCoverageComputeJob.cs
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/Scheduling/KbCoverageComputeJobTests.cs
git commit -m "feat(kb): KB-05 coverage score batch job (Quartz, SystemConfiguration)"
```

---

## Task 2: KB-01 + KB-02 — Admin per-game KB backend

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAdminGameKbDocuments/GetAdminGameKbDocumentsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAdminGameKbDocuments/GetAdminGameKbDocumentsQueryHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/RemoveDocumentFromKb/RemoveDocumentFromKbCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/RemoveDocumentFromKb/RemoveDocumentFromKbCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/RemoveDocumentFromKb/RemoveDocumentFromKbCommandValidator.cs`
- Create: `apps/api/src/Api/Routing/AdminGameKbEndpoints.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetAdminGameKbDocumentsQueryHandlerTests.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Commands/RemoveDocumentFromKbCommandHandlerTests.cs`

- [ ] **Step 1: Scrivi test fallente — query handler**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetAdminGameKbDocumentsQueryHandlerTests.cs
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminGameKbDocuments;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetAdminGameKbDocumentsQueryHandlerTests
{
    private readonly IVectorDocumentRepository _repo = Substitute.For<IVectorDocumentRepository>();
    private readonly GetAdminGameKbDocumentsQueryHandler _sut;

    public GetAdminGameKbDocumentsQueryHandlerTests()
    {
        _sut = new GetAdminGameKbDocumentsQueryHandler(_repo);
    }

    [Fact]
    public async Task Handle_GameWithNoDocuments_ReturnsEmptyList()
    {
        var gameId = Guid.NewGuid();
        _repo.GetByGameIdAsync(gameId, Arg.Any<CancellationToken>())
             .Returns(new List<VectorDocument>());

        var result = await _sut.Handle(
            new GetAdminGameKbDocumentsQuery(gameId), CancellationToken.None);

        Assert.Empty(result.Documents);
        Assert.Equal(gameId, result.GameId);
    }

    [Fact]
    public async Task Handle_GameWithDocuments_ReturnsMappedDtos()
    {
        var gameId = Guid.NewGuid();
        var doc = VectorDocumentTestBuilder.Create(gameId);
        _repo.GetByGameIdAsync(gameId, Arg.Any<CancellationToken>())
             .Returns(new List<VectorDocument> { doc });

        var result = await _sut.Handle(
            new GetAdminGameKbDocumentsQuery(gameId), CancellationToken.None);

        Assert.Single(result.Documents);
        Assert.Equal(doc.Id, result.Documents[0].Id);
    }
}
```

> **Nota:** `VectorDocumentTestBuilder` è una classe helper di test che crea un `VectorDocument` via costruttore pubblico o factory. Se non esiste, creala inline con i campi minimi.

- [ ] **Step 2: Implementa Query + DTO + Handler**

```csharp
// GetAdminGameKbDocumentsQuery.cs
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminGameKbDocuments;

internal sealed record GetAdminGameKbDocumentsQuery(Guid GameId)
    : IQuery<AdminGameKbDocumentsDto>;

internal sealed record AdminGameKbDocumentsDto(
    Guid GameId,
    List<AdminKbDocumentItemDto> Documents);

internal sealed record AdminKbDocumentItemDto(
    Guid Id,
    Guid PdfDocumentId,
    string Language,
    int ChunkCount,
    string IndexingStatus,
    DateTime IndexedAt,
    Guid? SharedGameId);
```

```csharp
// GetAdminGameKbDocumentsQueryHandler.cs
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminGameKbDocuments;

internal sealed class GetAdminGameKbDocumentsQueryHandler
    : IQueryHandler<GetAdminGameKbDocumentsQuery, AdminGameKbDocumentsDto>
{
    private readonly IVectorDocumentRepository _repo;

    public GetAdminGameKbDocumentsQueryHandler(IVectorDocumentRepository repo)
        => _repo = repo ?? throw new ArgumentNullException(nameof(repo));

    public async Task<AdminGameKbDocumentsDto> Handle(
        GetAdminGameKbDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var docs = await _repo
            .GetByGameIdAsync(query.GameId, cancellationToken)
            .ConfigureAwait(false);

        var dtos = docs.Select(d => new AdminKbDocumentItemDto(
            Id:             d.Id,
            PdfDocumentId:  d.PdfDocumentId,
            Language:       d.Language,
            ChunkCount:     d.TotalChunks,
            IndexingStatus: d.IndexingStatus ?? "unknown",
            IndexedAt:      d.IndexedAt,
            SharedGameId:   d.SharedGameId
        )).ToList();

        return new AdminGameKbDocumentsDto(query.GameId, dtos);
    }
}
```

> **Nota:** `VectorDocument.IndexingStatus` potrebbe non essere esposta nella entity del dominio. Se non c'è, accedila tramite la proprietà `Metadata` (JSON) oppure aggiungila al dominio. Verifica prima nella domain entity.

- [ ] **Step 3: Scrivi test fallente — remove command**

```csharp
// RemoveDocumentFromKbCommandHandlerTests.cs
using Api.BoundedContexts.KnowledgeBase.Application.Commands.RemoveDocumentFromKb;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Domain.Exceptions;
using NSubstitute;
using Xunit;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class RemoveDocumentFromKbCommandHandlerTests
{
    private readonly IVectorDocumentRepository _repo = Substitute.For<IVectorDocumentRepository>();
    private readonly RemoveDocumentFromKbCommandHandler _sut;

    public RemoveDocumentFromKbCommandHandlerTests()
        => _sut = new RemoveDocumentFromKbCommandHandler(_repo);

    [Fact]
    public async Task Handle_DocumentNotFound_ThrowsNotFoundException()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns((VectorDocument?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.Handle(
                new RemoveDocumentFromKbCommand(Guid.NewGuid(), Guid.NewGuid()),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_DocumentBelongsToGame_DeletesDocument()
    {
        var gameId = Guid.NewGuid();
        var doc = VectorDocumentTestBuilder.Create(gameId);
        _repo.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);

        await _sut.Handle(new RemoveDocumentFromKbCommand(doc.Id, gameId), CancellationToken.None);

        await _repo.Received(1).DeleteAsync(doc.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DocumentBelongsToDifferentGame_ThrowsNotFoundException()
    {
        var doc = VectorDocumentTestBuilder.Create(Guid.NewGuid());
        _repo.GetByIdAsync(doc.Id, Arg.Any<CancellationToken>()).Returns(doc);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _sut.Handle(
                new RemoveDocumentFromKbCommand(doc.Id, Guid.NewGuid()),
                CancellationToken.None));
    }
}
```

- [ ] **Step 4: Implementa Command + Validator + Handler**

```csharp
// RemoveDocumentFromKbCommand.cs
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.RemoveDocumentFromKb;

internal sealed record RemoveDocumentFromKbCommand(
    Guid VectorDocumentId,
    Guid GameId) : IRequest;
```

```csharp
// RemoveDocumentFromKbCommandValidator.cs
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.RemoveDocumentFromKb;

internal sealed class RemoveDocumentFromKbCommandValidator
    : AbstractValidator<RemoveDocumentFromKbCommand>
{
    public RemoveDocumentFromKbCommandValidator()
    {
        RuleFor(x => x.VectorDocumentId).NotEmpty();
        RuleFor(x => x.GameId).NotEmpty();
    }
}
```

```csharp
// RemoveDocumentFromKbCommandHandler.cs
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Domain.Exceptions;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.RemoveDocumentFromKb;

internal sealed class RemoveDocumentFromKbCommandHandler
    : IRequestHandler<RemoveDocumentFromKbCommand>
{
    private readonly IVectorDocumentRepository _repo;

    public RemoveDocumentFromKbCommandHandler(IVectorDocumentRepository repo)
        => _repo = repo ?? throw new ArgumentNullException(nameof(repo));

    public async Task Handle(
        RemoveDocumentFromKbCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var doc = await _repo
            .GetByIdAsync(command.VectorDocumentId, cancellationToken)
            .ConfigureAwait(false);

        if (doc is null || doc.GameId != command.GameId)
            throw new NotFoundException(
                $"VectorDocument {command.VectorDocumentId} not found for game {command.GameId}");

        await _repo.DeleteAsync(command.VectorDocumentId, cancellationToken)
            .ConfigureAwait(false);
    }
}
```

- [ ] **Step 5: Aggiungi endpoint admin**

```csharp
// apps/api/src/Api/Routing/AdminGameKbEndpoints.cs
using Api.BoundedContexts.KnowledgeBase.Application.Commands.RemoveDocumentFromKb;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminGameKbDocuments;
using Api.Filters;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints per gestione KB per-gioco.
/// KB-01, KB-02, KB-10.
/// </summary>
internal static class AdminGameKbEndpoints
{
    public static RouteGroupBuilder MapAdminGameKbEndpoints(this RouteGroupBuilder group)
    {
        var g = group.MapGroup("/admin/kb/games")
            .WithTags("Admin", "KnowledgeBase")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // KB-01: GET lista documenti per-gioco
        g.MapGet("/{gameId:guid}/documents", async (
            Guid gameId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator
                .Send(new GetAdminGameKbDocumentsQuery(gameId), ct)
                .ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetAdminGameKbDocuments")
        .WithSummary("Lista documenti KB indicizzati per un gioco (admin)");

        // KB-02: DELETE rimozione documento KB
        g.MapDelete("/{gameId:guid}/documents/{vectorDocId:guid}", async (
            Guid gameId,
            Guid vectorDocId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator
                .Send(new RemoveDocumentFromKbCommand(vectorDocId, gameId), ct)
                .ConfigureAwait(false);
            return Results.NoContent();
        })
        .WithName("RemoveDocumentFromKb")
        .WithSummary("Rimuove un documento dalla KB di un gioco (admin)");

        return group;
    }
}
```

Registra in `Program.cs` o `EndpointRegistration.cs` il nuovo gruppo:
```csharp
app.MapGroup("/api/v1").MapAdminGameKbEndpoints();
```

- [ ] **Step 6: Esegui test**

```bash
cd apps/api
dotnet test tests/Api.Tests/Api.Tests.csproj \
  --filter "FullyQualifiedName~GetAdminGameKbDocumentsQueryHandlerTests|FullyQualifiedName~RemoveDocumentFromKbCommandHandlerTests" \
  --nologo -v q
```
Expected: PASS

- [ ] **Step 7: Build verifica**

```bash
cd apps/api/src/Api && dotnet build --nologo -v q
```
Expected: 0 errors

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAdminGameKbDocuments/
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/RemoveDocumentFromKb/
git add apps/api/src/Api/Routing/AdminGameKbEndpoints.cs
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/
git commit -m "feat(kb): KB-01 admin per-game KB documents query + KB-02 remove document"
```

---

## Task 3: KB-06 — KbUserFeedback entity + migration + endpoint

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/KbUserFeedback.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Repositories/IKbUserFeedbackRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Configurations/KbUserFeedbackConfiguration.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/KbUserFeedbackRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/SubmitKbFeedback/SubmitKbFeedbackCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/SubmitKbFeedback/SubmitKbFeedbackCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/SubmitKbFeedback/SubmitKbFeedbackCommandValidator.cs`
- Create: `apps/api/src/Api/Routing/UserGameKbEndpoints.cs`
- Modify: `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Commands/SubmitKbFeedbackCommandHandlerTests.cs`

- [ ] **Step 1: Scrivi il test fallente**

```csharp
// SubmitKbFeedbackCommandHandlerTests.cs
using Api.BoundedContexts.KnowledgeBase.Application.Commands.SubmitKbFeedback;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using NSubstitute;
using Xunit;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class SubmitKbFeedbackCommandHandlerTests
{
    private readonly IKbUserFeedbackRepository _repo = Substitute.For<IKbUserFeedbackRepository>();
    private readonly SubmitKbFeedbackCommandHandler _sut;

    public SubmitKbFeedbackCommandHandlerTests()
        => _sut = new SubmitKbFeedbackCommandHandler(_repo);

    [Fact]
    public async Task Handle_ValidFeedback_SavesFeedback()
    {
        var cmd = new SubmitKbFeedbackCommand(
            UserId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            ChatSessionId: Guid.NewGuid(),
            MessageId: Guid.NewGuid(),
            Outcome: "helpful",
            Comment: null);

        await _sut.Handle(cmd, CancellationToken.None);

        await _repo.Received(1).AddAsync(
            Arg.Is<KbUserFeedback>(f =>
                f.Outcome == "helpful" &&
                f.GameId == cmd.GameId),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("helpful")]
    [InlineData("not_helpful")]
    public async Task Handle_ValidOutcomes_DoesNotThrow(string outcome)
    {
        var cmd = new SubmitKbFeedbackCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            outcome, null);

        await _sut.Handle(cmd, CancellationToken.None);

        await _repo.Received(1).AddAsync(Arg.Any<KbUserFeedback>(), Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Implementa la domain entity**

```csharp
// KbUserFeedback.cs
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Feedback utente su una risposta chat KB.
/// KB-06: User Feedback on Chat Response.
/// </summary>
public sealed class KbUserFeedback : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public Guid GameId { get; private set; }
    public Guid ChatSessionId { get; private set; }
    public Guid MessageId { get; private set; }

    /// <summary>"helpful" | "not_helpful"</summary>
    public string Outcome { get; private set; } = string.Empty;

    /// <summary>Commento opzionale (max 500 char).</summary>
    public string? Comment { get; private set; }

    public DateTime CreatedAt { get; private set; }

    // EF Core
    private KbUserFeedback() : base() { }

    public static KbUserFeedback Create(
        Guid userId,
        Guid gameId,
        Guid chatSessionId,
        Guid messageId,
        string outcome,
        string? comment)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (outcome is not "helpful" and not "not_helpful")
            throw new ArgumentException("Outcome must be 'helpful' or 'not_helpful'", nameof(outcome));

        return new KbUserFeedback
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = gameId,
            ChatSessionId = chatSessionId,
            MessageId = messageId,
            Outcome = outcome,
            Comment = comment?.Length > 500 ? comment[..500] : comment,
            CreatedAt = DateTime.UtcNow
        };
    }
}
```

- [ ] **Step 3: Implementa repository interface + EF config**

```csharp
// IKbUserFeedbackRepository.cs
namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

internal interface IKbUserFeedbackRepository
{
    Task AddAsync(KbUserFeedback feedback, CancellationToken cancellationToken = default);

    Task<List<KbUserFeedback>> GetByGameIdAsync(
        Guid gameId,
        string? outcomeFilter,
        DateTime? fromDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> CountByGameIdAsync(
        Guid gameId,
        string? outcomeFilter,
        DateTime? fromDate,
        CancellationToken cancellationToken = default);
}
```

```csharp
// KbUserFeedbackConfiguration.cs
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Configurations;

internal sealed class KbUserFeedbackConfiguration : IEntityTypeConfiguration<KbUserFeedback>
{
    public void Configure(EntityTypeBuilder<KbUserFeedback> builder)
    {
        builder.ToTable("kb_user_feedback", "knowledge_base");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.GameId).HasColumnName("game_id").IsRequired();
        builder.Property(e => e.ChatSessionId).HasColumnName("chat_session_id").IsRequired();
        builder.Property(e => e.MessageId).HasColumnName("message_id").IsRequired();
        builder.Property(e => e.Outcome).HasColumnName("outcome").HasMaxLength(20).IsRequired();
        builder.Property(e => e.Comment).HasColumnName("comment").HasMaxLength(500);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(e => e.GameId).HasDatabaseName("IX_kb_user_feedback_game_id");
        builder.HasIndex(e => e.UserId).HasDatabaseName("IX_kb_user_feedback_user_id");
        builder.HasIndex(e => e.MessageId).HasDatabaseName("IX_kb_user_feedback_message_id");
        builder.HasIndex(e => e.CreatedAt).HasDatabaseName("IX_kb_user_feedback_created_at");

        builder.Ignore(e => e.DomainEvents);
    }
}
```

- [ ] **Step 4: Aggiungi DbSet + migrazione**

In `MeepleAiDbContext.cs`, aggiungi:
```csharp
public DbSet<KbUserFeedback> KbUserFeedbacks => Set<KbUserFeedback>();
```

Crea la migrazione:
```bash
cd apps/api/src/Api
dotnet ef migrations add AddKbUserFeedback --nologo
```

Verifica il file generato: deve creare `knowledge_base.kb_user_feedback` con le colonne corrette.

- [ ] **Step 5: Implementa command + handler**

```csharp
// SubmitKbFeedbackCommand.cs
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.SubmitKbFeedback;

internal sealed record SubmitKbFeedbackCommand(
    Guid UserId,
    Guid GameId,
    Guid ChatSessionId,
    Guid MessageId,
    string Outcome,
    string? Comment) : IRequest;
```

```csharp
// SubmitKbFeedbackCommandValidator.cs
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.SubmitKbFeedback;

internal sealed class SubmitKbFeedbackCommandValidator
    : AbstractValidator<SubmitKbFeedbackCommand>
{
    public SubmitKbFeedbackCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.ChatSessionId).NotEmpty();
        RuleFor(x => x.MessageId).NotEmpty();
        RuleFor(x => x.Outcome)
            .NotEmpty()
            .Must(o => o is "helpful" or "not_helpful")
            .WithMessage("Outcome deve essere 'helpful' o 'not_helpful'");
        RuleFor(x => x.Comment).MaximumLength(500).When(x => x.Comment is not null);
    }
}
```

```csharp
// SubmitKbFeedbackCommandHandler.cs
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.SubmitKbFeedback;

internal sealed class SubmitKbFeedbackCommandHandler
    : IRequestHandler<SubmitKbFeedbackCommand>
{
    private readonly IKbUserFeedbackRepository _repo;

    public SubmitKbFeedbackCommandHandler(IKbUserFeedbackRepository repo)
        => _repo = repo ?? throw new ArgumentNullException(nameof(repo));

    public async Task Handle(
        SubmitKbFeedbackCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var feedback = KbUserFeedback.Create(
            command.UserId,
            command.GameId,
            command.ChatSessionId,
            command.MessageId,
            command.Outcome,
            command.Comment);

        await _repo.AddAsync(feedback, cancellationToken).ConfigureAwait(false);
    }
}
```

- [ ] **Step 6: Aggiungi endpoint utente**

```csharp
// apps/api/src/Api/Routing/UserGameKbEndpoints.cs
using Api.BoundedContexts.KnowledgeBase.Application.Commands.SubmitKbFeedback;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetUserGameKbStatus;
using Api.Extensions;
using Api.Infrastructure;
using Api.Middleware;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Endpoint utente per stato KB gioco e feedback.
/// KB-03, KB-06.
/// </summary>
internal static class UserGameKbEndpoints
{
    public static RouteGroupBuilder MapUserGameKbEndpoints(this RouteGroupBuilder group)
    {
        // KB-03: stato KB per il gioco
        group.MapGet("/games/{gameId:guid}/knowledge-base", async (
            Guid gameId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator
                .Send(new GetUserGameKbStatusQuery(gameId), ct)
                .ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireSession()
        .WithName("GetUserGameKbStatus")
        .WithTags("Games", "KnowledgeBase")
        .WithSummary("Stato KB indicizzazione per un gioco");

        // KB-06: feedback utente su risposta chat
        group.MapPost("/games/{gameId:guid}/knowledge-base/feedback", async (
            Guid gameId,
            [FromBody] KbFeedbackRequest request,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)httpContext.Items[nameof(SessionStatusDto)]!;
            var cmd = new SubmitKbFeedbackCommand(
                UserId:        session.User!.Id,
                GameId:        gameId,
                ChatSessionId: request.ChatSessionId,
                MessageId:     request.MessageId,
                Outcome:       request.Outcome,
                Comment:       request.Comment);

            await mediator.Send(cmd, ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .RequireSession()
        .WithName("SubmitKbFeedback")
        .WithTags("Games", "KnowledgeBase")
        .WithSummary("Invia feedback utente su una risposta chat KB");

        return group;
    }

    private sealed record KbFeedbackRequest(
        Guid ChatSessionId,
        Guid MessageId,
        string Outcome,
        string? Comment);
}
```

Registra in `Program.cs`:
```csharp
app.MapGroup("/api/v1").MapUserGameKbEndpoints();
```

- [ ] **Step 7: Implementa KbUserFeedbackRepository**

```csharp
// KbUserFeedbackRepository.cs
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

internal sealed class KbUserFeedbackRepository : IKbUserFeedbackRepository
{
    private readonly MeepleAiDbContext _db;

    public KbUserFeedbackRepository(MeepleAiDbContext db)
        => _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task AddAsync(KbUserFeedback feedback, CancellationToken cancellationToken = default)
    {
        await _db.KbUserFeedbacks.AddAsync(feedback, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<KbUserFeedback>> GetByGameIdAsync(
        Guid gameId, string? outcomeFilter, DateTime? fromDate,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _db.KbUserFeedbacks.AsNoTracking().Where(f => f.GameId == gameId);
        if (outcomeFilter is not null) query = query.Where(f => f.Outcome == outcomeFilter);
        if (fromDate.HasValue)        query = query.Where(f => f.CreatedAt >= fromDate.Value);

        return await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<int> CountByGameIdAsync(
        Guid gameId, string? outcomeFilter, DateTime? fromDate,
        CancellationToken cancellationToken = default)
    {
        var query = _db.KbUserFeedbacks.AsNoTracking().Where(f => f.GameId == gameId);
        if (outcomeFilter is not null) query = query.Where(f => f.Outcome == outcomeFilter);
        if (fromDate.HasValue)        query = query.Where(f => f.CreatedAt >= fromDate.Value);
        return query.CountAsync(cancellationToken);
    }
}
```

Registra in `KnowledgeBaseServiceExtensions.cs`:
```csharp
services.AddScoped<IKbUserFeedbackRepository, KbUserFeedbackRepository>();
```

- [ ] **Step 8: Esegui test + build**

```bash
cd apps/api
dotnet test tests/Api.Tests/Api.Tests.csproj \
  --filter "FullyQualifiedName~SubmitKbFeedbackCommandHandlerTests" --nologo -v q
cd src/Api && dotnet build --nologo -v q
```
Expected: PASS, 0 errors

- [ ] **Step 9: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Entities/KbUserFeedback.cs
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Repositories/IKbUserFeedbackRepository.cs
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Configurations/KbUserFeedbackConfiguration.cs
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/KbUserFeedbackRepository.cs
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/SubmitKbFeedback/
git add apps/api/src/Api/Routing/UserGameKbEndpoints.cs
git add apps/api/src/Api/Infrastructure/Migrations/
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Commands/SubmitKbFeedbackCommandHandlerTests.cs
git commit -m "feat(kb): KB-06 KbUserFeedback entity, migration, endpoint POST /games/{id}/knowledge-base/feedback"
```

---

## Task 4: KB-03 — GetUserGameKbStatus query

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetUserGameKbStatus/GetUserGameKbStatusQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetUserGameKbStatus/GetUserGameKbStatusQueryHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetUserGameKbStatusQueryHandlerTests.cs`

- [ ] **Step 1: Scrivi test fallente**

```csharp
// GetUserGameKbStatusQueryHandlerTests.cs
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetUserGameKbStatus;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using NSubstitute;
using Xunit;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetUserGameKbStatusQueryHandlerTests
{
    private readonly IVectorDocumentRepository _vectorRepo = Substitute.For<IVectorDocumentRepository>();
    private readonly ISystemConfigurationRepository _configRepo = Substitute.For<ISystemConfigurationRepository>();
    private readonly GetUserGameKbStatusQueryHandler _sut;

    public GetUserGameKbStatusQueryHandlerTests()
        => _sut = new GetUserGameKbStatusQueryHandler(_vectorRepo, _configRepo);

    [Fact]
    public async Task Handle_NoDocuments_ReturnsNotIndexed()
    {
        var gameId = Guid.NewGuid();
        _vectorRepo.GetByGameIdAsync(gameId, Arg.Any<CancellationToken>())
                   .Returns(new List<VectorDocument>());

        var result = await _sut.Handle(
            new GetUserGameKbStatusQuery(gameId), CancellationToken.None);

        Assert.False(result.IsIndexed);
        Assert.Equal(0, result.DocumentCount);
        Assert.Equal("None", result.CoverageLevel);
    }

    [Fact]
    public async Task Handle_HasDocuments_ReturnsIndexed()
    {
        var gameId = Guid.NewGuid();
        _vectorRepo.GetByGameIdAsync(gameId, Arg.Any<CancellationToken>())
                   .Returns(new List<VectorDocument> { VectorDocumentTestBuilder.Create(gameId) });

        var result = await _sut.Handle(
            new GetUserGameKbStatusQuery(gameId), CancellationToken.None);

        Assert.True(result.IsIndexed);
        Assert.Equal(1, result.DocumentCount);
    }
}
```

- [ ] **Step 2: Implementa Query + DTO + Handler**

```csharp
// GetUserGameKbStatusQuery.cs
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetUserGameKbStatus;

internal sealed record GetUserGameKbStatusQuery(Guid GameId)
    : IQuery<UserGameKbStatusDto>;

internal sealed record UserGameKbStatusDto(
    Guid GameId,
    bool IsIndexed,
    int DocumentCount,
    int CoverageScore,
    string CoverageLevel,
    List<string> SuggestedQuestions);
```

```csharp
// GetUserGameKbStatusQueryHandler.cs
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetUserGameKbStatus;

internal sealed class GetUserGameKbStatusQueryHandler
    : IQueryHandler<GetUserGameKbStatusQuery, UserGameKbStatusDto>
{
    private readonly IVectorDocumentRepository _vectorRepo;
    private readonly ISystemConfigurationRepository _configRepo;

    public GetUserGameKbStatusQueryHandler(
        IVectorDocumentRepository vectorRepo,
        ISystemConfigurationRepository configRepo)
    {
        _vectorRepo = vectorRepo ?? throw new ArgumentNullException(nameof(vectorRepo));
        _configRepo = configRepo ?? throw new ArgumentNullException(nameof(configRepo));
    }

    public async Task<UserGameKbStatusDto> Handle(
        GetUserGameKbStatusQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var docs = await _vectorRepo
            .GetByGameIdAsync(query.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (docs.Count == 0)
        {
            return new UserGameKbStatusDto(
                query.GameId, false, 0, 0, "None", new List<string>());
        }

        // Leggi coverage score da SystemConfiguration
        var coverageKey = $"KB:Coverage:{query.GameId}";
        var coverageConfig = await _configRepo
            .GetByKeyAsync(coverageKey, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        int coverageScore = 0;
        string coverageLevel = "None";
        if (coverageConfig is not null)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<JsonElement>(coverageConfig.Value);
                coverageScore = parsed.GetProperty("score").GetInt32();
                coverageLevel = parsed.GetProperty("level").GetString() ?? "None";
            }
            catch { /* usa valori default */ }
        }

        // Leggi domande suggerite
        var suggestKey = $"KB:SuggestedQuestions:{query.GameId}";
        var suggestConfig = await _configRepo
            .GetByKeyAsync(suggestKey, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var suggested = new List<string>();
        if (suggestConfig is not null)
        {
            try
            {
                suggested = JsonSerializer.Deserialize<List<string>>(suggestConfig.Value)
                            ?? new List<string>();
            }
            catch { /* usa lista vuota */ }
        }

        return new UserGameKbStatusDto(
            query.GameId,
            IsIndexed:        true,
            DocumentCount:    docs.Count,
            CoverageScore:    coverageScore,
            CoverageLevel:    coverageLevel,
            SuggestedQuestions: suggested);
    }
}
```

- [ ] **Step 3: Esegui test**

```bash
cd apps/api
dotnet test tests/Api.Tests/Api.Tests.csproj \
  --filter "FullyQualifiedName~GetUserGameKbStatusQueryHandlerTests" --nologo -v q
```
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetUserGameKbStatus/
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetUserGameKbStatusQueryHandlerTests.cs
git commit -m "feat(kb): KB-03 GetUserGameKbStatus query (coverage score + suggested questions)"
```

---

## Task 5: KB-09 — Suggested Questions Batch Job

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Scheduling/KbSuggestedQuestionsJob.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/Scheduling/KbSuggestedQuestionsJobTests.cs`

- [ ] **Step 1: Scrivi test fallente**

```csharp
// KbSuggestedQuestionsJobTests.cs
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;
using Xunit;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class KbSuggestedQuestionsJobTests
{
    [Fact]
    public void GenerateQuestions_ReturnsExactly5Questions()
    {
        var questions = KbSuggestedQuestionsJob.GenerateTemplateQuestions("Puerto Rico");
        Assert.Equal(5, questions.Count);
    }

    [Fact]
    public void GenerateQuestions_ContainsGameNameInQuestions()
    {
        var questions = KbSuggestedQuestionsJob.GenerateTemplateQuestions("Catan");
        Assert.All(questions, q => Assert.Contains("Catan", q));
    }

    [Fact]
    public void GenerateQuestions_NullGameName_UsesPlaceholder()
    {
        var questions = KbSuggestedQuestionsJob.GenerateTemplateQuestions(null);
        Assert.Equal(5, questions.Count);
    }
}
```

- [ ] **Step 2: Implementa il job**

```csharp
// KbSuggestedQuestionsJob.cs
using System.Text.Json;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;

/// <summary>
/// Quartz job: genera domande suggerite template per ogni gioco indicizzato
/// e le salva in SystemConfiguration con chiave "KB:SuggestedQuestions:{gameId}".
/// KB-09: Suggested Questions (pre-calculated).
/// </summary>
[DisallowConcurrentExecution]
internal sealed class KbSuggestedQuestionsJob : IJob
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ISystemConfigurationRepository _configRepo;
    private readonly ILogger<KbSuggestedQuestionsJob> _logger;

    public KbSuggestedQuestionsJob(
        MeepleAiDbContext dbContext,
        ISystemConfigurationRepository configRepo,
        ILogger<KbSuggestedQuestionsJob> logger)
    {
        _dbContext = dbContext;
        _configRepo = configRepo;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;

        var gameIds = await _dbContext.VectorDocuments
            .AsNoTracking()
            .Where(v => v.SharedGameId.HasValue &&
                        v.IndexingStatus == "completed")
            .Select(v => v.SharedGameId!.Value)
            .Distinct()
            .ToListAsync(ct).ConfigureAwait(false);

        var gameTitles = await _dbContext.SharedGames
            .AsNoTracking()
            .Where(sg => gameIds.Contains(sg.Id))
            .Select(sg => new { sg.Id, sg.Title })
            .ToDictionaryAsync(sg => sg.Id, sg => sg.Title, ct)
            .ConfigureAwait(false);

        int updated = 0;
        foreach (var gameId in gameIds)
        {
            gameTitles.TryGetValue(gameId, out var title);
            var questions = GenerateTemplateQuestions(title);
            var json = JsonSerializer.Serialize(questions);
            var key = $"KB:SuggestedQuestions:{gameId}";

            var existing = await _configRepo
                .GetByKeyAsync(key, cancellationToken: ct)
                .ConfigureAwait(false);

            if (existing is null)
                await _configRepo.AddAsync(
                    SystemConfigurationAggregate.Create(key, json, "KB"), ct)
                    .ConfigureAwait(false);
            else
            {
                existing.UpdateValue(json);
                await _configRepo.UpdateAsync(existing, ct).ConfigureAwait(false);
            }

            updated++;
        }

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("KB suggested questions job: {Count} games updated", updated);
        context.Result = new { Success = true, GamesUpdated = updated };
    }

    /// <summary>
    /// Genera 5 domande template basate sul nome del gioco.
    /// Internal per testabilità.
    /// </summary>
    internal static List<string> GenerateTemplateQuestions(string? gameName)
    {
        var name = string.IsNullOrWhiteSpace(gameName) ? "questo gioco" : gameName;
        return new List<string>
        {
            $"Come si svolge un turno in {name}?",
            $"Quali sono le regole di base di {name}?",
            $"Come si vince a {name}?",
            $"Quanti giocatori possono giocare a {name}?",
            $"Quanto dura una partita a {name}?"
        };
    }
}
```

- [ ] **Step 3: Registra il job**

In `KnowledgeBaseServiceExtensions.cs`, dentro `services.AddQuartz(q => { ... })`:

```csharp
// KB-09: Suggested questions — runs daily at 3:30 AM UTC
q.AddJob<KbSuggestedQuestionsJob>(opts => opts
    .WithIdentity("kb-suggested-questions-job", "knowledge-base")
    .StoreDurably(true));

q.AddTrigger(opts => opts
    .ForJob("kb-suggested-questions-job", "knowledge-base")
    .WithIdentity("kb-suggested-questions-trigger", "knowledge-base")
    .WithCronSchedule("0 30 3 * * ?")
    .WithDescription("Genera domande suggerite pre-calcolate per ogni gioco KB"));
```

- [ ] **Step 4: Esegui test**

```bash
cd apps/api
dotnet test tests/Api.Tests/Api.Tests.csproj \
  --filter "FullyQualifiedName~KbSuggestedQuestionsJobTests" --nologo -v q
```
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Scheduling/KbSuggestedQuestionsJob.cs
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/Scheduling/KbSuggestedQuestionsJobTests.cs
git commit -m "feat(kb): KB-09 suggested questions batch job (pre-calculated, SystemConfiguration)"
```

---

## Task 6: KB-08 — Admin Feedback Review backend

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAdminKbFeedback/GetAdminKbFeedbackQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAdminKbFeedback/GetAdminKbFeedbackQueryHandler.cs`
- Modify: `apps/api/src/Api/Routing/AdminGameKbEndpoints.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetAdminKbFeedbackQueryHandlerTests.cs`

- [ ] **Step 1: Scrivi test fallente**

```csharp
// GetAdminKbFeedbackQueryHandlerTests.cs
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminKbFeedback;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using NSubstitute;
using Xunit;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetAdminKbFeedbackQueryHandlerTests
{
    private readonly IKbUserFeedbackRepository _repo = Substitute.For<IKbUserFeedbackRepository>();
    private readonly GetAdminKbFeedbackQueryHandler _sut;

    public GetAdminKbFeedbackQueryHandlerTests()
        => _sut = new GetAdminKbFeedbackQueryHandler(_repo);

    [Fact]
    public async Task Handle_ReturnsPaginatedFeedback()
    {
        var gameId = Guid.NewGuid();
        _repo.GetByGameIdAsync(gameId, null, null, 1, 20, Arg.Any<CancellationToken>())
             .Returns(new List<KbUserFeedback>());
        _repo.CountByGameIdAsync(gameId, null, null, Arg.Any<CancellationToken>())
             .Returns(0);

        var result = await _sut.Handle(
            new GetAdminKbFeedbackQuery(gameId, null, null, 1, 20),
            CancellationToken.None);

        Assert.Equal(0, result.Total);
        Assert.Empty(result.Items);
    }
}
```

- [ ] **Step 2: Implementa Query + Handler**

```csharp
// GetAdminKbFeedbackQuery.cs
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminKbFeedback;

internal sealed record GetAdminKbFeedbackQuery(
    Guid GameId,
    string? OutcomeFilter,
    DateTime? FromDate,
    int Page,
    int PageSize) : IQuery<AdminKbFeedbackDto>;

internal sealed record AdminKbFeedbackDto(
    int Total,
    List<AdminKbFeedbackItemDto> Items);

internal sealed record AdminKbFeedbackItemDto(
    Guid Id,
    Guid UserId,
    Guid GameId,
    Guid ChatSessionId,
    Guid MessageId,
    string Outcome,
    string? Comment,
    DateTime CreatedAt);
```

```csharp
// GetAdminKbFeedbackQueryHandler.cs
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminKbFeedback;

internal sealed class GetAdminKbFeedbackQueryHandler
    : IQueryHandler<GetAdminKbFeedbackQuery, AdminKbFeedbackDto>
{
    private readonly IKbUserFeedbackRepository _repo;

    public GetAdminKbFeedbackQueryHandler(IKbUserFeedbackRepository repo)
        => _repo = repo ?? throw new ArgumentNullException(nameof(repo));

    public async Task<AdminKbFeedbackDto> Handle(
        GetAdminKbFeedbackQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var page = Math.Max(1, query.Page);

        var items = await _repo.GetByGameIdAsync(
            query.GameId, query.OutcomeFilter, query.FromDate,
            page, pageSize, cancellationToken).ConfigureAwait(false);

        var total = await _repo.CountByGameIdAsync(
            query.GameId, query.OutcomeFilter, query.FromDate,
            cancellationToken).ConfigureAwait(false);

        var dtos = items.Select(f => new AdminKbFeedbackItemDto(
            f.Id, f.UserId, f.GameId, f.ChatSessionId, f.MessageId,
            f.Outcome, f.Comment, f.CreatedAt)).ToList();

        return new AdminKbFeedbackDto(total, dtos);
    }
}
```

- [ ] **Step 3: Aggiungi endpoint admin in `AdminGameKbEndpoints.cs`**

Aggiungi dentro `MapAdminGameKbEndpoints()`:

```csharp
// KB-08: GET feedback per-gioco
g.MapGet("/{gameId:guid}/feedback", async (
    Guid gameId,
    string? outcome,
    DateTime? from,
    int? page,
    int? pageSize,
    IMediator mediator,
    CancellationToken ct) =>
{
    var result = await mediator.Send(new GetAdminKbFeedbackQuery(
        gameId, outcome, from,
        page ?? 1, pageSize ?? 20), ct).ConfigureAwait(false);
    return Results.Ok(result);
})
.WithName("GetAdminKbFeedback")
.WithSummary("Lista feedback utenti su risposte KB per-gioco (admin)");
```

- [ ] **Step 4: Esegui test + build**

```bash
cd apps/api
dotnet test tests/Api.Tests/Api.Tests.csproj \
  --filter "FullyQualifiedName~GetAdminKbFeedbackQueryHandlerTests" --nologo -v q
cd src/Api && dotnet build --nologo -v q
```
Expected: PASS, 0 errors

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetAdminKbFeedback/
git add apps/api/src/Api/Routing/AdminGameKbEndpoints.cs
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetAdminKbFeedbackQueryHandlerTests.cs
git commit -m "feat(kb): KB-08 admin feedback review endpoint GET /admin/kb/games/{id}/feedback"
```

---

## Task 7: KB-10 — Admin per-game KB settings

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetGameKbSettings/GetGameKbSettingsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetGameKbSettings/GetGameKbSettingsQueryHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/SetGameKbSettings/SetGameKbSettingsCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/SetGameKbSettings/SetGameKbSettingsCommandHandler.cs`
- Modify: `apps/api/src/Api/Routing/AdminGameKbEndpoints.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetGameKbSettingsQueryHandlerTests.cs`

- [ ] **Step 1: Implementa Query + Handler**

```csharp
// GetGameKbSettingsQuery.cs
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetGameKbSettings;

internal sealed record GetGameKbSettingsQuery(Guid GameId)
    : IQuery<GameKbSettingsDto>;

internal sealed record GameKbSettingsDto(
    Guid GameId,
    int? MaxChunksOverride,
    int? ChunkSizeOverride,
    bool? CacheEnabledOverride,
    string? LanguageOverride);
```

```csharp
// GetGameKbSettingsQueryHandler.cs
using System.Text.Json;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetGameKbSettings;

internal sealed class GetGameKbSettingsQueryHandler
    : IQueryHandler<GetGameKbSettingsQuery, GameKbSettingsDto>
{
    private readonly ISystemConfigurationRepository _configRepo;

    public GetGameKbSettingsQueryHandler(ISystemConfigurationRepository configRepo)
        => _configRepo = configRepo ?? throw new ArgumentNullException(nameof(configRepo));

    public async Task<GameKbSettingsDto> Handle(
        GetGameKbSettingsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var key = $"KB:Game:{query.GameId}:Settings";
        var config = await _configRepo
            .GetByKeyAsync(key, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (config is null)
            return new GameKbSettingsDto(query.GameId, null, null, null, null);

        try
        {
            var parsed = JsonSerializer.Deserialize<GameKbSettingsPayload>(config.Value)!;
            return new GameKbSettingsDto(
                query.GameId,
                parsed.MaxChunks,
                parsed.ChunkSize,
                parsed.CacheEnabled,
                parsed.Language);
        }
        catch
        {
            return new GameKbSettingsDto(query.GameId, null, null, null, null);
        }
    }

    private sealed record GameKbSettingsPayload(
        int? MaxChunks,
        int? ChunkSize,
        bool? CacheEnabled,
        string? Language);
}
```

```csharp
// SetGameKbSettingsCommand.cs
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.SetGameKbSettings;

internal sealed record SetGameKbSettingsCommand(
    Guid GameId,
    int? MaxChunks,
    int? ChunkSize,
    bool? CacheEnabled,
    string? Language) : IRequest;
```

```csharp
// SetGameKbSettingsCommandHandler.cs
using System.Text.Json;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.SetGameKbSettings;

internal sealed class SetGameKbSettingsCommandHandler
    : IRequestHandler<SetGameKbSettingsCommand>
{
    private readonly ISystemConfigurationRepository _configRepo;

    public SetGameKbSettingsCommandHandler(ISystemConfigurationRepository configRepo)
        => _configRepo = configRepo ?? throw new ArgumentNullException(nameof(configRepo));

    public async Task Handle(
        SetGameKbSettingsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var key = $"KB:Game:{command.GameId}:Settings";
        var value = JsonSerializer.Serialize(new
        {
            MaxChunks    = command.MaxChunks,
            ChunkSize    = command.ChunkSize,
            CacheEnabled = command.CacheEnabled,
            Language     = command.Language
        });

        var existing = await _configRepo
            .GetByKeyAsync(key, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
            await _configRepo.AddAsync(
                SystemConfigurationAggregate.Create(key, value, "KB"),
                cancellationToken).ConfigureAwait(false);
        else
        {
            existing.UpdateValue(value);
            await _configRepo.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        }
    }
}
```

- [ ] **Step 2: Aggiungi endpoint in `AdminGameKbEndpoints.cs`**

```csharp
// KB-10: GET impostazioni KB per-gioco
g.MapGet("/{gameId:guid}/settings", async (
    Guid gameId, IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator
        .Send(new GetGameKbSettingsQuery(gameId), ct)
        .ConfigureAwait(false);
    return Results.Ok(result);
})
.WithName("GetGameKbSettings")
.WithSummary("Impostazioni KB override per un gioco (admin)");

// KB-10: PUT impostazioni KB per-gioco
g.MapPut("/{gameId:guid}/settings", async (
    Guid gameId,
    [FromBody] SetGameKbSettingsRequest req,
    IMediator mediator,
    CancellationToken ct) =>
{
    await mediator.Send(new SetGameKbSettingsCommand(
        gameId, req.MaxChunks, req.ChunkSize, req.CacheEnabled, req.Language), ct)
        .ConfigureAwait(false);
    return Results.NoContent();
})
.WithName("SetGameKbSettings")
.WithSummary("Imposta override impostazioni KB per-gioco (admin)");
```

Aggiungi il record request in fondo al file `AdminGameKbEndpoints.cs`:
```csharp
private sealed record SetGameKbSettingsRequest(
    int? MaxChunks,
    int? ChunkSize,
    bool? CacheEnabled,
    string? Language);
```

- [ ] **Step 3: Build verifica**

```bash
cd apps/api/src/Api && dotnet build --nologo -v q
```
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetGameKbSettings/
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/SetGameKbSettings/
git add apps/api/src/Api/Routing/AdminGameKbEndpoints.cs
git commit -m "feat(kb): KB-10 admin per-game KB settings (GET/PUT SystemConfiguration)"
```

---

## Task 8: Frontend — API client extensions

**Files:**
- Modify: `apps/web/src/lib/api/clients/knowledgeBaseClient.ts`

- [ ] **Step 1: Aggiungi metodi al KnowledgeBaseClient**

Apri `knowledgeBaseClient.ts` e aggiungi i nuovi metodi alla classe/oggetto esistente:

```typescript
// Aggiungi al KnowledgeBaseClient

// KB-03
async getUserGameKbStatus(gameId: string): Promise<UserGameKbStatusDto | null> {
  return this.http.get<UserGameKbStatusDto>(
    `/games/${gameId}/knowledge-base`,
    userGameKbStatusSchema
  );
}

// KB-06
async submitKbFeedback(
  gameId: string,
  body: { chatSessionId: string; messageId: string; outcome: 'helpful' | 'not_helpful'; comment?: string }
): Promise<void> {
  await this.http.post<void>(`/games/${gameId}/knowledge-base/feedback`, body);
}

// KB-01
async getAdminGameKbDocuments(gameId: string): Promise<AdminGameKbDocumentsDto | null> {
  return this.http.get<AdminGameKbDocumentsDto>(
    `/admin/kb/games/${gameId}/documents`,
    adminGameKbDocumentsSchema
  );
}

// KB-02
async removeKbDocument(gameId: string, vectorDocId: string): Promise<void> {
  await this.http.delete(`/admin/kb/games/${gameId}/documents/${vectorDocId}`);
}

// KB-08
async getAdminKbFeedback(
  gameId: string,
  params?: { outcome?: string; from?: string; page?: number; pageSize?: number }
): Promise<AdminKbFeedbackDto | null> {
  const qs = new URLSearchParams();
  if (params?.outcome) qs.set('outcome', params.outcome);
  if (params?.from) qs.set('from', params.from);
  if (params?.page) qs.set('page', String(params.page));
  if (params?.pageSize) qs.set('pageSize', String(params.pageSize));
  const suffix = qs.toString() ? `?${qs}` : '';
  return this.http.get<AdminKbFeedbackDto>(
    `/admin/kb/games/${gameId}/feedback${suffix}`,
    adminKbFeedbackSchema
  );
}

// KB-10
async getGameKbSettings(gameId: string): Promise<GameKbSettingsDto | null> {
  return this.http.get<GameKbSettingsDto>(
    `/admin/kb/games/${gameId}/settings`,
    gameKbSettingsSchema
  );
}

async setGameKbSettings(gameId: string, settings: GameKbSettingsPayload): Promise<void> {
  await this.http.put<void>(`/admin/kb/games/${gameId}/settings`, settings);
}
```

Aggiungi i Zod schema e i tipi TypeScript nel file (o in un file `schemas/knowledgeBase.ts`):

```typescript
// Tipi e schema
export interface UserGameKbStatusDto {
  gameId: string;
  isIndexed: boolean;
  documentCount: number;
  coverageScore: number;
  coverageLevel: 'None' | 'Basic' | 'Standard' | 'Complete';
  suggestedQuestions: string[];
}

export interface AdminGameKbDocumentsDto {
  gameId: string;
  documents: Array<{
    id: string;
    pdfDocumentId: string;
    language: string;
    chunkCount: number;
    indexingStatus: string;
    indexedAt: string;
    sharedGameId?: string;
  }>;
}

export interface AdminKbFeedbackDto {
  total: number;
  items: Array<{
    id: string;
    userId: string;
    gameId: string;
    chatSessionId: string;
    messageId: string;
    outcome: string;
    comment?: string;
    createdAt: string;
  }>;
}

export interface GameKbSettingsDto {
  gameId: string;
  maxChunksOverride?: number;
  chunkSizeOverride?: number;
  cacheEnabledOverride?: boolean;
  languageOverride?: string;
}

export type GameKbSettingsPayload = Omit<GameKbSettingsDto, 'gameId'>;

// Zod schemas (usa z.any() come placeholder se il progetto usa Zod opzionale per GET)
const userGameKbStatusSchema = z.object({
  gameId: z.string(),
  isIndexed: z.boolean(),
  documentCount: z.number(),
  coverageScore: z.number(),
  coverageLevel: z.enum(['None', 'Basic', 'Standard', 'Complete']),
  suggestedQuestions: z.array(z.string()),
});

const adminGameKbDocumentsSchema = z.object({
  gameId: z.string(),
  documents: z.array(z.object({
    id: z.string(),
    pdfDocumentId: z.string(),
    language: z.string(),
    chunkCount: z.number(),
    indexingStatus: z.string(),
    indexedAt: z.string(),
    sharedGameId: z.string().optional(),
  })),
});

const adminKbFeedbackSchema = z.object({
  total: z.number(),
  items: z.array(z.object({
    id: z.string(),
    userId: z.string(),
    gameId: z.string(),
    chatSessionId: z.string(),
    messageId: z.string(),
    outcome: z.string(),
    comment: z.string().optional(),
    createdAt: z.string(),
  })),
});

const gameKbSettingsSchema = z.object({
  gameId: z.string(),
  maxChunksOverride: z.number().optional(),
  chunkSizeOverride: z.number().optional(),
  cacheEnabledOverride: z.boolean().optional(),
  languageOverride: z.string().optional(),
});
```

> **Nota:** Segui il pattern Zod/schema del file esistente. Se i metodi esistenti usano schema opzionale, usa lo stesso approccio.

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/lib/api/clients/knowledgeBaseClient.ts
git commit -m "feat(kb): estende KnowledgeBaseClient con metodi KB-01/02/03/06/08/10"
```

---

## Task 9: Frontend — hook `useGameKbStatus` + KB badge (KB-04)

**Files:**
- Create: `apps/web/src/hooks/use-game-kb-status.ts`
- Modify: game detail page per mostrare badge KB

- [ ] **Step 1: Crea hook**

```typescript
// apps/web/src/hooks/use-game-kb-status.ts
'use client';

import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';

export function useGameKbStatus(gameId: string | undefined) {
  return useQuery({
    queryKey: ['game-kb-status', gameId],
    queryFn: () => api.knowledgeBase.getUserGameKbStatus(gameId!),
    enabled: !!gameId,
    staleTime: 5 * 60 * 1000, // 5 min
  });
}
```

- [ ] **Step 2: Aggiungi badge KB alla pagina gioco**

Nel file del game detail user (es. `src/app/(authenticated)/games/[id]/page.tsx` o il componente che mostra le info gioco), aggiungi:

```tsx
import { useGameKbStatus } from '@/hooks/use-game-kb-status';
import { Badge } from '@/components/ui/badge';
import { BookOpen } from 'lucide-react';

// Dentro il componente:
const { data: kbStatus } = useGameKbStatus(gameId);

// Nel JSX, dove mostri le info del gioco:
{kbStatus?.isIndexed && (
  <Badge variant="secondary" className="gap-1 text-teal-600 border-teal-200 bg-teal-50">
    <BookOpen className="h-3 w-3" />
    KB {kbStatus.coverageLevel}
  </Badge>
)}
```

- [ ] **Step 3: Mostra domande suggerite**

Nello stesso componente game detail (sezione chat o sotto le info):

```tsx
{kbStatus?.suggestedQuestions && kbStatus.suggestedQuestions.length > 0 && (
  <div className="mt-4">
    <p className="text-sm font-medium text-muted-foreground mb-2">Domande frequenti:</p>
    <div className="flex flex-wrap gap-2">
      {kbStatus.suggestedQuestions.map((q, i) => (
        <button
          key={i}
          className="text-xs px-3 py-1 rounded-full border border-border bg-muted hover:bg-muted/80 text-left"
          onClick={() => onStartChat?.(q)}
        >
          {q}
        </button>
      ))}
    </div>
  </div>
)}
```

> **Nota:** `onStartChat` è un callback che avvia una chat con la domanda pre-compilata. Se non esiste, aggiungilo come prop opzionale o navigazione.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/hooks/use-game-kb-status.ts
git add apps/web/src/app/  # solo i file modificati
git commit -m "feat(kb): KB-04 badge KB + domande suggerite su pagina gioco"
```

---

## Task 10: Frontend — Chat feedback UI (KB-07)

**Files:**
- Modify: `apps/web/src/components/ui/meeple/chat-message.tsx` (già ha `FeedbackButtons`)

- [ ] **Step 1: Connetti onFeedbackChange alla chiamata API**

Nel componente che renderizza i messaggi chat (es. la pagina chat del gioco), passa `onFeedbackChange`:

```tsx
// Nel componente chat (es. src/app/(authenticated)/games/[id]/rules/page.tsx
// o il componente che monta ChatMessage)

import { api } from '@/lib/api';

// Funzione da passare ai messaggi assistant:
const handleFeedback = async (
  messageId: string,
  chatSessionId: string,
  gameId: string
) => async (outcome: 'helpful' | 'not_helpful', comment?: string) => {
  await api.knowledgeBase.submitKbFeedback(gameId, {
    chatSessionId,
    messageId,
    outcome,
    comment,
  });
};

// Nel render di ChatMessage:
<ChatMessage
  role="assistant"
  content={message.content}
  showFeedback={true}
  onFeedbackChange={handleFeedback(message.id, session.id, gameId)}
/>
```

> **Nota:** `ChatMessage` ha già `FeedbackButtons` integrato (prop `onFeedbackChange`). Basta passare la funzione. Il componente gestisce già il loading state e il toast visivo.

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/  # file chat modificati
git commit -m "feat(kb): KB-07 feedback chat UI — connette FeedbackButtons a POST /games/{id}/kb/feedback"
```

---

## Task 11: Frontend — Admin per-game KB panel (KB-01, KB-02, KB-10)

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/shared-games/[id]/knowledge-base/page.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/game-kb-documents.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/game-kb-settings.tsx`

- [ ] **Step 1: Crea componente `GameKbDocuments`**

```tsx
// apps/web/src/components/admin/knowledge-base/game-kb-documents.tsx
'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Trash2 } from 'lucide-react';
import { toast } from 'sonner';

interface Props {
  gameId: string;
}

export function GameKbDocuments({ gameId }: Props) {
  const queryClient = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ['admin-game-kb-documents', gameId],
    queryFn: () => api.knowledgeBase.getAdminGameKbDocuments(gameId),
  });

  const removeMutation = useMutation({
    mutationFn: (vectorDocId: string) =>
      api.knowledgeBase.removeKbDocument(gameId, vectorDocId),
    onSuccess: () => {
      toast.success('Documento rimosso dalla KB');
      queryClient.invalidateQueries({ queryKey: ['admin-game-kb-documents', gameId] });
    },
    onError: () => toast.error('Errore nella rimozione del documento'),
  });

  if (isLoading) return <div className="animate-pulse h-32 bg-muted rounded-lg" />;
  if (!data?.documents.length) {
    return (
      <div className="text-center py-8 text-muted-foreground">
        Nessun documento indicizzato per questo gioco
      </div>
    );
  }

  return (
    <div className="space-y-2">
      <p className="text-sm text-muted-foreground">{data.documents.length} documento/i indicizzato/i</p>
      {data.documents.map((doc) => (
        <div key={doc.id} className="flex items-center justify-between p-3 border rounded-lg">
          <div className="flex items-center gap-3">
            <Badge variant={doc.indexingStatus === 'completed' ? 'default' : 'secondary'}>
              {doc.indexingStatus}
            </Badge>
            <span className="text-sm font-mono">{doc.pdfDocumentId.slice(0, 8)}…</span>
            <span className="text-xs text-muted-foreground">{doc.chunkCount} chunk · {doc.language}</span>
          </div>
          <Button
            size="sm"
            variant="ghost"
            className="text-destructive hover:text-destructive"
            disabled={removeMutation.isPending}
            onClick={() => removeMutation.mutate(doc.id)}
          >
            <Trash2 className="h-4 w-4" />
          </Button>
        </div>
      ))}
    </div>
  );
}
```

- [ ] **Step 2: Crea componente `GameKbSettings`**

```tsx
// apps/web/src/components/admin/knowledge-base/game-kb-settings.tsx
'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { api } from '@/lib/api';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { toast } from 'sonner';
import { useEffect } from 'react';

interface Props {
  gameId: string;
}

interface SettingsForm {
  maxChunks: number | '';
  chunkSize: number | '';
  cacheEnabled: boolean;
  language: string;
}

export function GameKbSettings({ gameId }: Props) {
  const queryClient = useQueryClient();
  const { data } = useQuery({
    queryKey: ['admin-game-kb-settings', gameId],
    queryFn: () => api.knowledgeBase.getGameKbSettings(gameId),
  });

  const { register, handleSubmit, setValue, watch } = useForm<SettingsForm>({
    defaultValues: {
      maxChunks: '',
      chunkSize: '',
      cacheEnabled: true,
      language: '',
    },
  });

  useEffect(() => {
    if (!data) return;
    if (data.maxChunksOverride != null) setValue('maxChunks', data.maxChunksOverride);
    if (data.chunkSizeOverride != null) setValue('chunkSize', data.chunkSizeOverride);
    if (data.cacheEnabledOverride != null) setValue('cacheEnabled', data.cacheEnabledOverride);
    if (data.languageOverride) setValue('language', data.languageOverride);
  }, [data, setValue]);

  const saveMutation = useMutation({
    mutationFn: (values: SettingsForm) =>
      api.knowledgeBase.setGameKbSettings(gameId, {
        maxChunksOverride:   values.maxChunks === '' ? undefined : Number(values.maxChunks),
        chunkSizeOverride:   values.chunkSize === '' ? undefined : Number(values.chunkSize),
        cacheEnabledOverride: values.cacheEnabled,
        languageOverride:    values.language || undefined,
      }),
    onSuccess: () => {
      toast.success('Impostazioni KB salvate');
      queryClient.invalidateQueries({ queryKey: ['admin-game-kb-settings', gameId] });
    },
    onError: () => toast.error('Errore nel salvataggio impostazioni'),
  });

  return (
    <form onSubmit={handleSubmit((v) => saveMutation.mutate(v))} className="space-y-4">
      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-1">
          <Label>Max Chunks Override</Label>
          <Input type="number" placeholder="default globale" {...register('maxChunks')} />
        </div>
        <div className="space-y-1">
          <Label>Chunk Size Override</Label>
          <Input type="number" placeholder="default globale" {...register('chunkSize')} />
        </div>
        <div className="space-y-1">
          <Label>Lingua Override</Label>
          <Input placeholder="it, en, auto..." {...register('language')} />
        </div>
        <div className="flex items-center gap-2 pt-6">
          <Switch
            checked={watch('cacheEnabled')}
            onCheckedChange={(v) => setValue('cacheEnabled', v)}
          />
          <Label>Cache abilitata</Label>
        </div>
      </div>
      <Button type="submit" disabled={saveMutation.isPending}>
        {saveMutation.isPending ? 'Salvataggio…' : 'Salva impostazioni'}
      </Button>
    </form>
  );
}
```

- [ ] **Step 3: Crea pagina admin KB per-gioco**

```tsx
// apps/web/src/app/admin/(dashboard)/shared-games/[id]/knowledge-base/page.tsx
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { GameKbDocuments } from '@/components/admin/knowledge-base/game-kb-documents';
import { GameKbSettings } from '@/components/admin/knowledge-base/game-kb-settings';

interface Props {
  params: Promise<{ id: string }>;
}

export default async function GameKbPage({ params }: Props) {
  const { id } = await params;

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-semibold">Knowledge Base</h2>
        <p className="text-sm text-muted-foreground">
          Gestisci i documenti indicizzati e le impostazioni KB per questo gioco
        </p>
      </div>

      <Tabs defaultValue="documents">
        <TabsList>
          <TabsTrigger value="documents">Documenti</TabsTrigger>
          <TabsTrigger value="settings">Impostazioni</TabsTrigger>
        </TabsList>
        <TabsContent value="documents" className="mt-4">
          <GameKbDocuments gameId={id} />
        </TabsContent>
        <TabsContent value="settings" className="mt-4">
          <GameKbSettings gameId={id} />
        </TabsContent>
      </Tabs>
    </div>
  );
}
```

- [ ] **Step 4: Aggiungi link KB nel game detail admin**

In `apps/web/src/app/admin/(dashboard)/shared-games/[id]/page.tsx`, aggiungi un link o tab "Knowledge Base" che punta a `./knowledge-base`:

```tsx
// Aggiungi nel menu di navigazione del dettaglio gioco
<Link href={`/admin/shared-games/${id}/knowledge-base`}>
  <Button variant="outline" size="sm">
    <BookOpen className="h-4 w-4 mr-2" />
    Knowledge Base
  </Button>
</Link>
```

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/shared-games/
git add apps/web/src/components/admin/knowledge-base/game-kb-documents.tsx
git add apps/web/src/components/admin/knowledge-base/game-kb-settings.tsx
git commit -m "feat(kb): KB-01/02/10 admin panel per-game KB (documenti + settings)"
```

---

## Task 12: Frontend — Admin Feedback Review UI (KB-08)

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/knowledge-base/feedback/page.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/kb-feedback-panel.tsx`

- [ ] **Step 1: Crea componente `KbFeedbackPanel`**

```tsx
// apps/web/src/components/admin/knowledge-base/kb-feedback-panel.tsx
'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { ThumbsUp, ThumbsDown, MessageSquare } from 'lucide-react';

interface Props {
  gameId: string;
}

export function KbFeedbackPanel({ gameId }: Props) {
  const [outcomeFilter, setOutcomeFilter] = useState<string>('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useQuery({
    queryKey: ['admin-kb-feedback', gameId, outcomeFilter, page],
    queryFn: () => api.knowledgeBase.getAdminKbFeedback(gameId, {
      outcome: outcomeFilter || undefined,
      page,
      pageSize: 20,
    }),
  });

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-3">
        <Select value={outcomeFilter} onValueChange={setOutcomeFilter}>
          <SelectTrigger className="w-40">
            <SelectValue placeholder="Tutti" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="">Tutti</SelectItem>
            <SelectItem value="helpful">Utili</SelectItem>
            <SelectItem value="not_helpful">Non utili</SelectItem>
          </SelectContent>
        </Select>
        {data && <span className="text-sm text-muted-foreground">{data.total} feedback totali</span>}
      </div>

      {isLoading && <div className="animate-pulse h-48 bg-muted rounded-lg" />}

      <div className="space-y-2">
        {data?.items.map((item) => (
          <div key={item.id} className="flex items-start gap-3 p-3 border rounded-lg">
            {item.outcome === 'helpful'
              ? <ThumbsUp className="h-4 w-4 text-green-500 mt-0.5 shrink-0" />
              : <ThumbsDown className="h-4 w-4 text-red-500 mt-0.5 shrink-0" />}
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2 mb-1">
                <Badge variant={item.outcome === 'helpful' ? 'default' : 'destructive'} className="text-xs">
                  {item.outcome === 'helpful' ? 'Utile' : 'Non utile'}
                </Badge>
                <span className="text-xs text-muted-foreground font-mono">
                  msg: {item.messageId.slice(0, 8)}…
                </span>
                <span className="text-xs text-muted-foreground">
                  {new Date(item.createdAt).toLocaleDateString('it-IT')}
                </span>
              </div>
              {item.comment && (
                <p className="text-sm text-muted-foreground flex items-start gap-1">
                  <MessageSquare className="h-3 w-3 mt-0.5 shrink-0" />
                  {item.comment}
                </p>
              )}
            </div>
          </div>
        ))}
      </div>

      {data && data.total > 20 && (
        <div className="flex justify-center gap-2">
          <button
            className="px-3 py-1 text-sm border rounded disabled:opacity-50"
            disabled={page === 1}
            onClick={() => setPage(p => p - 1)}
          >
            ← Prec
          </button>
          <span className="px-3 py-1 text-sm">Pag {page}</span>
          <button
            className="px-3 py-1 text-sm border rounded disabled:opacity-50"
            disabled={page * 20 >= data.total}
            onClick={() => setPage(p => p + 1)}
          >
            Succ →
          </button>
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Crea pagina feedback (con selettore gioco)**

```tsx
// apps/web/src/app/admin/(dashboard)/knowledge-base/feedback/page.tsx
'use client';

import { useState } from 'react';
import { KbFeedbackPanel } from '@/components/admin/knowledge-base/kb-feedback-panel';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

export default function KbFeedbackPage() {
  const [gameId, setGameId] = useState('');

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-semibold">Feedback KB Utenti</h2>
        <p className="text-sm text-muted-foreground">
          Revisiona i feedback thumbs up/down degli utenti sulle risposte KB
        </p>
      </div>

      <div className="space-y-2 max-w-sm">
        <Label>Game ID</Label>
        <Input
          placeholder="Inserisci l'UUID del gioco..."
          value={gameId}
          onChange={(e) => setGameId(e.target.value)}
        />
      </div>

      {gameId.match(/^[0-9a-f-]{36}$/i) && (
        <KbFeedbackPanel gameId={gameId} />
      )}
    </div>
  );
}
```

> **Nota:** In futuro si può sostituire l'input con un select/autocomplete dei giochi indicizzati. Per ora l'UUID diretto è sufficiente.

- [ ] **Step 3: Aggiungi link nella KB hub**

In `apps/web/src/app/admin/(dashboard)/knowledge-base/page.tsx`, aggiungi una card per "Feedback":

```tsx
{
  title: 'Feedback Utenti',
  description: 'Revisiona thumbs up/down sulle risposte KB',
  href: '/admin/knowledge-base/feedback',
  icon: ThumbsUp,
  gradient: 'from-green-500/10 to-emerald-500/10',
}
```

- [ ] **Step 4: Frontend build verifica**

```bash
cd apps/web && pnpm typecheck
```
Expected: 0 errori TypeScript

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/knowledge-base/feedback/
git add apps/web/src/components/admin/knowledge-base/kb-feedback-panel.tsx
git commit -m "feat(kb): KB-08 admin feedback review UI (by game, filtro outcome, paginazione)"
```

---

## Task 13: DB migration + test integrazione finale + PR

**Files:**
- Check: `apps/api/src/Api/Infrastructure/Migrations/` (migrazione generata al Task 3)

- [ ] **Step 1: Applica la migrazione**

```bash
cd apps/api/src/Api
dotnet ef database update --nologo
```
Expected: Migration `AddKbUserFeedback` applicata. 0 errori.

- [ ] **Step 2: CI locale backend**

```bash
cd apps/api
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "Category=Unit" --nologo -v q
```
Expected: tutti i nuovi test passano.

- [ ] **Step 3: CI locale frontend**

```bash
cd apps/web
pnpm lint && pnpm typecheck && pnpm test && pnpm build
```
Expected: 0 errori.

- [ ] **Step 4: Push e PR**

```bash
git push -u origin feature/issue-kb-management
gh pr create \
  --title "feat(kb): KB management — coverage score, feedback, per-game admin, suggested questions" \
  --base main-dev \
  --body "$(cat <<'EOF'
## Summary
- KB-01: Admin lista documenti KB per-gioco (GET /admin/kb/games/{id}/documents)
- KB-02: Admin rimozione documento KB (DELETE /admin/kb/games/{id}/documents/{docId})
- KB-03: User stato KB gioco (GET /games/{id}/knowledge-base)
- KB-04: Badge KB + domande suggerite su pagina gioco
- KB-05: Quartz job coverage score giornaliero (SystemConfiguration)
- KB-06: KbUserFeedback entity + migration + endpoint POST /games/{id}/knowledge-base/feedback
- KB-07: Chat feedback UI (FeedbackButtons connessi)
- KB-08: Admin feedback review panel (per-game, filtro, paginazione)
- KB-09: Quartz job domande suggerite pre-calcolate (template-based)
- KB-10: Admin impostazioni KB per-gioco (GET/PUT SystemConfiguration)

## Test plan
- [ ] Unit tests backend: tutti i nuovi handler/job testati
- [ ] Build backend: 0 errori
- [ ] Migrazione DB: `AddKbUserFeedback` applicata correttamente
- [ ] Frontend typecheck: 0 errori
- [ ] Badge KB visibile su giochi con documenti indicizzati
- [ ] Feedback chat: thumbs up/down funzionanti e salvati in DB
- [ ] Admin panel: lista documenti, rimozione, settings per-gioco

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 5: Code review + merge**

Usa `/code-review:code-review <PR-URL>` prima di fare merge.

---

## Self-Review

### Spec coverage
| KB item | Task che lo implementa |
|---------|----------------------|
| KB-01 Admin per-game KB view | Task 2 (backend) + Task 11 (frontend) |
| KB-02 Admin add/remove document | Task 2 (remove backend) + Task 11 (UI) |
| KB-03 User KB status endpoint | Task 4 |
| KB-04 User KB badge | Task 9 |
| KB-05 Coverage score batch job | Task 1 |
| KB-06 User feedback backend | Task 3 |
| KB-07 Chat feedback UI | Task 10 |
| KB-08 Admin feedback review | Task 6 (backend) + Task 12 (frontend) |
| KB-09 Suggested questions | Task 5 |
| KB-10 Admin per-game settings | Task 7 (backend) + Task 11 (frontend) |

### Type consistency check
- `KbUserFeedback.Outcome` → `string` con valori `"helpful" | "not_helpful"` — coerente in entity, command, validator, frontend
- `UserGameKbStatusDto.CoverageLevel` → `"None" | "Basic" | "Standard" | "Complete"` — coerente con `KbCoverageComputeJob.ScoreToLevel()`
- `SystemConfiguration` chiavi: `KB:Coverage:{gameId}`, `KB:SuggestedQuestions:{gameId}`, `KB:Game:{gameId}:Settings` — pattern uniforme, max 200 char ✓
- Endpoint admin: tutti su `/admin/kb/games/{gameId}/...` tramite `RequireAdminSessionFilter` ✓
- Endpoint user: tutti su `/games/{gameId}/knowledge-base/...` con `.RequireSession()` ✓
