# Admin PDF→KB Workflow Improvements — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate admin context-switching in the SharedGame PDF→KB workflow by adding 4 inline UX improvements.

**Architecture:** Backend-first approach — new MediatR query + command modifications + SSE DTO enrichment, then frontend components consuming them. Each feature is independent and can be committed/tested separately.

**Tech Stack:** .NET 9 (MediatR, EF Core, SSE), Next.js 16 (React Query, sonner, shadcn/ui), XHR uploads

**Spec:** `docs/superpowers/specs/2026-03-15-admin-pdf-kb-workflow-design.md`

---

## Chunk 1: Feature 1 — Recently Processed Widget (Backend + Frontend)

### Task 1: Backend — GetRecentlyProcessedDocumentsQuery + DTO

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/DTOs/RecentlyProcessedDocumentDto.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetRecentlyProcessedDocuments/GetRecentlyProcessedDocumentsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetRecentlyProcessedDocuments/GetRecentlyProcessedDocumentsQueryHandler.cs`
- Reference: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetGameRagReadiness/GetGameRagReadinessQueryHandler.cs` (cross-BC pattern)

- [ ] **Step 1: Create the DTO**

```csharp
// RecentlyProcessedDocumentDto.cs
namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

public sealed record RecentlyProcessedDocumentDto(
    Guid PdfDocumentId,
    Guid? JobId,               // ProcessingJob.Id for retry action
    string FileName,
    string ProcessingState,
    DateTime Timestamp,
    string? ErrorCategory,
    bool CanRetry,
    Guid SharedGameId,
    string GameName,
    string? ThumbnailUrl);
```

- [ ] **Step 2: Create the query record**

```csharp
// GetRecentlyProcessedDocumentsQuery.cs
namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetRecentlyProcessedDocuments;

public sealed record GetRecentlyProcessedDocumentsQuery(int Limit = 10)
    : IRequest<List<RecentlyProcessedDocumentDto>>;
```

- [ ] **Step 3: Write the failing test for the handler**

Create test at `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Queries/GetRecentlyProcessedDocumentsQueryHandlerTests.cs`:

```csharp
[Fact]
public async Task Handle_ReturnsDocumentsOrderedByTimestampDesc()
{
    // Arrange: seed 3 PdfDocuments linked to SharedGames via SharedGameDocuments
    // with different CompletedAt timestamps
    // Act: send GetRecentlyProcessedDocumentsQuery(Limit: 2)
    // Assert: returns 2 items, ordered by most recent first
}

[Fact]
public async Task Handle_ExcludesDocumentsWithoutSharedGame()
{
    // Arrange: seed 1 PdfDocument WITH SharedGameDocument, 1 WITHOUT
    // Act: send query
    // Assert: returns only the one with SharedGameDocument
}

[Fact]
public async Task Handle_MapsProcessingStateCorrectly()
{
    // Arrange: seed PdfDocument with ProcessingState = "Ready"
    // Act: send query
    // Assert: result.ProcessingState == "Ready"
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `cd apps/api && dotnet test --filter "GetRecentlyProcessedDocumentsQueryHandler" -v n`
Expected: FAIL — handler class does not exist yet

- [ ] **Step 5: Implement the handler**

```csharp
// GetRecentlyProcessedDocumentsQueryHandler.cs
namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetRecentlyProcessedDocuments;

public sealed class GetRecentlyProcessedDocumentsQueryHandler
    : IRequestHandler<GetRecentlyProcessedDocumentsQuery, List<RecentlyProcessedDocumentDto>>
{
    private readonly MeepleAiDbContext _db;

    public GetRecentlyProcessedDocumentsQueryHandler(MeepleAiDbContext db) => _db = db;

    public async Task<List<RecentlyProcessedDocumentDto>> Handle(
        GetRecentlyProcessedDocumentsQuery request, CancellationToken ct)
    {
        // Cross-BC read query (deliberate, follows GetGameRagReadinessQueryHandler pattern)
        return await _db.SharedGameDocuments
            .Where(sgd => sgd.PdfDocumentId != null)
            .Join(_db.PdfDocuments,
                sgd => sgd.PdfDocumentId,
                pd => pd.Id,
                (sgd, pd) => new { sgd, pd })
            .Join(_db.SharedGames,
                x => x.sgd.SharedGameId,
                sg => sg.Id,
                (x, sg) => new RecentlyProcessedDocumentDto(
                    x.pd.Id,
                    x.pd.FileName,
                    x.pd.ProcessingState.ToString(),
                    x.pd.CompletedAt ?? x.pd.UpdatedAt,
                    x.pd.ErrorCategory != null ? x.pd.ErrorCategory.ToString() : null,
                    x.pd.CanRetry,
                    sg.Id,
                    sg.Name,
                    sg.ThumbnailUrl))
            .OrderByDescending(d => d.Timestamp)
            .Take(request.Limit)
            .ToListAsync(ct);
    }
}
```

Note: Adapt the LINQ to match actual EF entity properties and navigation patterns in `MeepleAiDbContext`. Use `GetGameRagReadinessQueryHandler.cs` as reference for how cross-BC joins are done in this codebase.

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd apps/api && dotnet test --filter "GetRecentlyProcessedDocumentsQueryHandler" -v n`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/DTOs/RecentlyProcessedDocumentDto.cs
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetRecentlyProcessedDocuments/
git add apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Queries/GetRecentlyProcessedDocumentsQueryHandlerTests.cs
git commit -m "feat(shared-games): add GetRecentlyProcessedDocumentsQuery cross-BC read"
```

---

### Task 2: Backend — Register endpoint in AdminSharedGameContentEndpoints

**Files:**
- Modify: `apps/api/src/Api/Routing/AdminSharedGameContentEndpoints.cs`

- [ ] **Step 1: Add the endpoint registration**

In `AdminSharedGameContentEndpoints.cs`, inside `MapAdminSharedGameContentEndpoints()`, add after the existing route group setup:

```csharp
group.MapGet("/recently-processed", async (
    [FromQuery] int? limit,
    IMediator mediator,
    CancellationToken ct) =>
{
    var result = await mediator.Send(
        new GetRecentlyProcessedDocumentsQuery(limit ?? 10), ct);
    return Results.Ok(result);
})
.WithName("GetRecentlyProcessedDocuments")
.WithDescription("Get recently processed PDF documents for SharedGames")
.Produces<List<RecentlyProcessedDocumentDto>>();
```

- [ ] **Step 2: Verify the API builds and responds**

Run: `cd apps/api/src/Api && dotnet build`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Routing/AdminSharedGameContentEndpoints.cs
git commit -m "feat(shared-games): register GET /recently-processed endpoint"
```

---

### Task 3: Frontend — API client method + RecentlyProcessedWidget

**Files:**
- Modify: `apps/web/src/lib/api/clients/sharedGamesClient.ts` (~line 873, after `getGameRagReadiness`)
- Create: `apps/web/src/components/admin/shared-games/RecentlyProcessedWidget.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/shared-games/all/page.tsx`

- [ ] **Step 1: Add API client method**

In `sharedGamesClient.ts`, add after the last method:

```typescript
async getRecentlyProcessed(limit: number = 10): Promise<RecentlyProcessedDocument[]> {
  const res = await this.http.get<RecentlyProcessedDocument[]>(
    `/api/v1/admin/shared-games/recently-processed?limit=${limit}`
  );
  return res ?? [];
},
```

Add the type near the top with other interfaces:

```typescript
export interface RecentlyProcessedDocument {
  pdfDocumentId: string;
  jobId: string | null;
  fileName: string;
  processingState: string;
  timestamp: string;
  errorCategory: string | null;
  canRetry: boolean;
  sharedGameId: string;
  gameName: string;
  thumbnailUrl: string | null;
}
```

- [ ] **Step 2: Write the failing test for RecentlyProcessedWidget**

Create `apps/web/src/components/admin/shared-games/__tests__/RecentlyProcessedWidget.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

// Mock api client
const mockGetRecentlyProcessed = vi.fn();
vi.mock('@/lib/api', () => ({
  apiClient: {
    sharedGames: { getRecentlyProcessed: mockGetRecentlyProcessed },
  },
}));

// Mock React Query
vi.mock('@tanstack/react-query', async () => {
  const actual = await vi.importActual('@tanstack/react-query');
  return { ...actual };
});

describe('RecentlyProcessedWidget', () => {
  it('renders table with PDF rows', async () => {
    // ...setup with QueryClientProvider + mock data
    // Assert: file name, game name, status badge visible
  });

  it('shows "Indicizzato" badge for Ready state', async () => {
    // Mock data with processingState: "Ready"
    // Assert: screen.getByText('Indicizzato')
  });

  it('shows "Elaborazione" badge for processing states', async () => {
    // Mock data with processingState: "Extracting"
    // Assert: screen.getByText('Elaborazione')
  });

  it('shows "Fallito" badge with retry button for Failed state', async () => {
    // Mock data with processingState: "Failed", canRetry: true
    // Assert: screen.getByText('Fallito'), screen.getByText('Riprova')
  });

  it('collapses and persists state in localStorage', async () => {
    // Render, click collapse toggle
    // Assert: localStorage.setItem called with 'admin:recentPdfs:collapsed'
  });

  it('renders empty state when no documents', async () => {
    // Mock empty array
    // Assert: widget not rendered or shows empty message
  });
});
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `cd apps/web && pnpm test -- --run RecentlyProcessedWidget`
Expected: FAIL — component does not exist

- [ ] **Step 4: Implement RecentlyProcessedWidget**

Create `apps/web/src/components/admin/shared-games/RecentlyProcessedWidget.tsx`:

```tsx
'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ChevronDown, ChevronUp, FileText, ExternalLink, RotateCcw, Loader2 } from 'lucide-react';
import Link from 'next/link';
import { apiClient } from '@/lib/api';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { toast } from 'sonner';
import type { RecentlyProcessedDocument } from '@/lib/api/clients/sharedGamesClient';

function getStatusBadge(state: string) {
  if (state === 'Ready') return <Badge variant="default" className="bg-green-100 text-green-800">Indicizzato</Badge>;
  if (state === 'Failed') return <Badge variant="destructive">Fallito</Badge>;
  return (
    <Badge variant="secondary" className="bg-blue-100 text-blue-800">
      <Loader2 className="mr-1 h-3 w-3 animate-spin" />
      Elaborazione
    </Badge>
  );
}

function formatRelativeTime(timestamp: string): string {
  const diff = Date.now() - new Date(timestamp).getTime();
  const minutes = Math.floor(diff / 60000);
  if (minutes < 1) return 'Ora';
  if (minutes < 60) return `${minutes}min fa`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h fa`;
  return `${Math.floor(hours / 24)}g fa`;
}

export function RecentlyProcessedWidget() {
  const [collapsed, setCollapsed] = useState(() =>
    typeof window !== 'undefined'
      ? localStorage.getItem('admin:recentPdfs:collapsed') === 'true'
      : false
  );

  const { data: documents = [], isLoading } = useQuery({
    queryKey: ['admin', 'recently-processed'],
    queryFn: () => apiClient.sharedGames.getRecentlyProcessed(10),
    refetchInterval: 15_000,
  });

  const toggleCollapse = () => {
    const next = !collapsed;
    setCollapsed(next);
    localStorage.setItem('admin:recentPdfs:collapsed', String(next));
  };

  const handleRetry = async (jobId: string) => {
    try {
      await apiClient.admin.retryJob(jobId);
      toast.success('Riprocessamento avviato');
    } catch {
      toast.error('Errore nel riprocessamento');
    }
  };

  if (!isLoading && documents.length === 0) return null;

  return (
    <Card className="mb-6">
      <CardHeader className="cursor-pointer flex flex-row items-center justify-between py-3" onClick={toggleCollapse}>
        <CardTitle className="text-base font-medium">Ultimi PDF elaborati</CardTitle>
        {collapsed ? <ChevronDown className="h-4 w-4" /> : <ChevronUp className="h-4 w-4" />}
      </CardHeader>
      {!collapsed && (
        <CardContent className="pt-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-4">
              <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          ) : (
            <>
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b text-left text-muted-foreground">
                      <th className="pb-2 font-medium">PDF</th>
                      <th className="pb-2 font-medium">Gioco</th>
                      <th className="pb-2 font-medium">Stato</th>
                      <th className="pb-2 font-medium">Tempo</th>
                      <th className="pb-2 font-medium">Azione</th>
                    </tr>
                  </thead>
                  <tbody>
                    {documents.map((doc) => (
                      <tr key={doc.pdfDocumentId} className="border-b last:border-0">
                        <td className="py-2">
                          <div className="flex items-center gap-2">
                            <FileText className="h-4 w-4 text-muted-foreground" />
                            <span className="truncate max-w-[200px]" title={doc.fileName}>
                              {doc.fileName}
                            </span>
                          </div>
                        </td>
                        <td className="py-2">
                          <Link
                            href={`/admin/shared-games/${doc.sharedGameId}`}
                            className="text-primary hover:underline flex items-center gap-1"
                          >
                            {doc.thumbnailUrl && (
                              <img src={doc.thumbnailUrl} alt="" className="h-6 w-6 rounded object-cover" />
                            )}
                            <span className="truncate max-w-[150px]">{doc.gameName}</span>
                          </Link>
                        </td>
                        <td className="py-2">{getStatusBadge(doc.processingState)}</td>
                        <td className="py-2 text-muted-foreground">{formatRelativeTime(doc.timestamp)}</td>
                        <td className="py-2">
                          {doc.processingState === 'Ready' && (
                            <Button variant="ghost" size="sm" asChild>
                              <Link href={`/admin/shared-games/${doc.sharedGameId}`}>
                                <ExternalLink className="h-3 w-3 mr-1" />Vai al gioco
                              </Link>
                            </Button>
                          )}
                          {doc.processingState === 'Failed' && doc.canRetry && doc.jobId && (
                            <Button variant="ghost" size="sm" onClick={() => handleRetry(doc.jobId!)}>
                              <RotateCcw className="h-3 w-3 mr-1" />Riprova
                            </Button>
                          )}
                          {doc.processingState !== 'Ready' && doc.processingState !== 'Failed' && (
                            <Button variant="ghost" size="sm" asChild>
                              <Link href="/admin/knowledge-base/queue">
                                <ExternalLink className="h-3 w-3 mr-1" />Vai alla coda
                              </Link>
                            </Button>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="mt-3 text-right">
                <Link href="/admin/knowledge-base/documents" className="text-sm text-primary hover:underline">
                  Vedi tutti →
                </Link>
              </div>
            </>
          )}
        </CardContent>
      )}
    </Card>
  );
}
```

- [ ] **Step 5: Mount widget in the SharedGames all page**

In `apps/web/src/app/admin/(dashboard)/shared-games/all/page.tsx`, add import and render above `GameCatalogGrid`:

```tsx
import { RecentlyProcessedWidget } from '@/components/admin/shared-games/RecentlyProcessedWidget';

// Inside the component JSX, before the first <Suspense>:
<RecentlyProcessedWidget />
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd apps/web && pnpm test -- --run RecentlyProcessedWidget`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/lib/api/clients/sharedGamesClient.ts
git add apps/web/src/components/admin/shared-games/RecentlyProcessedWidget.tsx
git add apps/web/src/components/admin/shared-games/__tests__/RecentlyProcessedWidget.test.tsx
git add apps/web/src/app/admin/(dashboard)/shared-games/all/page.tsx
git commit -m "feat(admin): add RecentlyProcessedWidget to SharedGames landing"
```

---

## Chunk 2: Feature 2 — Priority Urgent Selector (Backend + Frontend)

### Task 4: Backend — Add Priority to UploadPdfCommand

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommand.cs` (line 11-17)
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Validators/UploadPdfCommandValidator.cs`
- Modify: `apps/api/src/Api/Routing/PdfEndpoints.cs` (MapStandardUploadEndpoint — pass priority from query string)

- [ ] **Step 1: Write the failing test**

Create/extend test at `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandTests.cs`:

```csharp
[Fact]
public async Task Handle_WithUrgentPriority_SetsProcessingJobPriorityToUrgent()
{
    // Arrange: UploadPdfCommand with Priority = "urgent", admin user
    // Act: send command
    // Assert: ProcessingJob.Priority == ProcessingPriority.Urgent
}

[Fact]
public async Task Handle_WithNullPriority_DefaultsToNormalPriority()
{
    // Arrange: UploadPdfCommand with Priority = null
    // Act: send command
    // Assert: ProcessingJob.Priority == ProcessingPriority.Normal (or High for admin)
}

[Fact]
public async Task Validator_RejectsInvalidPriorityValues()
{
    // Arrange: UploadPdfCommand with Priority = "invalid"
    // Act: validate
    // Assert: validation error
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/api && dotnet test --filter "UploadPdfCommand" -v n`
Expected: FAIL

- [ ] **Step 3: Add Priority property to UploadPdfCommand**

In `UploadPdfCommand.cs`, add to the existing `internal record` (keep the existing access modifier):

```csharp
internal record UploadPdfCommand(
    // ... existing properties unchanged
    string? Priority = null   // "normal" | "urgent" | null
) : ICommand<PdfUploadResult>;
```

**Important**: Do NOT change `internal record` to `public sealed record`. Match existing codebase conventions.

- [ ] **Step 4: Create UploadPdfCommandValidator**

```csharp
// UploadPdfCommandValidator.cs
namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

internal sealed class UploadPdfCommandValidator : AbstractValidator<UploadPdfCommand>
{
    private static readonly string[] ValidPriorities = ["normal", "urgent"];

    public UploadPdfCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.UserId).NotEmpty();
        When(x => x.Priority != null, () =>
        {
            RuleFor(x => x.Priority)
                .Must(p => ValidPriorities.Contains(p!.ToLowerInvariant()))
                .WithMessage("Priority must be 'normal' or 'urgent'");
        });
    }
}
```

- [ ] **Step 5: Modify UploadPdfCommandHandler to respect priority**

In `UploadPdfCommandHandler.cs`, where `ProcessingJob` is created, add priority mapping:

```csharp
var priority = request.Priority?.ToLowerInvariant() == "urgent"
    ? ProcessingPriority.Urgent
    : ProcessingPriority.High; // Admin default
// Apply to the ProcessingJob creation
```

- [ ] **Step 6: Pass priority from endpoint to command**

In `PdfEndpoints.cs`, in the `HandleStandardUpload` private static method, read priority from the query string before constructing `UploadPdfCommand`:

```csharp
// Inside HandleStandardUpload body, before constructing UploadPdfCommand:
var priority = context.Request.Query["priority"].FirstOrDefault();

// Then pass it when constructing the command (~line 455):
var command = new UploadPdfCommand(
    // ... existing parameters unchanged
    Priority: priority
);
```

**Note**: `HandleStandardUpload` reads form data from `context.Request.ReadFormAsync()`, not from method parameters. The priority comes from query string, not form data. Do NOT change the method signature.

- [ ] **Step 7: Run tests to verify they pass**

Run: `cd apps/api && dotnet test --filter "UploadPdfCommand" -v n`
Expected: PASS

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommand.cs
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.cs
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Validators/UploadPdfCommandValidator.cs
git add apps/api/src/Api/Routing/PdfEndpoints.cs
git add apps/api/tests/
git commit -m "feat(doc-processing): add priority parameter to PDF upload command"
```

---

### Task 5: Frontend — Priority selector in PdfUploadSection

**Files:**
- Modify: `apps/web/src/components/admin/shared-games/PdfUploadSection.tsx` (lines 34-51 props, lines 210-317 upload)

- [ ] **Step 1: Write the failing test**

Add to existing or create `apps/web/src/components/admin/shared-games/__tests__/PdfUploadSection.priority.test.tsx`:

```typescript
describe('PdfUploadSection priority selector', () => {
  it('shows priority selector after file selection when showPrioritySelector=true', async () => {
    // Render with showPrioritySelector={true}
    // Select a file
    // Assert: screen.getByText('Normale') or screen.getByRole('combobox')
  });

  it('does not show priority selector when showPrioritySelector=false', async () => {
    // Render with showPrioritySelector={false}
    // Select a file
    // Assert: no priority selector
  });

  it('sends priority=urgent query param when Urgente selected', async () => {
    // Render, select file, pick "Urgente", click upload
    // Assert: XHR open called with /api/v1/ingest/pdf?priority=urgent
  });

  it('defaults to no priority param when Normale selected', async () => {
    // Render, select file, keep "Normale", click upload
    // Assert: XHR open called with /api/v1/ingest/pdf (no query param)
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm test -- --run PdfUploadSection.priority`
Expected: FAIL

- [ ] **Step 3: Add priority selector to PdfUploadSection**

In `PdfUploadSection.tsx`:

1. Add prop `showPrioritySelector?: boolean` (default `true`) to `PdfUploadSectionProps` interface (~line 34)
2. Add state: `const [priority, setPriority] = useState<'normal' | 'urgent'>('normal');`
3. After file validation area (~line 384), render the selector when `showPrioritySelector && selectedFile`:

```tsx
{showPrioritySelector && selectedFile && !isUploading && (
  <div className="flex items-center gap-2 mt-3">
    <Select value={priority} onValueChange={(v) => setPriority(v as 'normal' | 'urgent')}>
      <SelectTrigger className="w-[260px]">
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="normal">Normale — Elaborazione standard</SelectItem>
        <SelectItem value="urgent">
          <span className="flex items-center gap-1 text-amber-600">
            <Zap className="h-3 w-3" />Urgente — in testa alla coda
          </span>
        </SelectItem>
      </SelectContent>
    </Select>
  </div>
)}
```

4. In `handleUpload()` (~line 261), modify XHR URL:

```typescript
const url = priority === 'urgent'
  ? '/api/v1/ingest/pdf?priority=urgent'
  : '/api/v1/ingest/pdf';
xhr.open('POST', url);
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm test -- --run PdfUploadSection.priority`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/shared-games/PdfUploadSection.tsx
git add apps/web/src/components/admin/shared-games/__tests__/PdfUploadSection.priority.test.tsx
git commit -m "feat(admin): add priority selector to PdfUploadSection"
```

---

## Chunk 3: Feature 3 — Mini-Queue Widget (Backend + Frontend)

### Task 6: Backend — Add GameId filter to GetProcessingQueueQuery

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/Queue/GetProcessingQueueQuery.cs` (line 10-17)
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Handlers/Queue/GetProcessingQueueQueryHandler.cs` (handler is in Handlers/, not Queries/)

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task Handle_WithGameIdFilter_ReturnsOnlyJobsForThatGame()
{
    // Arrange: seed 2 jobs — one linked to SharedGame A, one to SharedGame B
    // Act: send GetProcessingQueueQuery with GameId = SharedGame A's ID
    // Assert: returns only the job for SharedGame A
}

[Fact]
public async Task Handle_WithoutGameIdFilter_ReturnsAllJobs()
{
    // Arrange: seed 2 jobs for different games
    // Act: send GetProcessingQueueQuery without GameId
    // Assert: returns both jobs
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/api && dotnet test --filter "GetProcessingQueueQuery" -v n`
Expected: FAIL

- [ ] **Step 3: Add GameId to the query record**

In `GetProcessingQueueQuery.cs`:

```csharp
public sealed record GetProcessingQueueQuery(
    string? StatusFilter = null,
    string? SearchText = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    Guid? GameId = null,       // NEW — filter by SharedGame
    int Page = 1,
    int PageSize = 20
) : IQuery<PaginatedQueueResponse>;
```

- [ ] **Step 4: Implement the GameId filter in the handler**

In the handler's `Handle` method, add a conditional join when `GameId` is provided:

```csharp
if (request.GameId.HasValue)
{
    query = query.Where(j =>
        _db.SharedGameDocuments
            .Any(sgd => sgd.PdfDocumentId == j.PdfDocumentId
                      && sgd.SharedGameId == request.GameId.Value));
}
```

- [ ] **Step 5: Pass GameId from the admin queue endpoint**

In `AdminQueueEndpoints.cs`, add `[FromQuery] Guid? gameId = null` to the GET queue handler and pass it to the query.

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd apps/api && dotnet test --filter "GetProcessingQueueQuery" -v n`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/Queue/
git add apps/api/src/Api/Routing/AdminQueueEndpoints.cs
git add apps/api/tests/
git commit -m "feat(doc-processing): add GameId filter to GetProcessingQueueQuery"
```

---

### Task 7: Frontend — GameProcessingQueue component

**Files:**
- Modify: `apps/web/src/lib/api/clients/adminClient.ts` — add `getQueueJobs()` method
- Create: `apps/web/src/components/admin/shared-games/GameProcessingQueue.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/shared-games/[id]/client.tsx`

- [ ] **Step 0: Add getQueueJobs to adminClient**

In `apps/web/src/lib/api/clients/adminClient.ts`, add:

```typescript
interface QueueJobsParams {
  gameId?: string;
  limit?: number;
  status?: string[];
}

async getQueueJobs(params: QueueJobsParams) {
  const searchParams = new URLSearchParams();
  if (params.gameId) searchParams.set('gameId', params.gameId);
  if (params.limit) searchParams.set('pageSize', String(params.limit));
  if (params.status) searchParams.set('statusFilter', params.status.join(','));
  const res = await this.http.get(`/api/v1/admin/queue?${searchParams}`);
  return res;
},
```

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/admin/shared-games/__tests__/GameProcessingQueue.test.tsx`:

```typescript
describe('GameProcessingQueue', () => {
  it('renders game-specific and global queue counts', async () => {
    // Mock: 3 jobs for this game, 12 total
    // Assert: "3 di questo gioco · 12 totali"
  });

  it('renders job rows with priority badges and progress', async () => {
    // Mock: 1 Urgent processing at 67%, 1 Normal queued
    // Assert: "Urgente" badge, progress bar, "In coda" text
  });

  it('renders deep-link to full queue with gameId param', async () => {
    // Assert: link href contains /admin/knowledge-base/queue?gameId=
  });

  it('renders empty state when no jobs', async () => {
    // Mock: empty jobs, 0 global
    // Assert: widget hidden or "Nessun documento in coda"
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm test -- --run GameProcessingQueue`
Expected: FAIL

- [ ] **Step 3: Implement GameProcessingQueue**

Create `apps/web/src/components/admin/shared-games/GameProcessingQueue.tsx`:

```tsx
'use client';

import { useQuery } from '@tanstack/react-query';
import { ExternalLink, Loader2 } from 'lucide-react';
import Link from 'next/link';
import { apiClient } from '@/lib/api';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';

const PRIORITY_CONFIG: Record<number, { label: string; className: string }> = {
  30: { label: 'Urgente', className: 'bg-red-100 text-red-800' },
  20: { label: 'Alta', className: 'bg-amber-100 text-amber-800' },
  10: { label: 'Normale', className: 'bg-blue-100 text-blue-800' },
  0: { label: 'Bassa', className: 'bg-gray-100 text-gray-800' },
};

interface GameProcessingQueueProps {
  gameId: string;
}

export function GameProcessingQueue({ gameId }: GameProcessingQueueProps) {
  const { data: queueData } = useQuery({
    queryKey: ['admin', 'queue', 'game', gameId],
    queryFn: () => apiClient.admin.getQueueJobs({
      gameId,
      limit: 5,
      status: ['Queued', 'Processing'],
    }),
    refetchInterval: 10_000,
  });

  const { data: queueStatus } = useQuery({
    queryKey: ['admin', 'queue', 'status'],
    queryFn: () => apiClient.admin.getQueueStatus(),
    refetchInterval: 10_000,
  });

  const jobs = queueData?.items ?? [];
  const gameCount = queueData?.totalCount ?? 0;
  const globalCount = queueStatus?.queueDepth ?? 0;

  if (gameCount === 0 && globalCount === 0) return null;

  return (
    <Card className="mt-4">
      <CardHeader className="py-3">
        <div className="flex items-center justify-between">
          <CardTitle className="text-base font-medium">Coda elaborazione</CardTitle>
          <span className="text-sm text-muted-foreground">
            <strong>{gameCount}</strong> di questo gioco · <strong>{globalCount}</strong> totali
          </span>
        </div>
      </CardHeader>
      <CardContent className="pt-0">
        {jobs.length === 0 ? (
          <p className="text-sm text-muted-foreground py-2">Nessun documento in coda per questo gioco</p>
        ) : (
          <div className="space-y-2">
            {jobs.map((job) => {
              const priorityCfg = PRIORITY_CONFIG[job.priority] ?? PRIORITY_CONFIG[10];
              return (
                <div key={job.id} className="flex items-center gap-3 text-sm">
                  <Badge variant="outline" className={priorityCfg.className}>
                    {priorityCfg.label}
                  </Badge>
                  <span className="truncate flex-1" title={job.fileName}>
                    {job.fileName}
                  </span>
                  <div className="w-24">
                    {job.status === 'Processing' && job.progress != null ? (
                      <Progress value={job.progress} className="h-2" />
                    ) : (
                      <span className="text-muted-foreground text-xs">In coda</span>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        )}
        <div className="mt-3 border-t pt-2">
          <Link
            href={`/admin/knowledge-base/queue?gameId=${gameId}`}
            className="text-sm text-primary hover:underline inline-flex items-center gap-1"
          >
            <ExternalLink className="h-3 w-3" />
            Apri coda completa →
          </Link>
        </div>
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 4: Mount in GameDetailClient Documents tab**

In `apps/web/src/app/admin/(dashboard)/shared-games/[id]/client.tsx`, import and add below `PdfUploadSection` in the Documents tab:

```tsx
import { GameProcessingQueue } from '@/components/admin/shared-games/GameProcessingQueue';

// Inside Documents tab, after PdfUploadSection:
<GameProcessingQueue gameId={gameId} />
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd apps/web && pnpm test -- --run GameProcessingQueue`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/admin/shared-games/GameProcessingQueue.tsx
git add apps/web/src/components/admin/shared-games/__tests__/GameProcessingQueue.test.tsx
git add apps/web/src/app/admin/(dashboard)/shared-games/[id]/client.tsx
git commit -m "feat(admin): add GameProcessingQueue mini-widget with deep-link"
```

---

## Chunk 4: Feature 4 — Toast Notifications (Backend SSE + Frontend)

### Task 8: Backend — Enrich SSE DTOs with game info

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/DTOs/QueueStreamEventDto.cs` (lines 57, 62)
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/EventHandlers/QueueStreamEventHandlers.cs` (lines 87-133)

- [ ] **Step 1: Write the failing test**

Create test at `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/EventHandlers/QueueStreamEventHandlerEnrichmentTests.cs`:

```csharp
[Fact]
public async Task JobCompletedStreamHandler_EnrichesWithGameInfo_WhenSharedGameDocumentExists()
{
    // Arrange: PdfDocument linked to SharedGameDocument → SharedGame "Catan"
    // Arrange: JobCompletedEvent for that PdfDocument
    // Act: handler publishes QueueStreamEvent
    // Assert: QueueStreamEvent.Data contains GameName = "Catan", SharedGameId, FileName
}

[Fact]
public async Task JobCompletedStreamHandler_HandlesNoSharedGame_Gracefully()
{
    // Arrange: PdfDocument NOT linked to any SharedGame
    // Act: handler publishes QueueStreamEvent
    // Assert: QueueStreamEvent.Data has null GameName and SharedGameId
}

// Same 2 tests for JobFailedStreamHandler
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/api && dotnet test --filter "StreamHandler" -v n`
Expected: FAIL

- [ ] **Step 3: Enhance SSE DTO records**

In `QueueStreamEventDto.cs`:

```csharp
// Line 57 — replace existing
public sealed record JobCompletedData(
    double TotalDurationSeconds,
    string? FileName,
    Guid? SharedGameId,
    string? GameName);

// Line 62 — replace existing
public sealed record JobFailedData(
    string Error,
    string? FailedAtStep,
    int RetryCount,
    string? FileName,
    Guid? SharedGameId,
    string? GameName);
```

- [ ] **Step 4: Enrich stream handlers with game lookup**

In `QueueStreamEventHandlers.cs`, modify `JobCompletedStreamHandler` (~line 87):

1. Inject `MeepleAiDbContext _db` in constructor
2. In `Handle()`, after receiving the domain event, resolve game info:

```csharp
var pdfDoc = await _db.PdfDocuments
    .Where(p => p.Id == notification.PdfDocumentId)
    .Select(p => new { p.FileName })
    .FirstOrDefaultAsync(ct);

var gameInfo = await _db.SharedGameDocuments
    .Where(sgd => sgd.PdfDocumentId == notification.PdfDocumentId)
    .Select(sgd => new { sgd.SharedGameId, GameName = sgd.SharedGame.Name })
    .FirstOrDefaultAsync(ct);

var data = new JobCompletedData(
    notification.TotalDuration.TotalSeconds,
    pdfDoc?.FileName,
    gameInfo?.SharedGameId,
    gameInfo?.GameName);
```

Apply same pattern to `JobFailedStreamHandler` (~line 111).

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd apps/api && dotnet test --filter "StreamHandler" -v n`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/DTOs/QueueStreamEventDto.cs
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/EventHandlers/QueueStreamEventHandlers.cs
git add apps/api/tests/
git commit -m "feat(doc-processing): enrich SSE JobCompleted/Failed DTOs with game info"
```

---

### Task 9: Frontend — PdfProcessingNotifier component

**Files:**
- Create: `apps/web/src/components/admin/layout/PdfProcessingNotifier.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/layout.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/admin/layout/__tests__/PdfProcessingNotifier.test.tsx`:

```typescript
describe('PdfProcessingNotifier', () => {
  it('shows success toast on JobCompleted SSE event', async () => {
    // Mock EventSource with JobCompleted event containing gameName + fileName
    // Assert: toast.success called with "rulebook.pdf per Catan"
  });

  it('shows error toast on JobFailed SSE event', async () => {
    // Mock EventSource with JobFailed event
    // Assert: toast.error called with "Elaborazione fallita"
  });

  it('ignores events without game info', async () => {
    // Mock EventSource with JobCompleted event where gameName is null
    // Assert: toast still shows with fileName only (no "per null")
  });

  it('cleans up SSE connection on unmount', async () => {
    // Render, then unmount
    // Assert: EventSource.close() called
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm test -- --run PdfProcessingNotifier`
Expected: FAIL

- [ ] **Step 3: Implement PdfProcessingNotifier**

Create `apps/web/src/components/admin/layout/PdfProcessingNotifier.tsx`:

```tsx
'use client';

import { useEffect, useRef } from 'react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';
import { apiClient } from '@/lib/api';

interface SseJobEvent {
  type: string;
  jobId: string;
  data: {
    fileName?: string;
    sharedGameId?: string;
    gameName?: string;
    error?: string;
  };
}

export function PdfProcessingNotifier() {
  const router = useRouter();
  const eventSourceRef = useRef<EventSource | null>(null);
  const reconnectTimeoutRef = useRef<ReturnType<typeof setTimeout>>();
  const reconnectDelayRef = useRef(1000);

  useEffect(() => {
    function connect() {
      const es = new EventSource('/api/v1/admin/queue/stream');
      eventSourceRef.current = es;

      // Note: If SSE server sends named events (e.g., `event: JobCompleted`),
      // use addEventListener instead of onmessage. Check IQueueStreamService
      // implementation. If events are unnamed, onmessage works.
      es.onmessage = (event) => {
        try {
          const parsed: SseJobEvent = JSON.parse(event.data);
          handleEvent(parsed);
        } catch { /* ignore parse errors */ }
      };

      es.onerror = () => {
        es.close();
        // Exponential backoff: 1s, 2s, 4s, ... max 30s
        reconnectTimeoutRef.current = setTimeout(() => {
          reconnectDelayRef.current = Math.min(reconnectDelayRef.current * 2, 30_000);
          connect();
        }, reconnectDelayRef.current);
      };

      es.onopen = () => {
        reconnectDelayRef.current = 1000; // Reset on successful connection
      };
    }

    function handleEvent(event: SseJobEvent) {
      const { fileName, sharedGameId, gameName, error } = event.data ?? {};

      if (event.type === 'JobCompleted') {
        const title = gameName
          ? `${fileName} per ${gameName}`
          : fileName ?? 'PDF';

        toast.success(title, {
          description: 'Indicizzazione completata',
          duration: 8000,
          action: sharedGameId ? {
            label: 'Vai al gioco',
            onClick: () => router.push(`/admin/shared-games/${sharedGameId}`),
          } : undefined,
        });
      }

      if (event.type === 'JobFailed') {
        const title = gameName
          ? `${fileName} per ${gameName}`
          : fileName ?? 'PDF';

        toast.error(title, {
          description: `Elaborazione fallita${error ? `: ${error}` : ''}`,
          duration: 8000,
          action: {
            label: 'Riprova',
            onClick: async () => {
              try {
                await apiClient.admin.retryJob(event.jobId);
                toast.success('Riprocessamento avviato');
              } catch { toast.error('Errore nel riprocessamento'); }
            },
          },
        });
      }
    }

    connect();

    return () => {
      eventSourceRef.current?.close();
      if (reconnectTimeoutRef.current) clearTimeout(reconnectTimeoutRef.current);
    };
  }, [router]);

  return null; // Invisible component — only side effects
}
```

- [ ] **Step 4: Mount in admin dashboard layout**

In `apps/web/src/app/admin/(dashboard)/layout.tsx`:

```tsx
import { PdfProcessingNotifier } from '@/components/admin/layout/PdfProcessingNotifier';

// Inside DashboardLayout, as sibling to UnifiedShell:
<PdfProcessingNotifier />
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd apps/web && pnpm test -- --run PdfProcessingNotifier`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/admin/layout/PdfProcessingNotifier.tsx
git add apps/web/src/components/admin/layout/__tests__/PdfProcessingNotifier.test.tsx
git add apps/web/src/app/admin/(dashboard)/layout.tsx
git commit -m "feat(admin): add PdfProcessingNotifier global SSE toast notifications"
```

---

## Chunk 5: Integration Testing + Final Validation

### Task 10: E2E smoke tests

**Files:**
- Create: `apps/web/e2e/admin/pdf-kb-workflow.spec.ts`

- [ ] **Step 1: Write E2E test scenarios**

**Important E2E conventions** (from MEMORY.md):
- Set `PLAYWRIGHT_AUTH_BYPASS=true` env var to bypass middleware session validation
- Use `page.context().route()` NOT `page.route()` for API mocking with Next.js dev server
- Use `.first()` on `getByText` when multiple elements may match (strict mode)
- Use error code 400 (not 500) for error mocks to avoid httpClient retry

```typescript
import { test, expect } from '@playwright/test';

test.describe('Admin PDF→KB Workflow', () => {
  test('RecentlyProcessedWidget visible on SharedGames landing', async ({ page }) => {
    await page.goto('/admin/shared-games/all');
    // Route mock for /recently-processed
    await expect(page.getByText('Ultimi PDF elaborati')).toBeVisible();
  });

  test('Priority selector appears during PDF upload', async ({ page }) => {
    await page.goto('/admin/shared-games/test-game-id');
    // Navigate to Documents tab, select file
    // Assert: priority selector visible with "Normale" and "Urgente"
  });

  test('GameProcessingQueue shows in game detail', async ({ page }) => {
    await page.goto('/admin/shared-games/test-game-id');
    // Assert: "Coda elaborazione" section visible
    // Assert: deep-link contains correct gameId
  });

  test('Deep-link navigates to queue with gameId filter', async ({ page }) => {
    await page.goto('/admin/shared-games/test-game-id');
    const link = page.getByText('Apri coda completa →');
    await expect(link).toHaveAttribute('href', /gameId=/);
  });
});
```

- [ ] **Step 2: Run E2E tests**

Run: `cd apps/web && pnpm test:e2e -- --grep "PDF→KB Workflow"`
Expected: PASS (with appropriate API mocking via `page.context().route()`)

- [ ] **Step 3: Commit**

```bash
git add apps/web/e2e/admin/pdf-kb-workflow.spec.ts
git commit -m "test(e2e): add admin PDF→KB workflow smoke tests"
```

---

### Task 11: Full build validation + typecheck

- [ ] **Step 1: Run backend build + tests**

```bash
cd apps/api/src/Api && dotnet build
cd apps/api && dotnet test -v n
```
Expected: BUILD SUCCEEDED, all tests PASS

- [ ] **Step 2: Run frontend typecheck + lint + tests**

```bash
cd apps/web && pnpm typecheck && pnpm lint && pnpm test -- --run
```
Expected: All pass

- [ ] **Step 3: Final commit with all changes**

If any residual formatting changes from lint-staged:

```bash
git status
# If changes exist:
git add -A && git commit -m "style: formatting from lint-staged"
```

- [ ] **Step 4: Create PR**

```bash
git push -u origin feature/admin-pdf-kb-workflow
gh pr create --base main-dev --title "feat(admin): PDF→KB workflow improvements" --body "## Summary
- RecentlyProcessedWidget on SharedGames landing (10 latest PDFs)
- Priority Urgent selector during PDF upload
- GameProcessingQueue mini-widget + deep-link in game detail
- PdfProcessingNotifier global SSE toast notifications

## Test plan
- [ ] Backend: GetRecentlyProcessedDocumentsQuery tests
- [ ] Backend: UploadPdfCommand priority tests
- [ ] Backend: GetProcessingQueueQuery GameId filter tests
- [ ] Backend: SSE stream handler enrichment tests
- [ ] Frontend: RecentlyProcessedWidget tests
- [ ] Frontend: PdfUploadSection priority tests
- [ ] Frontend: GameProcessingQueue tests
- [ ] Frontend: PdfProcessingNotifier tests
- [ ] E2E: Admin PDF→KB workflow smoke tests
- [ ] Manual: Upload PDF → verify widget → verify queue → verify toast

Closes #TBD"
```
