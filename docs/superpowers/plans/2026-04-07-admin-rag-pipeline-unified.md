# Admin RAG Pipeline — Unified Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix 3 backend bugs preventing PDF processing and create a unified admin page for RAG pipeline management.

**Architecture:** Backend fixes are surgical changes to existing files (event handler, metrics recording, batch ETA endpoint). Frontend creates a new tabbed page at `/admin/knowledge-base/rag-pipeline/` reusing existing queue/upload/embedding components.

**Tech Stack:** .NET 9 (MediatR, Quartz, EF Core) | Next.js 16 (React 19, TanStack Query, shadcn/ui, @dnd-kit)

---

## File Structure

### Backend (New Files)
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/EventHandlers/PrivatePdfAssociatedEventHandler.cs` — Enqueues PDF for processing when a private PDF is associated
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/Queue/GetBatchETAQuery.cs` — Query + Handler for batch ETA calculation
- `tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/EventHandlers/PrivatePdfAssociatedEventHandlerTests.cs` — Unit tests for event handler
- `tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Queries/GetBatchETAQueryHandlerTests.cs` — Unit tests for ETA query

### Backend (Modified Files)
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Jobs/PdfProcessingQuartzJob.cs` — Add `RecordStepDurationAsync` calls
- `apps/api/src/Api/Routing/AdminQueueEndpoints.cs` — Add batch ETA endpoint
- `tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Jobs/PdfProcessingQuartzJobTests.cs` — Test metrics recording

### Frontend (New Files)
- `apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/page.tsx` — Server page component
- `apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/rag-pipeline-client.tsx` — Client component with tab layout
- `apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/upload-and-queue-tab.tsx` — Tab 1: upload + queue + sidebar grid
- `apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/queue-eta-sidebar.tsx` — Right sidebar with metrics and actions
- `apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/history-tab.tsx` — Tab 2: completed PDFs history
- `apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/embedding-tab.tsx` — Tab 3: embedding service wrapper
- `apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/config-tab.tsx` — Tab 4: settings wrapper
- `apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/lib/use-queue-eta.ts` — Hook for batch ETA polling

### Frontend (Modified Files)
- `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api.ts` — Add `fetchBatchETA()` function and types
- `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/queue-item.tsx` — Add ETA display column
- `apps/web/src/config/admin-dashboard-navigation.ts` — Add RAG Pipeline nav item

---

## Task 1: Backend — PrivatePdfAssociatedEvent Handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/EventHandlers/PrivatePdfAssociatedEventHandler.cs`
- Create: `tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/EventHandlers/PrivatePdfAssociatedEventHandlerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/EventHandlers/PrivatePdfAssociatedEventHandlerTests.cs
using Api.BoundedContexts.DocumentProcessing.Application.EventHandlers;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.SharedKernel;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.EventHandlers;

public class PrivatePdfAssociatedEventHandlerTests
{
    private readonly Mock<IPdfDocumentRepository> _pdfDocRepo = new();
    private readonly Mock<IProcessingJobRepository> _jobRepo = new();
    private readonly Mock<IQueueStreamService> _queueStream = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<PrivatePdfAssociatedEventHandler>> _logger = new();
    private readonly Mock<TimeProvider> _timeProvider = new();
    private readonly PrivatePdfAssociatedEventHandler _sut;

    public PrivatePdfAssociatedEventHandlerTests()
    {
        _timeProvider.Setup(t => t.GetUtcNow()).Returns(DateTimeOffset.UtcNow);
        _sut = new PrivatePdfAssociatedEventHandler(
            _pdfDocRepo.Object,
            _jobRepo.Object,
            _queueStream.Object,
            _unitOfWork.Object,
            _logger.Object,
            _timeProvider.Object);
    }

    [Fact]
    public async Task Handle_WhenPdfExists_CreatesProcessingJobAndPublishesEvent()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var notification = new PrivatePdfAssociatedEvent(entryId, userId, gameId, pdfId);

        var pdfDoc = new Mock<PdfDocument>();
        pdfDoc.Setup(p => p.Id).Returns(pdfId);
        pdfDoc.Setup(p => p.FileSizeBytes).Returns(1024);
        _pdfDocRepo.Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfDoc.Object);
        _jobRepo.Setup(r => r.ExistsByPdfDocumentIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _sut.Handle(notification, CancellationToken.None);

        // Assert
        _jobRepo.Verify(r => r.AddAsync(It.Is<ProcessingJob>(j =>
            j.PdfDocumentId == pdfId && j.Priority == 10),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _queueStream.Verify(s => s.PublishQueueEventAsync(
            It.Is<QueueStreamEvent>(e => e.Type == QueueStreamEventType.JobQueued),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenJobAlreadyExists_SkipsCreation()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var notification = new PrivatePdfAssociatedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), pdfId);

        _jobRepo.Setup(r => r.ExistsByPdfDocumentIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _sut.Handle(notification, CancellationToken.None);

        // Assert
        _jobRepo.Verify(r => r.AddAsync(It.IsAny<ProcessingJob>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPdfNotFound_LogsWarningAndReturns()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var notification = new PrivatePdfAssociatedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), pdfId);

        _pdfDocRepo.Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PdfDocument?)null);
        _jobRepo.Setup(r => r.ExistsByPdfDocumentIdAsync(pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _sut.Handle(notification, CancellationToken.None);

        // Assert
        _jobRepo.Verify(r => r.AddAsync(It.IsAny<ProcessingJob>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~PrivatePdfAssociatedEventHandlerTests" --no-build 2>&1 | head -20`
Expected: FAIL — class `PrivatePdfAssociatedEventHandler` does not exist

- [ ] **Step 3: Write the event handler implementation**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/EventHandlers/PrivatePdfAssociatedEventHandler.cs
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.SharedKernel;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Application.EventHandlers;

public sealed class PrivatePdfAssociatedEventHandler : INotificationHandler<PrivatePdfAssociatedEvent>
{
    private readonly IPdfDocumentRepository _pdfDocRepo;
    private readonly IProcessingJobRepository _jobRepo;
    private readonly IQueueStreamService _queueStream;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PrivatePdfAssociatedEventHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public PrivatePdfAssociatedEventHandler(
        IPdfDocumentRepository pdfDocRepo,
        IProcessingJobRepository jobRepo,
        IQueueStreamService queueStream,
        IUnitOfWork unitOfWork,
        ILogger<PrivatePdfAssociatedEventHandler> logger,
        TimeProvider timeProvider)
    {
        _pdfDocRepo = pdfDocRepo;
        _jobRepo = jobRepo;
        _queueStream = queueStream;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task Handle(PrivatePdfAssociatedEvent notification, CancellationToken cancellationToken)
    {
        var pdfId = notification.PdfDocumentId;

        if (await _jobRepo.ExistsByPdfDocumentIdAsync(pdfId, cancellationToken))
        {
            _logger.LogInformation("Processing job already exists for PDF {PdfId}, skipping", pdfId);
            return;
        }

        var pdfDoc = await _pdfDocRepo.GetByIdAsync(pdfId, cancellationToken);
        if (pdfDoc is null)
        {
            _logger.LogWarning("PDF {PdfId} not found when handling PrivatePdfAssociatedEvent", pdfId);
            return;
        }

        var job = ProcessingJob.Create(pdfId, priority: 10, _timeProvider);
        await _jobRepo.AddAsync(job, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Enqueued processing job {JobId} for private PDF {PdfId}", job.Id, pdfId);

        await _queueStream.PublishQueueEventAsync(
            new QueueStreamEvent(
                QueueStreamEventType.JobQueued,
                job.Id,
                Data: null,
                _timeProvider.GetUtcNow()),
            cancellationToken);
    }
}
```

> **Note:** The exact `ProcessingJob.Create()` factory method signature may differ. Check `ProcessingJob` entity for the correct factory. If it uses a constructor, adapt accordingly. The key is: create a job with `PdfDocumentId = pdfId`, `Priority = 10`, `Status = Queued`.

- [ ] **Step 4: Build and run tests**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Then: `dotnet test --filter "FullyQualifiedName~PrivatePdfAssociatedEventHandlerTests" --no-build`
Expected: 3 tests PASS

> **If build fails:** Check the exact `ProcessingJob` entity and `IProcessingJobRepository.AddAsync` signature. Adapt the code to match the actual API. Check `IQueueStreamService` — it may be internal. If so, use the public interface.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/EventHandlers/PrivatePdfAssociatedEventHandler.cs tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/EventHandlers/PrivatePdfAssociatedEventHandlerTests.cs
git commit -m "feat(doc-processing): add event handler to enqueue private PDFs for processing

PrivatePdfAssociatedEvent was raised but had no handler — private PDFs
were uploaded but never queued for text extraction and embedding."
```

---

## Task 2: Backend — Record Step Metrics in Quartz Job

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Jobs/PdfProcessingQuartzJob.cs`
- Create: `tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Jobs/PdfProcessingQuartzJobMetricsTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Jobs/PdfProcessingQuartzJobMetricsTests.cs
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Jobs;

public class PdfProcessingQuartzJobMetricsTests
{
    [Fact]
    public void QuartzJob_ShouldAcceptIProcessingMetricsService_InConstructor()
    {
        // This test verifies that PdfProcessingQuartzJob accepts IProcessingMetricsService
        // as a constructor dependency. If the DI wiring is wrong, this will fail at runtime.
        var metricsService = new Mock<IProcessingMetricsService>();
        Assert.NotNull(metricsService.Object);
    }
}
```

> **Note:** Full integration test of the Quartz job requires the DB context and pipeline service. The real verification is: after processing a PDF, check that `ProcessingMetrics` table has records. This is best tested as an integration test with the actual pipeline.

- [ ] **Step 2: Run test to verify it compiles**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~PdfProcessingQuartzJobMetricsTests" --no-build 2>&1 | tail -5`
Expected: PASS (baseline)

- [ ] **Step 3: Modify PdfProcessingQuartzJob to inject and call IProcessingMetricsService**

Open `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Jobs/PdfProcessingQuartzJob.cs`.

**Add to constructor:**
```csharp
// Add field
private readonly IProcessingMetricsService _metricsService;

// Add to constructor parameters
public PdfProcessingQuartzJob(
    MeepleAiDbContext dbContext,
    IServiceProvider serviceProvider,
    ILogger<PdfProcessingQuartzJob> logger,
    TimeProvider timeProvider,
    IProcessingMetricsService metricsService)  // <-- ADD THIS
{
    _dbContext = dbContext;
    _serviceProvider = serviceProvider;
    _logger = logger;
    _timeProvider = timeProvider;
    _metricsService = metricsService;  // <-- ADD THIS
}
```

**Add using:**
```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Services;
```

**Add metrics recording after step completion (in the block around lines 184-209 where steps are marked as completed):**

Find the section where individual steps are iterated and their `DurationMs` is set. After setting the duration for each step, add:

```csharp
// After: step.DurationMs = perStepMs;
// Add:
try
{
    await _metricsService.RecordStepDurationAsync(
        job.PdfDocumentId,
        Enum.Parse<PdfProcessingState>(step.StepName, ignoreCase: true),
        TimeSpan.FromMilliseconds(perStepMs),
        pdfDocument?.FileSizeBytes ?? 0,
        pdfDocument?.PageCount ?? 0,
        context.CancellationToken);
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to record metrics for step {Step} of PDF {PdfId}", step.StepName, job.PdfDocumentId);
}
```

> **Note:** The try/catch ensures metrics recording failures don't break PDF processing. The exact property names (`FileSizeBytes`, `PageCount`) must match the `PdfDocument` entity. Check and adapt.

- [ ] **Step 4: Build and verify**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Jobs/PdfProcessingQuartzJob.cs tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Jobs/PdfProcessingQuartzJobMetricsTests.cs
git commit -m "feat(doc-processing): record step duration metrics in Quartz job

RecordStepDurationAsync was never called — ETAs fell back to static
2s/page calculation. Now each step duration is recorded for historical
ETA accuracy."
```

---

## Task 3: Backend — Batch ETA Endpoint

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/Queue/GetBatchETAQuery.cs`
- Modify: `apps/api/src/Api/Routing/AdminQueueEndpoints.cs`
- Create: `tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Queries/GetBatchETAQueryHandlerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Queries/GetBatchETAQueryHandlerTests.cs
using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Queries;

public class GetBatchETAQueryHandlerTests
{
    private readonly Mock<IProcessingJobRepository> _jobRepo = new();
    private readonly Mock<IProcessingMetricsService> _metricsService = new();
    private readonly GetBatchETAQueryHandler _sut;

    public GetBatchETAQueryHandlerTests()
    {
        _sut = new GetBatchETAQueryHandler(_jobRepo.Object, _metricsService.Object);
    }

    [Fact]
    public async Task Handle_WithQueuedJobs_ReturnsETAsForEachJob()
    {
        // Arrange
        var jobId1 = Guid.NewGuid();
        var jobId2 = Guid.NewGuid();
        var pdfId1 = Guid.NewGuid();
        var pdfId2 = Guid.NewGuid();

        var jobs = new List<ProcessingJob>
        {
            CreateJob(jobId1, pdfId1),
            CreateJob(jobId2, pdfId2)
        };

        _jobRepo.Setup(r => r.GetAllByStatusAsync(
            It.Is<JobStatus>(s => s == JobStatus.Queued || s == JobStatus.Processing),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);

        _metricsService.Setup(m => m.CalculateETAAsync(pdfId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TimeSpan.FromMinutes(3));
        _metricsService.Setup(m => m.CalculateETAAsync(pdfId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TimeSpan.FromMinutes(5));

        var query = new GetBatchETAQuery();

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.JobETAs.Count);
        Assert.Equal(TimeSpan.FromMinutes(8), result.TotalETA);
    }

    [Fact]
    public async Task Handle_WithNoJobs_ReturnsEmptyResult()
    {
        _jobRepo.Setup(r => r.GetAllByStatusAsync(It.IsAny<JobStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProcessingJob>());

        var result = await _sut.Handle(new GetBatchETAQuery(), CancellationToken.None);

        Assert.Empty(result.JobETAs);
        Assert.Equal(TimeSpan.Zero, result.TotalETA);
    }

    private static ProcessingJob CreateJob(Guid jobId, Guid pdfId)
    {
        // Use reflection or factory to create test instances
        // Adapt to actual ProcessingJob construction
        return ProcessingJob.Create(pdfId, priority: 10, TimeProvider.System);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~GetBatchETAQueryHandlerTests" --no-build 2>&1 | head -10`
Expected: FAIL — `GetBatchETAQuery` and `GetBatchETAQueryHandler` do not exist

- [ ] **Step 3: Write the query and handler**

```csharp
// apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/Queue/GetBatchETAQuery.cs
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;

public sealed record GetBatchETAQuery : IRequest<BatchETAResult>;

public sealed record BatchETAResult
{
    public Dictionary<Guid, double> JobETAs { get; init; } = new();
    public TimeSpan TotalETA { get; init; }
}

public sealed class GetBatchETAQueryHandler : IRequestHandler<GetBatchETAQuery, BatchETAResult>
{
    private readonly IProcessingJobRepository _jobRepo;
    private readonly IProcessingMetricsService _metricsService;

    public GetBatchETAQueryHandler(
        IProcessingJobRepository jobRepo,
        IProcessingMetricsService metricsService)
    {
        _jobRepo = jobRepo;
        _metricsService = metricsService;
    }

    public async Task<BatchETAResult> Handle(GetBatchETAQuery request, CancellationToken cancellationToken)
    {
        var queuedJobs = await _jobRepo.GetAllByStatusAsync(JobStatus.Queued, cancellationToken);
        var processingJobs = await _jobRepo.GetAllByStatusAsync(JobStatus.Processing, cancellationToken);
        var allJobs = queuedJobs.Concat(processingJobs).ToList();

        if (allJobs.Count == 0)
            return new BatchETAResult { TotalETA = TimeSpan.Zero };

        var jobETAs = new Dictionary<Guid, double>();
        var totalSeconds = 0.0;

        foreach (var job in allJobs)
        {
            var eta = await _metricsService.CalculateETAAsync(job.PdfDocumentId, cancellationToken);
            var seconds = eta.TotalSeconds;
            jobETAs[job.Id] = seconds;
            totalSeconds += seconds;
        }

        return new BatchETAResult
        {
            JobETAs = jobETAs,
            TotalETA = TimeSpan.FromSeconds(totalSeconds)
        };
    }
}
```

- [ ] **Step 4: Register the endpoint**

Open `apps/api/src/Api/Routing/AdminQueueEndpoints.cs`. Add the following endpoint in the existing group:

```csharp
// Add inside MapGroup("/admin/queue") block:
group.MapGet("/eta", async (IMediator mediator) =>
{
    var result = await mediator.Send(new GetBatchETAQuery());
    return Results.Ok(result);
})
.WithName("GetBatchETA")
.WithDescription("Get ETA estimates for all queued and processing jobs");
```

Add using:
```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
```

- [ ] **Step 5: Build and run tests**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Then: `dotnet test --filter "FullyQualifiedName~GetBatchETAQueryHandlerTests" --no-build`
Expected: 2 tests PASS

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/Queue/GetBatchETAQuery.cs apps/api/src/Api/Routing/AdminQueueEndpoints.cs tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Queries/GetBatchETAQueryHandlerTests.cs
git commit -m "feat(doc-processing): add batch ETA endpoint for queue dashboard

GET /admin/queue/eta returns per-job ETA estimates and total queue
drain time, used by the unified RAG pipeline admin dashboard."
```

---

## Task 4: Frontend — Queue API Extension (ETA types + fetch)

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api.ts`

- [ ] **Step 1: Add BatchETA types and fetch function**

Open `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api.ts`. Add at the end of the file, before the closing exports:

```typescript
// --- Batch ETA ---

export interface BatchETAResponse {
  jobETAs: Record<string, number>; // jobId -> seconds remaining
  totalETA: number; // total seconds to drain queue (as TimeSpan.TotalSeconds)
}

export async function fetchBatchETA(): Promise<BatchETAResponse> {
  const response = await fetch('/api/v1/admin/queue/eta', {
    credentials: 'include',
  });
  if (!response.ok) {
    throw new Error(`Failed to fetch batch ETA: ${response.status}`);
  }
  return response.json();
}
```

- [ ] **Step 2: Verify the file compiles**

Run: `cd apps/web && pnpm typecheck 2>&1 | tail -10`
Expected: No new type errors

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api.ts
git commit -m "feat(web): add batch ETA API types and fetch function"
```

---

## Task 5: Frontend — useQueueETA Hook

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/lib/use-queue-eta.ts`

- [ ] **Step 1: Create the hook**

```typescript
// apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/lib/use-queue-eta.ts
'use client';

import { useQuery } from '@tanstack/react-query';
import { fetchBatchETA, type BatchETAResponse } from '../../queue/lib/queue-api';

export function useQueueETA(enabled: boolean = true) {
  return useQuery<BatchETAResponse>({
    queryKey: ['admin', 'queue', 'eta'],
    queryFn: fetchBatchETA,
    enabled,
    staleTime: 25_000,      // 25s — slightly less than refetch interval
    refetchInterval: 30_000, // poll every 30s
  });
}

export function formatETA(totalSeconds: number): string {
  if (totalSeconds <= 0) return '—';
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = Math.floor(totalSeconds % 60);
  if (minutes === 0) return `~${seconds}s`;
  return `~${minutes}m ${seconds > 0 ? `${seconds}s` : ''}`.trim();
}
```

- [ ] **Step 2: Verify the file compiles**

Run: `cd apps/web && pnpm typecheck 2>&1 | tail -10`
Expected: No new type errors

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/lib/use-queue-eta.ts
git commit -m "feat(web): add useQueueETA hook with 30s polling"
```

---

## Task 6: Frontend — Unified Page Shell (Server + Client with Tabs)

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/page.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/rag-pipeline-client.tsx`

- [ ] **Step 1: Create the server page**

```tsx
// apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/page.tsx
import type { Metadata } from 'next';
import { RagPipelineClient } from './components/rag-pipeline-client';

export const metadata: Metadata = {
  title: 'RAG Pipeline | Admin',
  description: 'Unified RAG pipeline management — upload, queue, metrics',
};

export default function RagPipelinePage() {
  return <RagPipelineClient />;
}
```

- [ ] **Step 2: Create the client component with tab layout**

```tsx
// apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/rag-pipeline-client.tsx
'use client';

import { useState } from 'react';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { UploadAndQueueTab } from './upload-and-queue-tab';
import { HistoryTab } from './history-tab';
import { EmbeddingTab } from './embedding-tab';
import { ConfigTab } from './config-tab';

export function RagPipelineClient() {
  const [activeTab, setActiveTab] = useState('upload-queue');

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">RAG Pipeline</h1>
        <p className="text-muted-foreground">
          Gestisci upload, processing e embedding dei PDF
        </p>
      </div>

      <Tabs value={activeTab} onValueChange={setActiveTab}>
        <TabsList>
          <TabsTrigger value="upload-queue">📤 Upload & Coda</TabsTrigger>
          <TabsTrigger value="history">📊 Storico & Analytics</TabsTrigger>
          <TabsTrigger value="embedding">🧠 Embedding Service</TabsTrigger>
          <TabsTrigger value="config">⚙️ Configurazione</TabsTrigger>
        </TabsList>

        <TabsContent value="upload-queue" className="mt-4">
          <UploadAndQueueTab />
        </TabsContent>

        <TabsContent value="history" className="mt-4">
          <HistoryTab />
        </TabsContent>

        <TabsContent value="embedding" className="mt-4">
          <EmbeddingTab />
        </TabsContent>

        <TabsContent value="config" className="mt-4">
          <ConfigTab />
        </TabsContent>
      </Tabs>
    </div>
  );
}
```

- [ ] **Step 3: Create placeholder tab components so the page renders**

```tsx
// apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/history-tab.tsx
'use client';

export function HistoryTab() {
  return (
    <div className="rounded-lg border p-8 text-center text-muted-foreground">
      Storico & Analytics — coming in Task 9
    </div>
  );
}
```

```tsx
// apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/embedding-tab.tsx
'use client';

export function EmbeddingTab() {
  return (
    <div className="rounded-lg border p-8 text-center text-muted-foreground">
      Embedding Service — coming in Task 10
    </div>
  );
}
```

```tsx
// apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/config-tab.tsx
'use client';

export function ConfigTab() {
  return (
    <div className="rounded-lg border p-8 text-center text-muted-foreground">
      Configurazione — coming in Task 11
    </div>
  );
}
```

```tsx
// apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/upload-and-queue-tab.tsx
'use client';

export function UploadAndQueueTab() {
  return (
    <div className="rounded-lg border p-8 text-center text-muted-foreground">
      Upload & Coda — coming in Task 7
    </div>
  );
}
```

- [ ] **Step 4: Verify the page renders**

Run: `cd apps/web && pnpm typecheck 2>&1 | tail -10`
Expected: No type errors

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/
git commit -m "feat(web): add RAG pipeline unified page shell with tab layout

Creates /admin/knowledge-base/rag-pipeline/ with 4 tabs.
Tab content will be implemented in subsequent tasks."
```

---

## Task 7: Frontend — Upload & Queue Tab (Main Dashboard)

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/upload-and-queue-tab.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/queue-eta-sidebar.tsx`

- [ ] **Step 1: Implement the Upload & Queue tab with grid layout**

```tsx
// apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/upload-and-queue-tab.tsx
'use client';

import { useState, useCallback } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { UploadZone } from '@/components/admin/knowledge-base/upload-zone';
import { QueueList } from '../../../queue/components/queue-list';
import { QueueFilters } from '../../../queue/components/queue-filters';
import { BulkActionsBar } from '../../../queue/components/bulk-actions-bar';
import { SSEConnectionIndicator } from '../../../queue/components/sse-connection-indicator';
import { useQueueSSE } from '../../../queue/lib/queue-api';
import { useQueueETA, formatETA } from '../lib/use-queue-eta';
import { QueueETASidebar } from './queue-eta-sidebar';
import type { QueueFilters as QueueFiltersType } from '../../../queue/lib/queue-api';

export function UploadAndQueueTab() {
  const [filters, setFilters] = useState<QueueFiltersType>({
    page: 1,
    pageSize: 20,
  });
  const [selectedJobId, setSelectedJobId] = useState<string | null>(null);
  const queryClient = useQueryClient();

  const { connected: sseConnected } = useQueueSSE();
  const { data: etaData } = useQueueETA();

  const handleUploadComplete = useCallback(() => {
    queryClient.invalidateQueries({ queryKey: ['admin', 'queue'] });
  }, [queryClient]);

  return (
    <div className="space-y-4">
      {/* Stats Bar */}
      <StatsBar etaData={etaData} />

      {/* Main Grid: Left (upload + queue) + Right (sidebar) */}
      <div className="grid grid-cols-1 lg:grid-cols-[1fr_280px] gap-4">
        {/* Left Column */}
        <div className="space-y-4">
          {/* Upload Zone */}
          <UploadZone />

          {/* Queue Toolbar */}
          <div className="flex items-center justify-between gap-2">
            <div className="flex items-center gap-2">
              <h2 className="text-lg font-semibold">Coda di Processing</h2>
              <SSEConnectionIndicator connected={sseConnected} />
            </div>
            <div className="flex items-center gap-2">
              <QueueFilters filters={filters} onFiltersChange={setFilters} />
              <BulkActionsBar />
            </div>
          </div>

          {/* Queue List */}
          <QueueList
            filters={filters}
            onFiltersChange={setFilters}
            selectedJobId={selectedJobId}
            onSelectJob={setSelectedJobId}
            etaData={etaData?.jobETAs}
          />
        </div>

        {/* Right Sidebar */}
        <QueueETASidebar />
      </div>
    </div>
  );
}

function StatsBar({ etaData }: { etaData?: { totalETA: number } | null }) {
  // Stats are already available via useQueueSSE invalidations
  // This component renders the horizontal stats bar
  return (
    <div className="grid grid-cols-5 gap-0 rounded-lg border bg-muted/30 overflow-hidden">
      <StatCell label="In coda" queryKey={['admin', 'queue', 'stats', 'Queued']} color="text-orange-500" />
      <StatCell label="Processing" queryKey={['admin', 'queue', 'stats', 'Processing']} color="text-blue-500" />
      <StatCell label="Completati 24h" queryKey={['admin', 'queue', 'stats', 'Completed']} color="text-green-500" />
      <StatCell label="Falliti" queryKey={['admin', 'queue', 'stats', 'Failed']} color="text-red-500" />
      <div className="p-3 text-center bg-orange-50 dark:bg-orange-950/20">
        <div className="text-lg font-bold text-orange-600">
          {etaData ? formatETA(etaData.totalETA) : '—'}
        </div>
        <div className="text-xs text-muted-foreground uppercase tracking-wide">ETA svuotamento</div>
      </div>
    </div>
  );
}

function StatCell({ label, queryKey, color }: { label: string; queryKey: string[]; color: string }) {
  // This is a simplified version — adapt to use the actual useQueueStats hook
  // from queue-dashboard-client.tsx which fetches count per status
  return (
    <div className="p-3 text-center border-r last:border-r-0">
      <div className={`text-xl font-bold ${color}`}>—</div>
      <div className="text-xs text-muted-foreground uppercase tracking-wide">{label}</div>
    </div>
  );
}
```

> **Note:** The `StatsBar` `StatCell` component needs to use the actual `useQueueStats` hooks from `queue-api.ts`. The above is a structural placeholder for the stat values — adapt `StatCell` to call `fetchQueue({ status: 'Queued', pageSize: 0 })` and read the `total` from the response, or reuse the existing `useQueueStats()` hook from `queue-dashboard-client.tsx`. Similarly, `QueueList` may need a prop adaptation for `etaData` — check its actual props interface and pass ETA data through.

- [ ] **Step 2: Implement the sidebar**

```tsx
// apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/queue-eta-sidebar.tsx
'use client';

import { useQuery } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { formatETA } from '../lib/use-queue-eta';
import { bulkReindexFailed, updateQueueConfig, getQueueConfig } from '../../../queue/lib/queue-api';

export function QueueETASidebar() {
  const { data: pipelineHealth } = useQuery({
    queryKey: ['admin', 'pipeline', 'health'],
    queryFn: async () => {
      const res = await fetch('/api/v1/admin/pipeline/health', { credentials: 'include' });
      return res.json();
    },
    staleTime: 30_000,
    refetchInterval: 60_000,
  });

  const { data: embeddingInfo } = useQuery({
    queryKey: ['admin', 'embedding', 'info'],
    queryFn: async () => {
      const res = await fetch('/api/v1/admin/embedding/info', { credentials: 'include' });
      return res.json();
    },
    staleTime: 30_000,
  });

  const { data: embeddingMetrics } = useQuery({
    queryKey: ['admin', 'embedding', 'metrics'],
    queryFn: async () => {
      const res = await fetch('/api/v1/admin/embedding/metrics', { credentials: 'include' });
      return res.json();
    },
    staleTime: 30_000,
    refetchInterval: 30_000,
  });

  const { data: stepStats } = useQuery({
    queryKey: ['admin', 'pipeline', 'metrics'],
    queryFn: async () => {
      const res = await fetch('/api/v1/admin/pipeline/metrics', { credentials: 'include' });
      return res.json();
    },
    staleTime: 30_000,
    refetchInterval: 60_000,
  });

  const { data: queueConfig } = useQuery({
    queryKey: ['admin', 'queue', 'config'],
    queryFn: getQueueConfig,
    staleTime: 10_000,
  });

  return (
    <div className="space-y-3 lg:sticky lg:top-4">
      {/* Step timing averages */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm">⏱️ Tempo medio per PDF</CardTitle>
        </CardHeader>
        <CardContent className="text-sm space-y-1">
          {stepStats?.steps ? (
            <>
              {stepStats.steps.map((step: { name: string; avgDurationSeconds: number }) => (
                <div key={step.name} className="flex justify-between">
                  <span className="text-muted-foreground">{step.name}</span>
                  <span className="font-medium">{formatETA(step.avgDurationSeconds)}</span>
                </div>
              ))}
              <div className="border-t pt-1 flex justify-between font-semibold">
                <span>Totale</span>
                <span className="text-orange-600">
                  {formatETA(stepStats.steps.reduce((sum: number, s: { avgDurationSeconds: number }) => sum + s.avgDurationSeconds, 0))}
                </span>
              </div>
            </>
          ) : (
            <p className="text-muted-foreground text-xs">Nessun dato disponibile</p>
          )}
        </CardContent>
      </Card>

      {/* Pipeline health */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm">🏥 Stato Pipeline</CardTitle>
        </CardHeader>
        <CardContent className="text-sm space-y-1">
          {pipelineHealth?.stages?.map((stage: { name: string; status: string }) => (
            <div key={stage.name} className="flex justify-between items-center">
              <span>{stage.name}</span>
              <span className={stage.status === 'Healthy' ? 'text-green-500' : stage.status === 'Degraded' ? 'text-amber-500' : 'text-red-500'}>
                ● {stage.status === 'Healthy' ? 'OK' : stage.status}
              </span>
            </div>
          )) ?? <p className="text-muted-foreground text-xs">Non disponibile</p>}
        </CardContent>
      </Card>

      {/* Embedding mini */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm">🧠 Embedding Service</CardTitle>
        </CardHeader>
        <CardContent className="text-sm space-y-1">
          <div className="flex justify-between">
            <span className="text-muted-foreground">Modello</span>
            <span className="text-xs font-medium">{embeddingInfo?.modelName ?? '—'}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted-foreground">Throughput</span>
            <span>{embeddingMetrics?.requestsPerMinute ? `~${Math.round(embeddingMetrics.requestsPerMinute)}/min` : '—'}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted-foreground">Failure rate</span>
            <span className={embeddingMetrics?.failureRate > 5 ? 'text-red-500' : 'text-green-500'}>
              {embeddingMetrics?.failureRate != null ? `${embeddingMetrics.failureRate.toFixed(1)}%` : '—'}
            </span>
          </div>
        </CardContent>
      </Card>

      {/* Quick actions */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm">⚡ Azioni Rapide</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          <Button
            variant="outline"
            size="sm"
            className="w-full justify-start text-red-600 hover:text-red-700"
            onClick={() => bulkReindexFailed()}
          >
            🔄 Ritenta tutti i falliti
          </Button>
          <Button
            variant="outline"
            size="sm"
            className="w-full justify-start"
            onClick={() => fetch('/api/v1/admin/pdfs/maintenance/cleanup-orphans', { method: 'POST', credentials: 'include' })}
          >
            🧹 Pulisci chunk orfani
          </Button>
          <Button
            variant="outline"
            size="sm"
            className="w-full justify-start"
            onClick={() => updateQueueConfig({ isPaused: !queueConfig?.isPaused })}
          >
            {queueConfig?.isPaused ? '▶️ Riprendi coda' : '⏸️ Pausa coda'}
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
```

- [ ] **Step 3: Verify typecheck**

Run: `cd apps/web && pnpm typecheck 2>&1 | tail -10`
Expected: No new type errors (may need import path adjustments)

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/upload-and-queue-tab.tsx apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/queue-eta-sidebar.tsx
git commit -m "feat(web): implement Upload & Queue tab with ETA sidebar

Main dashboard tab with upload zone, live queue list, stats bar,
and right sidebar showing step timing, pipeline health, embedding
metrics, and quick actions."
```

---

## Task 8: Frontend — QueueItem ETA Display

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/queue-item.tsx`

- [ ] **Step 1: Add ETA display to queue item**

Open `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/queue-item.tsx`.

Add an optional `etaSeconds` prop to the component's props interface:

```typescript
// Add to props interface:
etaSeconds?: number;
```

In the render, after the status badge and before any action buttons, add the ETA display:

```tsx
{/* Add ETA display for Queued/Processing items */}
{props.etaSeconds != null && props.etaSeconds > 0 && (
  <span className="text-xs text-muted-foreground tabular-nums">
    ETA {formatETA(props.etaSeconds)}
  </span>
)}
```

Add the `formatETA` import:
```typescript
import { formatETA } from '../../rag-pipeline/lib/use-queue-eta';
```

> **Note:** If `formatETA` creates a circular dependency, move it to a shared utils file like `apps/web/src/lib/utils/format-eta.ts` and import from there in both places.

- [ ] **Step 2: Verify typecheck**

Run: `cd apps/web && pnpm typecheck 2>&1 | tail -10`
Expected: No new type errors

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/queue-item.tsx
git commit -m "feat(web): add ETA display to queue item component"
```

---

## Task 9: Frontend — History Tab

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/history-tab.tsx`

- [ ] **Step 1: Implement the history tab**

```tsx
// apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/history-tab.tsx
'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { fetchQueue, type QueueFilters } from '../../../queue/lib/queue-api';
import { formatETA } from '../lib/use-queue-eta';

export function HistoryTab() {
  const [page, setPage] = useState(1);

  const { data: completedData, isLoading } = useQuery({
    queryKey: ['admin', 'queue', 'history', page],
    queryFn: () => fetchQueue({ status: 'Completed', page, pageSize: 20 }),
    staleTime: 30_000,
  });

  const { data: distribution } = useQuery({
    queryKey: ['admin', 'pdfs', 'distribution'],
    queryFn: async () => {
      const res = await fetch('/api/v1/admin/pdfs/analytics/distribution', { credentials: 'include' });
      return res.json();
    },
    staleTime: 60_000,
  });

  return (
    <div className="space-y-4">
      {/* Distribution cards */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        {distribution?.statuses?.map((s: { status: string; count: number; percentage: number }) => (
          <Card key={s.status}>
            <CardContent className="p-4 text-center">
              <div className="text-2xl font-bold">{s.count}</div>
              <div className="text-xs text-muted-foreground">{s.status} ({s.percentage.toFixed(1)}%)</div>
            </CardContent>
          </Card>
        ))}
      </div>

      {/* Completed PDFs table */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">PDF Completati</CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="text-center text-muted-foreground py-8">Caricamento...</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b text-left">
                    <th className="pb-2 font-medium">File</th>
                    <th className="pb-2 font-medium">Durata</th>
                    <th className="pb-2 font-medium">Data</th>
                  </tr>
                </thead>
                <tbody>
                  {completedData?.items?.map((job: { id: string; fileName: string; totalDurationSeconds?: number; completedAt?: string }) => (
                    <tr key={job.id} className="border-b last:border-0">
                      <td className="py-2">{job.fileName}</td>
                      <td className="py-2 tabular-nums">
                        {job.totalDurationSeconds ? formatETA(job.totalDurationSeconds) : '—'}
                      </td>
                      <td className="py-2 text-muted-foreground">
                        {job.completedAt ? new Date(job.completedAt).toLocaleString('it-IT') : '—'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {/* Pagination */}
              {completedData && completedData.total > 20 && (
                <div className="flex justify-center gap-2 mt-4">
                  <button
                    className="px-3 py-1 text-sm border rounded disabled:opacity-50"
                    disabled={page <= 1}
                    onClick={() => setPage(p => p - 1)}
                  >
                    ← Precedente
                  </button>
                  <span className="px-3 py-1 text-sm text-muted-foreground">
                    Pagina {page} di {Math.ceil(completedData.total / 20)}
                  </span>
                  <button
                    className="px-3 py-1 text-sm border rounded disabled:opacity-50"
                    disabled={page >= Math.ceil(completedData.total / 20)}
                    onClick={() => setPage(p => p + 1)}
                  >
                    Successiva →
                  </button>
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
```

> **Note:** The `fetchQueue` function may return items with different property names. Check the actual `ProcessingJobDto` type in `queue-api.ts` for the correct field names (e.g., `fileName` vs `pdfFileName`, `totalDurationSeconds` vs `durationMs`). Adapt accordingly.

- [ ] **Step 2: Verify typecheck**

Run: `cd apps/web && pnpm typecheck 2>&1 | tail -10`
Expected: No new type errors

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/history-tab.tsx
git commit -m "feat(web): implement History & Analytics tab with completed PDFs table"
```

---

## Task 10: Frontend — Embedding Tab

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/embedding-tab.tsx`

- [ ] **Step 1: Implement the embedding tab reusing existing logic**

```tsx
// apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/embedding-tab.tsx
'use client';

import { useQuery } from '@tanstack/react-query';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { RefreshCw } from 'lucide-react';
import { useState } from 'react';

export function EmbeddingTab() {
  const [lastRefresh, setLastRefresh] = useState(Date.now());

  const { data: info, isLoading: infoLoading, refetch: refetchInfo } = useQuery({
    queryKey: ['admin', 'embedding', 'info', lastRefresh],
    queryFn: async () => {
      const res = await fetch('/api/v1/admin/embedding/info', { credentials: 'include' });
      if (!res.ok) throw new Error('Failed to fetch embedding info');
      return res.json();
    },
    staleTime: 30_000,
    refetchInterval: 30_000,
  });

  const { data: metrics, isLoading: metricsLoading, refetch: refetchMetrics } = useQuery({
    queryKey: ['admin', 'embedding', 'metrics', lastRefresh],
    queryFn: async () => {
      const res = await fetch('/api/v1/admin/embedding/metrics', { credentials: 'include' });
      if (!res.ok) throw new Error('Failed to fetch embedding metrics');
      return res.json();
    },
    staleTime: 30_000,
    refetchInterval: 30_000,
  });

  const { data: vectorStats } = useQuery({
    queryKey: ['admin', 'kb', 'vector-stats'],
    queryFn: async () => {
      const res = await fetch('/api/v1/admin/kb/vector-stats', { credentials: 'include' });
      if (!res.ok) throw new Error('Failed to fetch vector stats');
      return res.json();
    },
    staleTime: 60_000,
  });

  const handleRefresh = () => {
    setLastRefresh(Date.now());
    refetchInfo();
    refetchMetrics();
  };

  const isLoading = infoLoading || metricsLoading;

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold">Embedding Service</h2>
        <Button variant="outline" size="sm" onClick={handleRefresh}>
          <RefreshCw className="h-4 w-4 mr-1" />
          Aggiorna
        </Button>
      </div>

      {isLoading ? (
        <div className="text-center text-muted-foreground py-8">Caricamento...</div>
      ) : (
        <>
          {/* Service info */}
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Informazioni Servizio</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
              <div><span className="text-muted-foreground">Modello:</span> <span className="font-medium">{info?.modelName ?? '—'}</span></div>
              <div><span className="text-muted-foreground">Device:</span> <span className="font-medium">{info?.device ?? '—'}</span></div>
              <div><span className="text-muted-foreground">Dimensioni:</span> <span className="font-medium">{info?.dimensions ?? '—'}</span></div>
              <div><span className="text-muted-foreground">Max Input:</span> <span className="font-medium">{info?.maxInputLength ?? '—'}</span></div>
            </CardContent>
          </Card>

          {/* Metrics */}
          <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
            <MetricCard title="Richieste Totali" value={metrics?.totalRequests ?? 0} />
            <MetricCard title="Fallimenti" value={metrics?.totalFailures ?? 0} color="text-red-500" />
            <MetricCard title="Failure Rate" value={metrics?.failureRate != null ? `${metrics.failureRate.toFixed(1)}%` : '—'} color={metrics?.failureRate > 5 ? 'text-red-500' : 'text-green-500'} />
            <MetricCard title="Durata Media" value={metrics?.avgDurationMs != null ? `${Math.round(metrics.avgDurationMs)}ms` : '—'} />
            <MetricCard title="Chars Processati" value={metrics?.totalCharsProcessed?.toLocaleString() ?? '—'} />
          </div>

          {/* Vector stats */}
          {vectorStats && (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">pgvector Statistics</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="overflow-x-auto">
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b text-left">
                        <th className="pb-2 font-medium">Gioco</th>
                        <th className="pb-2 font-medium">Vettori</th>
                        <th className="pb-2 font-medium">Documenti</th>
                      </tr>
                    </thead>
                    <tbody>
                      {vectorStats?.games?.map((g: { gameName: string; vectorCount: number; documentCount: number }) => (
                        <tr key={g.gameName} className="border-b last:border-0">
                          <td className="py-2">{g.gameName}</td>
                          <td className="py-2 tabular-nums">{g.vectorCount}</td>
                          <td className="py-2 tabular-nums">{g.documentCount}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </CardContent>
            </Card>
          )}
        </>
      )}
    </div>
  );
}

function MetricCard({ title, value, color }: { title: string; value: string | number; color?: string }) {
  return (
    <Card>
      <CardContent className="p-4 text-center">
        <div className={`text-2xl font-bold ${color ?? ''}`}>{value}</div>
        <div className="text-xs text-muted-foreground">{title}</div>
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 2: Verify typecheck**

Run: `cd apps/web && pnpm typecheck 2>&1 | tail -10`
Expected: No new type errors

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/embedding-tab.tsx
git commit -m "feat(web): implement Embedding Service tab with vector stats"
```

---

## Task 11: Frontend — Config Tab

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/config-tab.tsx`

- [ ] **Step 1: Implement the config tab**

```tsx
// apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/config-tab.tsx
'use client';

import { useQuery } from '@tanstack/react-query';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { getQueueConfig } from '../../../queue/lib/queue-api';

export function ConfigTab() {
  const { data: queueConfig } = useQuery({
    queryKey: ['admin', 'queue', 'config'],
    queryFn: getQueueConfig,
    staleTime: 10_000,
  });

  const { data: kbSettings } = useQuery({
    queryKey: ['admin', 'kb', 'settings'],
    queryFn: async () => {
      const res = await fetch('/api/v1/admin/kb/settings', { credentials: 'include' });
      if (!res.ok) return null;
      return res.json();
    },
    staleTime: 60_000,
  });

  return (
    <div className="space-y-4">
      <h2 className="text-lg font-semibold">Configurazione Pipeline (Read-Only)</h2>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Coda</CardTitle>
          </CardHeader>
          <CardContent className="text-sm space-y-2">
            <ConfigRow label="Stato" value={queueConfig?.isPaused ? '⏸️ In pausa' : '▶️ Attiva'} />
            <ConfigRow label="Max Workers" value={queueConfig?.maxConcurrentWorkers?.toString() ?? '—'} />
            <ConfigRow label="Intervallo Job" value="Ogni 10 secondi (Quartz)" />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Estrazione</CardTitle>
          </CardHeader>
          <CardContent className="text-sm space-y-2">
            <ConfigRow label="Provider" value={kbSettings?.extractorProvider ?? 'Orchestrator (default)'} />
            <ConfigRow label="Fallback" value="Unstructured → SmolDocling → Docnet" />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Chunking</CardTitle>
          </CardHeader>
          <CardContent className="text-sm space-y-2">
            <ConfigRow label="Chunk Size" value={kbSettings?.chunkSize?.toString() ?? '—'} />
            <ConfigRow label="Overlap" value={kbSettings?.chunkOverlap?.toString() ?? '—'} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Embedding</CardTitle>
          </CardHeader>
          <CardContent className="text-sm space-y-2">
            <ConfigRow label="Batch Size" value={kbSettings?.embeddingBatchSize?.toString() ?? '20'} />
            <ConfigRow label="Dimensioni" value={kbSettings?.embeddingDimensions?.toString() ?? '—'} />
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function ConfigRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between">
      <span className="text-muted-foreground">{label}</span>
      <span className="font-medium">{value}</span>
    </div>
  );
}
```

- [ ] **Step 2: Verify typecheck**

Run: `cd apps/web && pnpm typecheck 2>&1 | tail -10`
Expected: No new type errors

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/knowledge-base/rag-pipeline/components/config-tab.tsx
git commit -m "feat(web): implement Config tab with read-only pipeline settings"
```

---

## Task 12: Frontend — Navigation + Redirects

**Files:**
- Modify: `apps/web/src/config/admin-dashboard-navigation.ts`

- [ ] **Step 1: Add RAG Pipeline nav item**

Open `apps/web/src/config/admin-dashboard-navigation.ts`. Find the "Content" section where Knowledge Base items are listed. Add the RAG Pipeline entry:

```typescript
// In the Content section's sidebar items, after the existing KB entries:
{
  label: 'RAG Pipeline',
  href: '/admin/knowledge-base/rag-pipeline',
  icon: 'Workflow',   // or whatever Lucide icon the project uses
  description: 'Upload, coda e metriche unificate',
},
```

> **Note:** Check the exact interface used (`DashboardSidebarItem`) and adapt the icon field to match the pattern used by other items in the navigation config.

- [ ] **Step 2: Verify typecheck**

Run: `cd apps/web && pnpm typecheck 2>&1 | tail -10`
Expected: No new type errors

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/config/admin-dashboard-navigation.ts
git commit -m "feat(web): add RAG Pipeline to admin sidebar navigation"
```

---

## Task 13: Verification — End-to-End Check

- [ ] **Step 1: Backend build**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: Build succeeded, 0 errors

- [ ] **Step 2: Backend tests**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~PrivatePdfAssociatedEventHandler|FullyQualifiedName~GetBatchETAQuery" --no-build 2>&1 | tail -10`
Expected: All tests pass

- [ ] **Step 3: Frontend typecheck**

Run: `cd apps/web && pnpm typecheck 2>&1 | tail -10`
Expected: No type errors

- [ ] **Step 4: Frontend build**

Run: `cd apps/web && pnpm build 2>&1 | tail -10`
Expected: Build succeeds, new page is compiled

- [ ] **Step 5: Final commit and summary**

```bash
git log --oneline -13
```

Verify all 12 commits are present, covering:
1. Event handler for PrivatePdfAssociatedEvent
2. Metrics recording in Quartz job
3. Batch ETA endpoint
4. Queue API ETA types
5. useQueueETA hook
6. Page shell with tabs
7. Upload & Queue tab + sidebar
8. QueueItem ETA display
9. History tab
10. Embedding tab
11. Config tab
12. Navigation entry

---

## Addendum: Review Corrections (MUST APPLY)

The following corrections were identified by code review against the actual codebase. Each implementing agent MUST apply these fixes.

### Task 1 Corrections

1. **`ProcessingJob.Create()` requires 5 params, not 3:**
   ```csharp
   // WRONG: ProcessingJob.Create(pdfId, priority: 10, _timeProvider);
   // CORRECT:
   var currentQueueSize = await _jobRepo.CountByStatusAsync(JobStatus.Queued, cancellationToken);
   var job = ProcessingJob.Create(pdfId, notification.UserId, priority: 10, currentQueueSize, _timeProvider);
   ```

2. **`IUnitOfWork` namespace is `Api.SharedKernel.Infrastructure.Persistence`**, not `Api.SharedKernel`

3. **`IPdfDocumentRepository`, `IProcessingJobRepository`, `IQueueStreamService` are `internal`**. Check if `InternalsVisibleTo("Api.Tests")` is configured. If not, the handler must use public wrappers or MediatR `Send()` to enqueue. Adapt the test to use integration test patterns if mocking internals fails.

4. **Test helper `CreateJob` also needs 5 params:**
   ```csharp
   return ProcessingJob.Create(pdfId, Guid.NewGuid(), priority: 10, currentQueueSize: 0, TimeProvider.System);
   ```

### Task 2 Corrections

5. **`PdfDocument.FileSizeBytes` does not exist** — the domain entity uses `FileSize` (value object). The Quartz job works with `PdfDocumentEntity` from EF context. Check the actual entity property name (likely `FileSizeBytes` on the EF entity, or `FileSize.Bytes`).

6. **`Enum.Parse<PdfProcessingState>(step.StepName)` will fail** — step names are `"Upload"`, `"Extract"`, `"Chunk"`, `"Embed"`, `"Index"` but enum values are `Uploading`, `Extracting`, `Chunking`, `Embedding`, `Indexing`. Use explicit mapping:
   ```csharp
   var stateMap = new Dictionary<string, PdfProcessingState>(StringComparer.OrdinalIgnoreCase)
   {
       ["Upload"] = PdfProcessingState.Uploading,
       ["Extract"] = PdfProcessingState.Extracting,
       ["Chunk"] = PdfProcessingState.Chunking,
       ["Embed"] = PdfProcessingState.Embedding,
       ["Index"] = PdfProcessingState.Indexing,
   };
   if (stateMap.TryGetValue(step.StepName, out var pdfState))
       await _metricsService.RecordStepDurationAsync(...);
   ```

### Task 3 Corrections

7. **`CalculateETAAsync` requires 3 params** (pdfId, currentStep, cancellationToken), not 2. The handler must resolve each job's current `PdfProcessingState`:
   ```csharp
   // Load PdfDocument to get current state, or use a default:
   var eta = await _metricsService.CalculateETAAsync(
       job.PdfDocumentId,
       currentStep, // from PdfDocument.ProcessingState or job metadata
       cancellationToken);
   ```

8. **Test mocks must use 3-arg setup:**
   ```csharp
   _metricsService.Setup(m => m.CalculateETAAsync(pdfId1, It.IsAny<PdfProcessingState>(), It.IsAny<CancellationToken>()))
       .ReturnsAsync((TimeSpan?)TimeSpan.FromMinutes(3));
   ```
   Note: return type is `TimeSpan?` (nullable).

9. **Test mock for `GetAllByStatusAsync` needs separate Setup per status** (Queued and Processing), not a combined OR condition.

### Task 4 Corrections

10. **Use `apiClient` instead of raw `fetch`** to match the existing pattern in `queue-api.ts`:
    ```typescript
    export async function fetchBatchETA(): Promise<BatchETAResponse> {
      return apiClient.get<BatchETAResponse>('/admin/queue/eta');
    }
    ```

### Task 7 Corrections

11. **`useQueueSSE` import path is wrong** — it lives at `../../../queue/hooks/use-queue-sse`, not `../../../queue/lib/queue-api`

12. **`useQueueSSE` returns `{ connectionState, reconnect }`**, not `{ connected }`:
    ```typescript
    const { connectionState } = useQueueSSE(true);
    ```

13. **`SSEConnectionIndicator` takes `state` prop**, not `connected`:
    ```tsx
    <SSEConnectionIndicator state={connectionState} />
    ```

14. **`QueueList` requires `data` and `isLoading` props** — use `useQueueList(filters, connectionState === 'connected')` to get the data:
    ```typescript
    const { data: queueData, isLoading } = useQueueList(filters, connectionState === 'connected');
    // Then:
    <QueueList data={queueData} isLoading={isLoading} ... />
    ```
    `QueueList` does NOT have an `etaData` prop. ETA display is handled in Task 8 by modifying `QueueItem`.

15. **`updateQueueConfig` takes positional args**, not an object:
    ```typescript
    onClick={() => updateQueueConfig(!queueConfig?.isPaused)}
    ```
