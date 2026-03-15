# Admin Shared Game Content Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build admin tools for managing shared game catalog content: per-game document overview (#119), bulk PDF upload (#117), guided wizard for catalog population (#118), and MAU monitoring (#113).

**Architecture:** Backend adds new CQRS queries/commands in SharedGameCatalog and DocumentProcessing bounded contexts. Frontend adds admin pages with the existing glassmorphism design pattern. Dependency chain: #119 → #117 → #118 (sequential); #113 is independent.

**Tech Stack:** .NET 9 (MediatR, EF Core), Next.js 16, React 19, Tailwind 4, shadcn/ui, Zod, React Query, Vitest

**Branch:** `feature/issue-236-shared-game-content` from `main-dev`

---

## Chunk 1: Per-SharedGame Document Overview (#119)

### Task 1: Create branch and backend query

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetSharedGameDocumentsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Handlers/GetSharedGameDocumentsQueryHandler.cs`

- [ ] **Step 1: Create feature branch**

```bash
cd D:/Repositories/meepleai-monorepo-backend
git checkout main-dev && git pull
git checkout -b feature/issue-236-shared-game-content
git config branch.feature/issue-236-shared-game-content.parent main-dev
```

- [ ] **Step 2: Research existing SharedGameCatalog structure**

Read these files to understand existing patterns:
- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/` — all files
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/` — PDF entities, processing state
- `apps/api/src/Api/Routing/SharedGameCatalogEndpoints.cs`
- `apps/api/src/Api/Routing/AdminPdfManagementEndpoints.cs`

Identify:
- SharedGame entity structure (Id, Title, BggId, etc.)
- PdfDocument entity structure (GameId, ProcessingState, etc.)
- Existing admin endpoints for PDF management
- How documents link to shared games (GameId FK)

- [ ] **Step 3: Create query and handler**

```csharp
// GetSharedGameDocumentsQuery.cs
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Returns all PDF documents associated with a shared game.
/// Issue #119: Per-SharedGame document overview endpoint.
/// </summary>
public record GetSharedGameDocumentsQuery(Guid GameId) : IRequest<SharedGameDocumentsDto>;

public record SharedGameDocumentsDto(
    Guid GameId,
    string GameTitle,
    int TotalDocuments,
    int IndexedDocuments,
    int ProcessingDocuments,
    int FailedDocuments,
    IReadOnlyList<GameDocumentSummaryDto> Documents);

public record GameDocumentSummaryDto(
    Guid DocumentId,
    string FileName,
    string ProcessingState,
    long FileSizeBytes,
    DateTime UploadedAt,
    DateTime? IndexedAt,
    int? ChunkCount);
```

The handler queries PdfDocuments by GameId, joins with SharedGame for the title, and aggregates state counts. Follow existing handler patterns in the SharedGameCatalog BC.

- [ ] **Step 4: Create endpoint**

Add to `AdminPdfManagementEndpoints.cs` or create a new `AdminSharedGameContentEndpoints.cs`:

```csharp
// GET /api/v1/admin/shared-games/{gameId}/documents
group.MapGet("/shared-games/{gameId}/documents", async (
    HttpContext context,
    IMediator mediator,
    Guid gameId,
    CancellationToken ct) =>
{
    var (authorized, _, error) = context.RequireAdminSession();
    if (!authorized) return error!;

    var result = await mediator.Send(new GetSharedGameDocumentsQuery(gameId), ct)
        .ConfigureAwait(false);
    return Results.Ok(result);
})
.RequireAuthorization("RequireAdminOrAbove")
.WithSummary("Get documents for shared game")
.WithDescription("Returns all PDF documents and their processing status for a shared game");
```

- [ ] **Step 5: Add frontend API client method**

In `adminClient.ts`:
```typescript
async getSharedGameDocuments(gameId: string): Promise<SharedGameDocuments> {
  const result = await httpClient.get(
    `/api/v1/admin/shared-games/${gameId}/documents`,
    sharedGameDocumentsSchema
  );
  if (!result) throw new Error('Failed to load documents');
  return result;
},
```

Add corresponding Zod schema:
```typescript
export const gameDocumentSummarySchema = z.object({
  documentId: z.string(),
  fileName: z.string(),
  processingState: z.string(),
  fileSizeBytes: z.number(),
  uploadedAt: z.string(),
  indexedAt: z.string().nullable(),
  chunkCount: z.number().nullable(),
});

export const sharedGameDocumentsSchema = z.object({
  gameId: z.string(),
  gameTitle: z.string(),
  totalDocuments: z.number(),
  indexedDocuments: z.number(),
  processingDocuments: z.number(),
  failedDocuments: z.number(),
  documents: z.array(gameDocumentSummarySchema),
});

export type SharedGameDocuments = z.infer<typeof sharedGameDocumentsSchema>;
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/ apps/api/src/Api/Routing/ apps/web/src/lib/api/
git commit -m "feat(admin): per-SharedGame document overview endpoint (#119)"
```

---

## Chunk 2: Bulk PDF Upload (#117)

### Task 2: Bulk upload command and endpoint

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/BulkUploadPdfsCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Handlers/BulkUploadPdfsCommandHandler.cs`

- [ ] **Step 1: Research existing PDF upload flow**

Read:
- `apps/api/src/Api/Routing/PdfEndpoints.cs` — existing single PDF upload
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/` — existing upload commands
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Handlers/` — existing upload handlers

Understand the upload → extraction → indexing pipeline.

- [ ] **Step 2: Create bulk upload command**

```csharp
// BulkUploadPdfsCommand.cs
using MediatR;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Upload multiple PDFs for a shared game in one request.
/// Issue #117: Bulk PDF upload for shared games.
/// </summary>
public record BulkUploadPdfsCommand(
    Guid GameId,
    IReadOnlyList<BulkPdfFile> Files,
    Guid AdminUserId) : IRequest<BulkUploadResultDto>;

public record BulkPdfFile(string FileName, Stream Content, long SizeBytes);

public record BulkUploadResultDto(
    int TotalFiles,
    int SuccessfulUploads,
    int FailedUploads,
    IReadOnlyList<BulkUploadFileResult> Results);

public record BulkUploadFileResult(
    string FileName,
    bool Success,
    Guid? DocumentId,
    string? Error);
```

The handler iterates files, reuses the existing single-upload logic internally, and collects results. Each file is uploaded independently so one failure doesn't block others.

- [ ] **Step 3: Create endpoint**

```csharp
// POST /api/v1/admin/shared-games/{gameId}/documents/bulk-upload
group.MapPost("/shared-games/{gameId}/documents/bulk-upload", async (
    HttpContext context,
    IMediator mediator,
    Guid gameId,
    CancellationToken ct) =>
{
    var (authorized, session, error) = context.RequireAdminSession();
    if (!authorized) return error!;

    var form = await context.Request.ReadFormAsync(ct).ConfigureAwait(false);
    var files = form.Files
        .Where(f => f.Length > 0 && f.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        .Select(f => new BulkPdfFile(f.FileName, f.OpenReadStream(), f.Length))
        .ToList();

    if (files.Count == 0)
        return Results.BadRequest(new { error = "No valid PDF files provided" });

    var command = new BulkUploadPdfsCommand(gameId, files, session!.User!.Id);
    var result = await mediator.Send(command, ct).ConfigureAwait(false);
    return Results.Ok(result);
})
.RequireAuthorization("RequireAdminOrAbove")
.DisableAntiforgery()
.WithSummary("Bulk upload PDFs for shared game")
.WithDescription("Upload multiple PDF files for a shared game. Each file is processed independently.");
```

- [ ] **Step 4: Add frontend API client method**

```typescript
async bulkUploadPdfs(
  gameId: string,
  files: File[],
  onProgress?: (percent: number) => void
): Promise<BulkUploadResult> {
  const formData = new FormData();
  files.forEach(f => formData.append('files', f));

  const result = await httpClient.postMultipart(
    `/api/v1/admin/shared-games/${gameId}/documents/bulk-upload`,
    formData,
    bulkUploadResultSchema,
    onProgress
  );
  if (!result) throw new Error('Bulk upload failed');
  return result;
},
```

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/ apps/api/src/Api/Routing/ apps/web/src/lib/api/
git commit -m "feat(admin): bulk PDF upload for shared games (#117)"
```

---

## Chunk 3: Guided Wizard (#118) & MAU Monitoring (#113)

### Task 3: Guided Wizard Frontend (#118)

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/games/shared/wizard/page.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/games/shared/wizard/CatalogWizard.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/games/shared/wizard/__tests__/CatalogWizard.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

const mockSearchBgg = vi.hoisted(() => vi.fn());
const mockBulkUploadPdfs = vi.hoisted(() => vi.fn());
const mockGetSharedGameDocuments = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      bulkUploadPdfs: mockBulkUploadPdfs,
      getSharedGameDocuments: mockGetSharedGameDocuments,
    },
    bgg: { search: mockSearchBgg },
  },
}));

import { CatalogWizard } from '../CatalogWizard';

describe('CatalogWizard', () => {
  beforeEach(() => vi.clearAllMocks());

  it('renders wizard steps', () => {
    render(<CatalogWizard />);
    expect(screen.getByTestId('wizard-step-1')).toBeInTheDocument();
    expect(screen.getByText(/Select Game/i)).toBeInTheDocument();
  });

  it('step 1: search and select a game', async () => {
    const user = userEvent.setup();
    mockSearchBgg.mockResolvedValue([
      { id: '1', name: 'Catan', yearPublished: 1995 },
    ]);
    render(<CatalogWizard />);

    await user.type(screen.getByTestId('wizard-search-input'), 'Catan');

    await waitFor(() => {
      expect(screen.getByTestId('wizard-result-1')).toBeInTheDocument();
    });
  });

  it('step 2: upload PDFs', async () => {
    // Test that step 2 shows upload dropzone after game selection
    render(<CatalogWizard />);
    // Implementation details depend on step navigation
  });

  it('step 3: review and confirm', async () => {
    render(<CatalogWizard />);
    // Step 3 shows summary of selected game + uploaded PDFs
  });
});
```

- [ ] **Step 2: Implement CatalogWizard**

Multi-step wizard with 3 steps:
1. **Select Game** — Search BGG or existing catalog, select game
2. **Upload Documents** — Drag-and-drop PDF upload zone with bulk upload
3. **Review & Submit** — Summary of game + documents, confirm

Follow the glassmorphism design pattern (`rounded-xl border bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md`). Use existing `Button`, `Badge` from shadcn/ui.

- [ ] **Step 3: Run tests and commit**

```bash
cd apps/web && pnpm test -- --run src/app/admin/\(dashboard\)/games/shared/wizard/
git add apps/web/src/app/admin/\(dashboard\)/games/shared/wizard/
git commit -m "feat(admin): guided wizard for catalog population (#118)"
```

### Task 4: MAU Monitoring (#113)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Analytics/GetActiveAiUsersQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Handlers/Analytics/GetActiveAiUsersQueryHandler.cs`
- Create: `apps/web/src/app/admin/(dashboard)/analytics/mau/MauDashboard.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/analytics/mau/__tests__/MauDashboard.test.tsx`

- [ ] **Step 1: Research existing analytics**

Read:
- `apps/api/src/Api/Routing/AnalyticsEndpoints.cs`
- `apps/api/src/Api/Routing/AdminBusinessStatsEndpoints.cs`
- Any existing MAU or user activity tracking

- [ ] **Step 2: Create backend query**

```csharp
// GetActiveAiUsersQuery.cs
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.Analytics;

/// <summary>
/// Returns Monthly Active AI Users — users who have used AI features.
/// Issue #113: MAU monitoring — ActiveAiUsers.
/// </summary>
public record GetActiveAiUsersQuery(
    DateTime? StartDate = null,
    DateTime? EndDate = null) : IRequest<ActiveAiUsersDto>;

public record ActiveAiUsersDto(
    int CurrentMonthMau,
    int PreviousMonthMau,
    double ChangePercent,
    IReadOnlyList<DailyActiveUsersDto> DailyBreakdown,
    IReadOnlyList<AiFeatureUsageDto> FeatureBreakdown);

public record DailyActiveUsersDto(DateTime Date, int UniqueUsers);
public record AiFeatureUsageDto(string Feature, int UniqueUsers, int TotalUsages);
```

The handler queries chat sessions, agent interactions, and RAG queries grouped by user and date.

- [ ] **Step 3: Create endpoint, frontend component, and tests**

Follow the same pattern as DbStatsPanel: KPI cards + chart + table.

Frontend shows:
- MAU KPI card with month-over-month change
- Daily active users sparkline/chart
- Feature breakdown table (Chat, RAG, Agent, etc.)

- [ ] **Step 4: Commit**

```bash
git add apps/api/ apps/web/
git commit -m "feat(admin): MAU monitoring dashboard for AI users (#113)"
```

### Task 5: Final validation and PR

- [ ] **Step 1: Run all tests**

```bash
cd apps/web && pnpm typecheck && pnpm lint && pnpm test --run
cd ../api/src/Api && dotnet build
```

- [ ] **Step 2: Create PR**

```bash
git push -u origin feature/issue-236-shared-game-content
gh pr create --base main-dev --title "feat(admin): Shared Game Content management (#119, #117, #118, #113)" --body "$(cat <<'EOF'
## Summary
- Per-SharedGame document overview endpoint (#119)
- Bulk PDF upload for shared games (#117)
- Guided wizard for catalog population (#118)
- MAU monitoring dashboard for AI users (#113)

## Test plan
- [ ] Document overview shows correct counts per game
- [ ] Bulk PDF upload handles multiple files with individual status
- [ ] Wizard navigates through all 3 steps correctly
- [ ] MAU dashboard shows daily breakdown and feature usage
- [ ] All frontend tests pass
- [ ] Backend builds successfully

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3: Update issues**

```bash
for issue in 119 117 118 113; do
  gh issue edit $issue --add-label "status:in-review"
done
```
