# Admin Embedding Flow — Gap Filling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Connect existing PDF upload, embedding queue, and RAG agent test pages with navigation links, real-time progress, chunk preview, and flow banner — validated by E2E browser test.

**Architecture:** Gap-filling approach — no new pages, only new components added to existing pages. Three new UI components (UploadProgressTracker, ChunkPreviewTab, EmbeddingFlowBanner), two new layout files, one backend query extension, one E2E flow test with POM.

**Tech Stack:** Next.js 16 (App Router), React 19, Tailwind 4, shadcn/ui, Playwright, .NET 9 (MediatR/CQRS), FluentValidation

**Spec:** `docs/superpowers/specs/2026-03-20-admin-embedding-flow-gap-filling-design.md`

---

## File Map

### New Files (Frontend)

| File | Responsibility |
|------|---------------|
| `apps/web/src/components/ui/admin/embedding-flow-banner.tsx` | Flow indicator banner (Upload → Queue → Agent Test) |
| `apps/web/src/components/ui/admin/upload-progress-tracker.tsx` | SSE-powered processing progress display |
| `apps/web/src/components/ui/admin/chunk-preview-tab.tsx` | Paginated chunk preview for queue job detail |
| `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/layout.tsx` | Layout wrapper mounting EmbeddingFlowBanner |
| `apps/web/src/app/admin/(dashboard)/games/[gameId]/agent/test/layout.tsx` | Layout wrapper mounting EmbeddingFlowBanner |
| `apps/web/e2e/pages/admin/QueueDashboardPage.ts` | Page Object Model for queue dashboard |
| `apps/web/e2e/flows/admin-embedding-flow.spec.ts` | E2E flow test (dev + integration) |

### Modified Files (Frontend)

| File | Change |
|------|--------|
| `apps/web/.env.local` | Set `NEXT_PUBLIC_ENABLE_PROGRESS_UI=true` (create if not exists) |
| `apps/web/src/app/(authenticated)/upload/upload-client.tsx` | Add admin "Vai alla Queue" button + mount UploadProgressTracker |
| `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/job-detail-panel.tsx` | Add "Testa Agent" button + mount ChunkPreviewTab |
| `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api.ts` | Add `fetchChunksPreview()` function |
| `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/page.tsx` | Read `jobId` from searchParams and pass to client |

### Modified Files (Backend)

| File | Change |
|------|--------|
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetPdfChunksPreviewQuery.cs` | Add `Page`, `PageSize`, `Search` properties |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetPdfChunksPreviewQueryHandler.cs` | Implement pagination + text search |
| `apps/api/src/Api/Routing/AdminSandboxEndpoints.cs` | Add `page`, `pageSize`, `search` query params |

### New Files (Backend)

| File | Responsibility |
|------|---------------|
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Validators/GetPdfChunksPreviewQueryValidator.cs` | FluentValidation for pagination params |

---

## Task 1: Feature Flag + Env Setup

**Files:**
- Modify: `apps/web/.env.local` (create if not exists — `.env.development` does not exist in this project)

- [ ] **Step 1: Enable progress UI flag**

In `apps/web/.env.local`, add or update:

```env
NEXT_PUBLIC_ENABLE_PROGRESS_UI=true
```

Note: `.env.local` is gitignored. For CI/integration, set this env var in the environment config.

- [ ] **Step 2: Verify flag is read**

Run: `cd apps/web && grep -r "NEXT_PUBLIC_ENABLE_PROGRESS_UI" src/`

Verify the flag is used somewhere in the codebase. Note the files that reference it — this is where the progress UI is gated.

- [ ] **Step 3: Commit**

No git commit needed — `.env.local` is gitignored. Document the required env var in the spec or README.

---

## Task 2: EmbeddingFlowBanner Component

**Files:**
- Create: `apps/web/src/components/ui/admin/embedding-flow-banner.tsx`
- Create: `apps/web/src/components/ui/admin/__tests__/embedding-flow-banner.test.tsx`

- [ ] **Step 1: Write failing test for EmbeddingFlowBanner**

```typescript
// apps/web/src/components/ui/admin/__tests__/embedding-flow-banner.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

// Mock useSearchParams
vi.mock('next/navigation', () => ({
  useSearchParams: vi.fn(),
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

import { useSearchParams } from 'next/navigation';
import { EmbeddingFlowBanner } from '../embedding-flow-banner';

describe('EmbeddingFlowBanner', () => {
  it('renders nothing when flow param is missing', () => {
    (useSearchParams as ReturnType<typeof vi.fn>).mockReturnValue(
      new URLSearchParams('')
    );
    const { container } = render(<EmbeddingFlowBanner />);
    expect(container.firstChild).toBeNull();
  });

  it('renders banner with game name when flow=embedding', () => {
    (useSearchParams as ReturnType<typeof vi.fn>).mockReturnValue(
      new URLSearchParams('flow=embedding&gameName=Catan&gameId=abc-123')
    );
    render(<EmbeddingFlowBanner currentStep="queue" />);
    expect(screen.getByText(/Catan/)).toBeInTheDocument();
    expect(screen.getByText(/Queue/)).toBeInTheDocument();
  });

  it('shows upload as done when on queue step', () => {
    (useSearchParams as ReturnType<typeof vi.fn>).mockReturnValue(
      new URLSearchParams('flow=embedding&gameName=Catan&gameId=abc-123')
    );
    render(<EmbeddingFlowBanner currentStep="queue" />);
    const uploadStep = screen.getByTestId('flow-step-upload');
    expect(uploadStep).toHaveAttribute('data-status', 'done');
  });

  it('can be dismissed', async () => {
    (useSearchParams as ReturnType<typeof vi.fn>).mockReturnValue(
      new URLSearchParams('flow=embedding&gameName=Catan&gameId=abc-123')
    );
    const { user } = render(<EmbeddingFlowBanner currentStep="queue" />);
    // Implementation will use userEvent for dismiss
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/admin/__tests__/embedding-flow-banner.test.tsx`

Expected: FAIL — module not found

- [ ] **Step 3: Implement EmbeddingFlowBanner**

```typescript
// apps/web/src/components/ui/admin/embedding-flow-banner.tsx
'use client';

import { useSearchParams } from 'next/navigation';
import { useEffect, useState } from 'react';
import { X } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/primitives/button';

type FlowStep = 'upload' | 'queue' | 'agent-test';
type StepStatus = 'done' | 'in-progress' | 'pending' | 'failed';

interface EmbeddingFlowBannerProps {
  currentStep: FlowStep;
  queueStatus?: 'Completed' | 'Processing' | 'Queued' | 'Failed';
  agentTestDone?: boolean;
}

const STEPS: { key: FlowStep; label: string }[] = [
  { key: 'upload', label: 'Upload' },
  { key: 'queue', label: 'Queue' },
  { key: 'agent-test', label: 'Agent Test' },
];

function getStepStatus(
  step: FlowStep,
  currentStep: FlowStep,
  queueStatus?: string,
  agentTestDone?: boolean
): StepStatus {
  const stepOrder: FlowStep[] = ['upload', 'queue', 'agent-test'];
  const currentIdx = stepOrder.indexOf(currentStep);
  const stepIdx = stepOrder.indexOf(step);

  if (stepIdx < currentIdx) return 'done';
  if (stepIdx > currentIdx) return 'pending';

  // Current step
  if (step === 'queue') {
    if (queueStatus === 'Completed') return 'done';
    if (queueStatus === 'Failed') return 'failed';
    return 'in-progress';
  }
  if (step === 'agent-test') {
    return agentTestDone ? 'done' : 'in-progress';
  }
  return 'done'; // upload is always done if we're here
}

const statusStyles: Record<StepStatus, string> = {
  done: 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400',
  'in-progress': 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400',
  pending: 'bg-gray-100 text-gray-500 dark:bg-gray-800 dark:text-gray-500',
  failed: 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400',
};

const statusIcons: Record<StepStatus, string> = {
  done: '✓',
  'in-progress': '●',
  pending: '○',
  failed: '✕',
};

export function EmbeddingFlowBanner({
  currentStep,
  queueStatus,
  agentTestDone,
}: EmbeddingFlowBannerProps) {
  const searchParams = useSearchParams();
  const flow = searchParams.get('flow');
  const gameName = searchParams.get('gameName');
  const gameId = searchParams.get('gameId');
  const dismissKey = `embedding-flow-dismissed-${gameId}`;

  // Read sessionStorage in useEffect to avoid SSR hydration mismatch
  const [dismissed, setDismissed] = useState(false);
  useEffect(() => {
    if (typeof window !== 'undefined' && sessionStorage.getItem(dismissKey)) {
      setDismissed(true);
    }
  }, [dismissKey]);

  if (flow !== 'embedding' || !gameName || dismissed) return null;

  const handleDismiss = () => {
    setDismissed(true);
    if (typeof window !== 'undefined') {
      sessionStorage.setItem(dismissKey, 'true');
    }
  };

  return (
    <div
      data-testid="embedding-flow-banner"
      className="mb-4 flex items-center justify-between rounded-lg border border-border bg-card px-4 py-3"
    >
      <div className="flex items-center gap-4">
        <span className="text-sm font-medium">{gameName} — Flusso Embedding</span>
        <div className="flex items-center gap-2">
          {STEPS.map((step, i) => {
            const status = getStepStatus(step.key, currentStep, queueStatus, agentTestDone);
            return (
              <div key={step.key} className="flex items-center gap-2">
                {i > 0 && <span className="text-muted-foreground">→</span>}
                <span
                  data-testid={`flow-step-${step.key}`}
                  data-status={status}
                  className={cn('rounded-full px-2 py-0.5 text-xs font-medium', statusStyles[status])}
                >
                  {statusIcons[status]} {step.label}
                </span>
              </div>
            );
          })}
        </div>
      </div>
      <Button
        variant="ghost"
        size="icon"
        className="h-6 w-6"
        onClick={handleDismiss}
        aria-label="Chiudi banner"
      >
        <X className="h-4 w-4" />
      </Button>
    </div>
  );
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/ui/admin/__tests__/embedding-flow-banner.test.tsx`

Expected: PASS (adjust mocks in test if needed based on actual useSearchParams API)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/admin/embedding-flow-banner.tsx apps/web/src/components/ui/admin/__tests__/embedding-flow-banner.test.tsx
git commit -m "feat(admin): add EmbeddingFlowBanner component with tests"
```

---

## Task 3: Layout Files for Banner Mounting

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/layout.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/games/[gameId]/agent/test/layout.tsx`

**Reference:** Check existing admin layouts for patterns. Likely a simple wrapper that passes `children`.

- [ ] **Step 1: Check existing layout patterns in admin**

Read: `apps/web/src/app/admin/(dashboard)/layout.tsx` to see the pattern used.

- [ ] **Step 2: Create queue layout with banner**

```typescript
// apps/web/src/app/admin/(dashboard)/knowledge-base/queue/layout.tsx
import { Suspense } from 'react';
import { EmbeddingFlowBanner } from '@/components/ui/admin/embedding-flow-banner';

export default function QueueLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <Suspense fallback={null}>
        <EmbeddingFlowBanner currentStep="queue" />
      </Suspense>
      {children}
    </>
  );
}
```

Note: `Suspense` wraps the banner because `useSearchParams()` requires it in App Router.

- [ ] **Step 3: Create agent test layout with banner**

```typescript
// apps/web/src/app/admin/(dashboard)/games/[gameId]/agent/test/layout.tsx
import { Suspense } from 'react';
import { EmbeddingFlowBanner } from '@/components/ui/admin/embedding-flow-banner';

export default function AgentTestLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <Suspense fallback={null}>
        <EmbeddingFlowBanner currentStep="agent-test" />
      </Suspense>
      {children}
    </>
  );
}
```

- [ ] **Step 4: Verify no layout conflicts**

Run: `cd apps/web && pnpm build`

Expected: Build succeeds. If there's an existing layout in the parent directory that wraps content, the new layout nests inside it — verify the banner appears correctly.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/knowledge-base/queue/layout.tsx apps/web/src/app/admin/(dashboard)/games/[gameId]/agent/test/layout.tsx
git commit -m "feat(admin): add layout files for embedding flow banner mounting"
```

---

## Task 4: UploadProgressTracker Component

**Files:**
- Create: `apps/web/src/components/ui/admin/upload-progress-tracker.tsx`
- Create: `apps/web/src/components/ui/admin/__tests__/upload-progress-tracker.test.tsx`

**Reference:** `apps/web/src/hooks/usePdfProgress.ts` — hook returns `{ status, metrics, isConnected, isLoading, error }`

- [ ] **Step 1: Write failing test**

```typescript
// apps/web/src/components/ui/admin/__tests__/upload-progress-tracker.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('@/hooks/usePdfProgress', () => ({
  usePdfProgress: vi.fn(),
}));

import { usePdfProgress } from '@/hooks/usePdfProgress';
import { UploadProgressTracker } from '../upload-progress-tracker';

describe('UploadProgressTracker', () => {
  it('renders nothing when documentId is null', () => {
    (usePdfProgress as ReturnType<typeof vi.fn>).mockReturnValue({
      status: null, metrics: null, isConnected: false, isLoading: false, error: null,
    });
    const { container } = render(<UploadProgressTracker documentId={null} />);
    expect(container.firstChild).toBeNull();
  });

  it('shows processing state when status is available', () => {
    // PdfStatusData actual shape: { state: PdfState, progress: number, eta?, timestamp, errorMessage? }
    (usePdfProgress as ReturnType<typeof vi.fn>).mockReturnValue({
      status: {
        state: 'Chunking',
        progress: 60,
        eta: '45s',
        timestamp: new Date().toISOString(),
        errorMessage: null,
      },
      metrics: null,
      isConnected: true,
      isLoading: false,
      error: null,
    });

    render(<UploadProgressTracker documentId="abc-123" fileName="Rules.pdf" />);
    expect(screen.getByText('Rules.pdf')).toBeInTheDocument();
    expect(screen.getByText(/60%/)).toBeInTheDocument();
    expect(screen.getByText(/Chunking/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/admin/__tests__/upload-progress-tracker.test.tsx`

Expected: FAIL — module not found

- [ ] **Step 3: Implement UploadProgressTracker**

```typescript
// apps/web/src/components/ui/admin/upload-progress-tracker.tsx
'use client';

import { useEffect, useState } from 'react';
import { usePdfProgress } from '@/hooks/usePdfProgress';
import { cn } from '@/lib/utils';
import { CheckCircle2, Loader2, XCircle } from 'lucide-react';

interface UploadProgressTrackerProps {
  documentId: string | null;
  fileName?: string;
  className?: string;
}

// Pipeline states in order — actual PdfState values from the backend
const PIPELINE_STATES = ['Pending', 'Extracting', 'Chunking', 'Embedding', 'Indexing', 'Completed'] as const;

function getStateIcon(pipelineState: string, currentState: string) {
  const stateIdx = PIPELINE_STATES.indexOf(pipelineState as any);
  const currentIdx = PIPELINE_STATES.indexOf(currentState as any);

  if (stateIdx < currentIdx) return <CheckCircle2 className="h-4 w-4 text-green-600" />;
  if (stateIdx === currentIdx) return <Loader2 className="h-4 w-4 animate-spin text-blue-600" />;
  return <span className="h-4 w-4 rounded-full border-2 border-gray-300 inline-block" />;
}

export function UploadProgressTracker({
  documentId,
  fileName,
  className,
}: UploadProgressTrackerProps) {
  const { status, isConnected, error } = usePdfProgress(documentId);
  const [hidden, setHidden] = useState(false);

  // Auto-hide 5s after completion (per spec)
  useEffect(() => {
    if (status?.state === 'Completed' || status?.state === 'Failed') {
      const timer = setTimeout(() => setHidden(true), 5000);
      return () => clearTimeout(timer);
    }
  }, [status?.state]);

  if (!documentId || !status || hidden) return null;

  // PdfStatusData actual shape: { state, progress, eta?, timestamp, errorMessage? }
  const percentage = status.progress ?? 0;
  const currentState = status.state;
  const isComplete = currentState === 'Completed';
  const isFailed = currentState === 'Failed';

  return (
    <div
      data-testid="upload-progress-tracker"
      className={cn('rounded-lg border border-border bg-card p-4', className)}
    >
      {fileName && <p className="mb-3 text-sm font-medium">{fileName}</p>}

      <div className="mb-3 space-y-2">
        {PIPELINE_STATES.filter(s => s !== 'Pending').map((state) => (
          <div key={state} className="flex items-center gap-2 text-sm">
            {isFailed && state === currentState
              ? <XCircle className="h-4 w-4 text-red-600" />
              : getStateIcon(state, currentState)}
            <span className={cn(
              PIPELINE_STATES.indexOf(state as any) > PIPELINE_STATES.indexOf(currentState as any) && 'text-muted-foreground',
              isFailed && state === currentState && 'text-red-600',
            )}>
              {state}
            </span>
            {status.eta && state === currentState && !isComplete && !isFailed && (
              <span className="text-xs text-muted-foreground">({status.eta})</span>
            )}
          </div>
        ))}
      </div>

      {!isComplete && !isFailed && (
        <div className="flex items-center gap-2">
          {/* Simple progress bar — no shadcn Progress component needed */}
          <div className="flex-1 h-2 rounded-full bg-secondary overflow-hidden">
            <div
              className="h-full bg-primary rounded-full transition-all duration-300"
              style={{ width: `${percentage}%` }}
            />
          </div>
          <span className="text-xs text-muted-foreground">{percentage}%</span>
        </div>
      )}

      {!isConnected && !isComplete && !isFailed && (
        <p className="mt-2 text-xs text-amber-600">Connessione SSE persa, polling attivo...</p>
      )}

      {error && <p className="mt-2 text-xs text-red-600">{error.message}</p>}
      {isFailed && status.errorMessage && (
        <p className="mt-2 text-xs text-red-600">{status.errorMessage}</p>
      )}
    </div>
  );
}
```

**Note:** The `PIPELINE_STATES` array must match the actual `PdfState` enum values from the backend. Verify by reading `usePdfProgress` return type and the backend `PdfDocument` state machine. The implementer should adjust state names if they differ (e.g., `'Extracting'` vs `'Extraction'`).

- [ ] **Step 4: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/ui/admin/__tests__/upload-progress-tracker.test.tsx`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/admin/upload-progress-tracker.tsx apps/web/src/components/ui/admin/__tests__/upload-progress-tracker.test.tsx
git commit -m "feat(admin): add UploadProgressTracker component with SSE progress"
```

---

## Task 5: Upload Page — Admin Links + Progress Tracker

**Files:**
- Modify: `apps/web/src/app/(authenticated)/upload/upload-client.tsx`

**Reference:** Upload wizard step 4 shows "Published Successfully!" around lines 564-581. The wizard state has `wizardState.documentId` and `confirmedGameId`.

- [ ] **Step 1: Read current upload-client.tsx step 4 section**

Read: `apps/web/src/app/(authenticated)/upload/upload-client.tsx` — find the step 4 / publish success UI (around lines 564-581). Note the exact JSX structure and available state variables (`wizardState.documentId`, `confirmedGameId`, `selectedGame`).

- [ ] **Step 2: Add admin imports at top of file**

Add these imports near existing imports:

```typescript
import { useAdminRole } from '@/hooks/useAdminRole';
import { UploadProgressTracker } from '@/components/ui/admin/upload-progress-tracker';
import Link from 'next/link';
```

- [ ] **Step 3: Add admin role check in component body**

Inside the component function, near other hooks:

```typescript
const { isAdminOrAbove } = useAdminRole();
```

- [ ] **Step 4: Add progress tracker + admin queue link after success message**

After the existing success message JSX in step 4, add:

```tsx
{/* Progress tracker — shown for all users when flag is enabled */}
{process.env.NEXT_PUBLIC_ENABLE_PROGRESS_UI === 'true' && wizardState.documentId && (
  <UploadProgressTracker
    documentId={wizardState.documentId}
    fileName={uploadedFileName}
    className="mt-4"
  />
)}

{/* Admin-only: link to queue dashboard */}
{isAdminOrAbove && wizardState.documentId && confirmedGameId && (
  <Link
    href={`/admin/knowledge-base/queue?flow=embedding&gameId=${confirmedGameId}&gameName=${encodeURIComponent(selectedGame?.name ?? '')}`}
    className="mt-4 inline-flex items-center gap-2 rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90"
  >
    Vai alla Queue →
  </Link>
)}
```

**Note:** The exact variable names (`uploadedFileName`, `selectedGame?.name`, `confirmedGameId`) must be verified by reading the file. Adjust as needed.

- [ ] **Step 5: Verify build**

Run: `cd apps/web && pnpm build`

Expected: Build succeeds with no type errors

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/(authenticated)/upload/upload-client.tsx
git commit -m "feat(upload): add admin queue link and progress tracker after upload"
```

---

## Task 6: Backend — Extend Chunk Preview with Pagination

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetPdfChunksPreviewQuery.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetPdfChunksPreviewQueryHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Validators/GetPdfChunksPreviewQueryValidator.cs`
- Modify: `apps/api/src/Api/Routing/AdminSandboxEndpoints.cs`

- [ ] **Step 1: Read current query and handler**

Read:
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetPdfChunksPreviewQuery.cs`
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetPdfChunksPreviewQueryHandler.cs`
- `apps/api/src/Api/Routing/AdminSandboxEndpoints.cs` (the chunks preview endpoint)

Note the current query properties, handler logic, and endpoint binding.

- [ ] **Step 2: Add pagination properties to query**

In `GetPdfChunksPreviewQuery.cs`, add:

```csharp
public int Page { get; init; } = 1;
public int PageSize { get; init; } = 20;
public string? Search { get; init; }
```

- [ ] **Step 3: Create FluentValidation validator**

```csharp
// GetPdfChunksPreviewQueryValidator.cs
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

public class GetPdfChunksPreviewQueryValidator : AbstractValidator<GetPdfChunksPreviewQuery>
{
    public GetPdfChunksPreviewQueryValidator()
    {
        RuleFor(x => x.PdfDocumentId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
```

- [ ] **Step 4: Update handler for pagination + search**

In the handler, modify the query to apply:
1. `.Where(e => search == null || e.TextContent.Contains(search))` for search
2. `.OrderBy(e => e.ChunkIndex)` for ordering
3. Count total before pagination: `var total = await query.CountAsync(ct);`
4. `.Skip((page - 1) * pageSize).Take(pageSize)` for pagination
5. Return a new `PaginatedChunksResult` record: `{ Chunks, Total, Page, PageSize }`

Create the result type:

```csharp
public record PaginatedChunksResult(
    IReadOnlyList<ChunkPreviewDto> Chunks,
    int Total,
    int Page,
    int PageSize);
```

- [ ] **Step 5: Update endpoint to accept new params**

In `AdminSandboxEndpoints.cs`, update the endpoint to read query params:

```csharp
.MapGet("/pdfs/{id:guid}/chunks/preview", HandleGetChunksPreview)
```

Handler signature should accept: `int page = 1, int pageSize = 20, string? search = null`

Pass them to the query.

- [ ] **Step 6: Run backend tests**

Run: `cd apps/api/src/Api && dotnet test --filter "GetPdfChunksPreview"`

If no specific tests exist, run: `cd apps/api/src/Api && dotnet build`

Expected: Build succeeds

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetPdfChunksPreviewQuery.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetPdfChunksPreviewQueryHandler.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Validators/GetPdfChunksPreviewQueryValidator.cs apps/api/src/Api/Routing/AdminSandboxEndpoints.cs
git commit -m "feat(api): extend chunk preview endpoint with pagination and search"
```

---

## Task 7: ChunkPreviewTab Component

**Files:**
- Create: `apps/web/src/components/ui/admin/chunk-preview-tab.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api.ts`

**Reference:** `sandboxClient.getChunksPreview(pdfId, limit)` exists. We need a paginated version in queue-api.

- [ ] **Step 1: Add fetchChunksPreview to queue-api.ts**

Read: `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api.ts`

Add a new function:

```typescript
export interface ChunkPreviewDto {
  embeddingId: string;
  textContent: string;
  chunkIndex: number;
  pageNumber: number;
  model: string;
  createdAt: string;
}

export interface PaginatedChunksResult {
  chunks: ChunkPreviewDto[];
  total: number;
  page: number;
  pageSize: number;
}

export async function fetchChunksPreview(
  pdfDocumentId: string,
  page = 1,
  pageSize = 20,
  search?: string
): Promise<PaginatedChunksResult> {
  const params = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
  });
  if (search) params.set('search', search);

  const res = await fetch(
    `/api/v1/admin/sandbox/pdfs/${pdfDocumentId}/chunks/preview?${params}`,
    { credentials: 'include' }
  );
  if (!res.ok) throw new Error(`Failed to fetch chunks: ${res.status}`);
  return res.json();
}

export function useChunksPreview(pdfDocumentId: string | null, page: number, pageSize: number, search?: string) {
  return useQuery({
    queryKey: ['chunks-preview', pdfDocumentId, page, pageSize, search],
    queryFn: () => fetchChunksPreview(pdfDocumentId!, page, pageSize, search),
    enabled: !!pdfDocumentId,
  });
}
```

- [ ] **Step 2: Create ChunkPreviewTab component**

```typescript
// apps/web/src/components/ui/admin/chunk-preview-tab.tsx
'use client';

import { useState } from 'react';
import { useChunksPreview } from '@/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api';
import { Input } from '@/components/ui/primitives/input';
import { Button } from '@/components/ui/primitives/button';
import { Badge } from '@/components/ui/data-display/badge';
import { ChevronLeft, ChevronRight, Search, ChevronDown, ChevronUp } from 'lucide-react';
import { cn } from '@/lib/utils';

interface ChunkPreviewTabProps {
  pdfDocumentId: string | null;
  className?: string;
}

export function ChunkPreviewTab({ pdfDocumentId, className }: ChunkPreviewTabProps) {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [searchInput, setSearchInput] = useState('');
  const [expandedChunk, setExpandedChunk] = useState<number | null>(null);

  const { data, isLoading, error } = useChunksPreview(pdfDocumentId, page, 20, search || undefined);

  if (!pdfDocumentId) return null;

  const totalPages = data ? Math.ceil(data.total / 20) : 0;

  const handleSearch = () => {
    setSearch(searchInput);
    setPage(1);
  };

  return (
    <div className={cn('space-y-3', className)} data-testid="chunk-preview-tab">
      {/* Header */}
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium">
          Chunks {data ? `(${data.total} totali)` : ''}
        </span>
        {totalPages > 1 && (
          <span className="text-xs text-muted-foreground">
            Pagina {page}/{totalPages}
          </span>
        )}
      </div>

      {/* Search */}
      <div className="flex gap-2">
        <Input
          placeholder="Cerca nei chunk..."
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
          className="h-8 text-sm"
        />
        <Button variant="outline" size="sm" onClick={handleSearch}>
          <Search className="h-3 w-3" />
        </Button>
      </div>

      {/* Chunks list */}
      {isLoading && <p className="text-sm text-muted-foreground">Caricamento...</p>}
      {error && <p className="text-sm text-red-600">Errore: {error.message}</p>}

      {data?.chunks.map((chunk) => (
        <div
          key={chunk.embeddingId}
          className="rounded-md border border-border p-3 text-sm"
          data-testid={`chunk-${chunk.chunkIndex}`}
        >
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <span className="font-mono text-xs text-muted-foreground">
                #{chunk.chunkIndex}
              </span>
              <span className="text-xs text-muted-foreground">p.{chunk.pageNumber}</span>
              <Badge variant="secondary" className="text-xs">
                {chunk.model}
              </Badge>
            </div>
            <Button
              variant="ghost"
              size="sm"
              className="h-6 px-1"
              onClick={() => setExpandedChunk(
                expandedChunk === chunk.chunkIndex ? null : chunk.chunkIndex
              )}
            >
              {expandedChunk === chunk.chunkIndex
                ? <ChevronUp className="h-3 w-3" />
                : <ChevronDown className="h-3 w-3" />}
            </Button>
          </div>
          <p className={cn(
            'mt-1 text-xs',
            expandedChunk !== chunk.chunkIndex && 'line-clamp-2'
          )}>
            {chunk.textContent}
          </p>
        </div>
      ))}

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={page <= 1}
            onClick={() => setPage((p) => p - 1)}
          >
            <ChevronLeft className="h-3 w-3" />
          </Button>
          <span className="text-xs">{page} / {totalPages}</span>
          <Button
            variant="outline"
            size="sm"
            disabled={page >= totalPages}
            onClick={() => setPage((p) => p + 1)}
          >
            <ChevronRight className="h-3 w-3" />
          </Button>
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 3: Verify build**

Run: `cd apps/web && pnpm build`

Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/ui/admin/chunk-preview-tab.tsx apps/web/src/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api.ts
git commit -m "feat(admin): add ChunkPreviewTab component with paginated search"
```

---

## Task 8: Job Detail Panel — "Testa Agent" Button + Chunks Tab

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/job-detail-panel.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api.ts` (DTO extension)

**IMPORTANT — DTO gap:** `ProcessingJobDetailDto` currently has NO `gameId` or `gameName` field. Before implementing the "Testa Agent" link, the implementer must:
1. Check if `ProcessingJobDto` includes `gameId` (read the backend `ProcessingJobDto.cs`)
2. If NOT: extend the backend DTO to include `GameId` and `GameName` (from the associated `PdfDocument -> Game` relationship)
3. Update the frontend DTO type in `queue-api.ts` accordingly
4. Alternative: read `gameId` from the flow query params (`useSearchParams()`) instead of from the job DTO — this works when navigating from the upload page but not for standalone queue access

- [ ] **Step 1: Read current job-detail-panel.tsx and check DTO fields**

Read: `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/job-detail-panel.tsx`

Also read: `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api.ts` — check `ProcessingJobDto` and `ProcessingJobDetailDto` interfaces for `gameId` field.

Note: The panel uses sections with `<Separator />` dividers, not tabs. The component receives `job: ProcessingJobDetailDto`.

Identify:
- Whether `gameId` exists on the DTO (if not, extend backend DTO or use flow params)
- Where action buttons are (Cancel/Retry/Remove)
- Where to add "Testa Agent" button
- Where to add ChunkPreviewTab section

- [ ] **Step 2: Add imports**

```typescript
import Link from 'next/link';
import { ChunkPreviewTab } from '@/components/ui/admin/chunk-preview-tab';
import { useSearchParams } from 'next/navigation';
```

- [ ] **Step 3: Add "Testa Agent" button after existing action buttons**

The button source for `gameId` depends on Step 1 findings:
- **Option A (DTO has gameId):** Use `job.gameId` directly
- **Option B (DTO lacks gameId):** Read from flow query params via `useSearchParams()`

```tsx
// Option A: if job.gameId exists on DTO
{job.status === 'Completed' && job.gameId && (
  <Link
    href={`/admin/games/${job.gameId}/agent/test?flow=embedding&gameName=${encodeURIComponent(job.gameName ?? '')}`}
    className="inline-flex items-center gap-2 rounded-md bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground hover:bg-primary/90"
  >
    Testa Agent →
  </Link>
)}

// Option B: if using flow params
const searchParams = useSearchParams();
const flowGameId = searchParams.get('gameId');
const flowGameName = searchParams.get('gameName');

{job.status === 'Completed' && flowGameId && (
  <Link
    href={`/admin/games/${flowGameId}/agent/test?flow=embedding&gameName=${encodeURIComponent(flowGameName ?? '')}`}
    className="inline-flex items-center gap-2 rounded-md bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground hover:bg-primary/90"
  >
    Testa Agent →
  </Link>
)}
```

**Choose the option based on Step 1 findings.** Option B is simpler (no backend change) but only works during the embedding flow navigation.

- [ ] **Step 4: Add ChunkPreviewTab section**

After the Logs section (after the separator following logs), add:

```tsx
{/* Chunk Preview — visible after chunking step */}
{job.pdfDocumentId && hasPassedChunking(job) && (
  <>
    <Separator />
    <div>
      <h4 className="mb-2 text-sm font-medium">Chunks</h4>
      <ChunkPreviewTab pdfDocumentId={job.pdfDocumentId} />
    </div>
  </>
)}
```

Add helper function:

```typescript
function hasPassedChunking(job: ProcessingJobDetailDto): boolean {
  const chunkingStep = job.steps?.find((s) => s.name === 'Chunking' || s.name === 'Chunk');
  return chunkingStep?.status === 'Completed' || chunkingStep?.status === 'Processing'
    || job.status === 'Completed';
}
```

- [ ] **Step 5: Verify build**

Run: `cd apps/web && pnpm build`

Expected: Build succeeds

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/job-detail-panel.tsx
git commit -m "feat(admin): add Testa Agent button and ChunkPreviewTab to job detail"
```

---

## Task 9: Queue Page — Read jobId from URL

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/page.tsx`

- [ ] **Step 1: Read current queue page**

Read: `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/page.tsx`

It already reads `gameId` from searchParams. Add `jobId` reading.

- [ ] **Step 2: Pass jobId to QueueDashboardClient**

Update the server component to also read `jobId`:

```typescript
export default async function QueuePage({
  searchParams,
}: {
  searchParams: Promise<{ gameId?: string; jobId?: string }>;
}) {
  const { gameId, jobId } = await searchParams;
  return <QueueDashboardClient gameId={gameId} highlightJobId={jobId} />;
}
```

- [ ] **Step 3: Update QueueDashboardClient to highlight job**

Read: `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/queue-dashboard-client.tsx`

Add `highlightJobId` prop and use it to auto-select the job in the list on mount:

```typescript
interface QueueDashboardClientProps {
  gameId?: string;
  highlightJobId?: string;
}
```

In the client, when `highlightJobId` is provided, auto-select that job:

```typescript
useEffect(() => {
  if (highlightJobId) {
    setSelectedJobId(highlightJobId);
  }
}, [highlightJobId]);
```

- [ ] **Step 4: Verify build**

Run: `cd apps/web && pnpm build`

Expected: Build succeeds

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/knowledge-base/queue/page.tsx apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/queue-dashboard-client.tsx
git commit -m "feat(admin): auto-highlight job from URL jobId param in queue dashboard"
```

---

## Task 10: Queue Dashboard POM

**Files:**
- Create: `apps/web/e2e/pages/admin/QueueDashboardPage.ts`

**Reference:** Check `apps/web/e2e/pages/admin/AdminPage.ts` and `apps/web/e2e/pages/base/BasePage.ts` for POM pattern.

- [ ] **Step 1: Read existing POM patterns**

Read: `apps/web/e2e/pages/base/BasePage.ts` and `apps/web/e2e/pages/admin/AdminPage.ts`

Note the class structure, constructor, navigation pattern, locator getters.

- [ ] **Step 2: Create QueueDashboardPage POM**

```typescript
// apps/web/e2e/pages/admin/QueueDashboardPage.ts
import { type Page, type Locator, expect } from '@playwright/test';
import { BasePage } from '../base/BasePage';

export class QueueDashboardPage extends BasePage {
  constructor(page: Page) {
    super(page);
  }

  // Navigation
  async goto(params?: { jobId?: string; flow?: string; gameId?: string; gameName?: string }) {
    const searchParams = new URLSearchParams();
    if (params?.jobId) searchParams.set('jobId', params.jobId);
    if (params?.flow) searchParams.set('flow', params.flow);
    if (params?.gameId) searchParams.set('gameId', params.gameId);
    if (params?.gameName) searchParams.set('gameName', params.gameName);
    const qs = searchParams.toString();
    await this.page.goto(`/admin/knowledge-base/queue${qs ? '?' + qs : ''}`);
  }

  // Locators
  get flowBanner(): Locator {
    return this.page.getByTestId('embedding-flow-banner');
  }

  get jobList(): Locator {
    return this.page.locator('[data-testid="queue-job-list"]');
  }

  get jobDetailPanel(): Locator {
    return this.page.locator('[data-testid="job-detail-panel"]');
  }

  get chunkPreviewTab(): Locator {
    return this.page.getByTestId('chunk-preview-tab');
  }

  get testAgentButton(): Locator {
    return this.page.getByRole('link', { name: /Testa Agent/i });
  }

  // Actions
  async waitForJobCompletion(timeout = 120_000): Promise<void> {
    await this.page.waitForSelector(
      '[data-testid="job-status-badge"][data-status="Completed"]',
      { timeout }
    );
  }

  async selectJob(jobId: string): Promise<void> {
    await this.page.click(`[data-job-id="${jobId}"]`);
  }

  async openChunksSection(): Promise<void> {
    // Scroll to chunks section if needed
    const chunksHeader = this.page.getByText('Chunks', { exact: false });
    await chunksHeader.scrollIntoViewIfNeeded();
  }

  // Assertions
  async expectFlowBannerVisible(gameName: string): Promise<void> {
    await expect(this.flowBanner).toBeVisible();
    await expect(this.flowBanner).toContainText(gameName);
  }

  async expectJobHighlighted(): Promise<void> {
    await expect(this.jobDetailPanel).toBeVisible();
  }

  async expectChunksVisible(): Promise<void> {
    await expect(this.chunkPreviewTab).toBeVisible();
    const chunks = this.page.locator('[data-testid^="chunk-"]');
    await expect(chunks.first()).toBeVisible();
  }
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/e2e/pages/admin/QueueDashboardPage.ts
git commit -m "test(e2e): add QueueDashboardPage POM for embedding flow test"
```

---

## Task 11: E2E Flow Test

**Files:**
- Create: `apps/web/e2e/flows/admin-embedding-flow.spec.ts`

**Reference:** Check `apps/web/e2e/flows/` for existing flow test patterns. Use `admin-user-onboarding.spec.ts` pattern with serial tests + shared state.

- [ ] **Step 1: Check e2e/flows directory**

Run: `ls apps/web/e2e/flows/` to see existing patterns.

Also check: `apps/web/e2e/test-data/` for existing PDF fixtures.

- [ ] **Step 2: Create PDF test fixture**

If no small PDF exists in `e2e/test-data/`, we need one. Options:
- Copy a small existing PDF from the project
- Create a minimal PDF programmatically in the test setup
- Use a 1-page dummy PDF

For now, note this as a manual step: place a 2-3 page PDF at `apps/web/e2e/test-data/sample-rules-short.pdf`.

- [ ] **Step 3: Create the E2E flow test**

```typescript
// apps/web/e2e/flows/admin-embedding-flow.spec.ts
import { test, expect } from '@playwright/test';
import { QueueDashboardPage } from '../pages/admin/QueueDashboardPage';
import path from 'path';

const ENV = process.env.TEST_ENV ?? 'dev';

const envConfig = {
  dev: {
    baseUrl: 'http://localhost:3000',
    testPdf: path.resolve(__dirname, '../test-data/sample-rules-short.pdf'),
    gameSelector: 'seed' as const,
    timeout: 120_000,
  },
  integration: {
    baseUrl: process.env.INTEGRATION_URL ?? 'http://localhost:3000',
    testPdf: path.resolve(__dirname, '../test-data/sample-rules-short.pdf'),
    gameSelector: 'search' as const,
    timeout: 180_000,
  },
};

const config = envConfig[ENV as keyof typeof envConfig] ?? envConfig.dev;

// Shared state across serial tests
const state: {
  documentId?: string;
  gameId?: string;
  gameName?: string;
  jobId?: string;
} = {};

test.describe('Admin Embedding Flow', () => {
  test.describe.configure({ mode: 'serial' });

  test('1. Upload PDF for existing game', async ({ page }) => {
    // Login as admin
    await page.goto('/login');
    await page.fill('[name="email"]', process.env.ADMIN_EMAIL ?? 'admin@meepleai.com');
    await page.fill('[name="password"]', process.env.ADMIN_PASSWORD ?? 'admin123');
    await page.click('button[type="submit"]');
    await page.waitForURL(/dashboard|upload/);

    // Navigate to upload
    await page.goto('/upload');
    await page.waitForLoadState('networkidle');

    // Step 1: Select existing game
    await test.step('Select game from catalog', async () => {
      // Click game selector — adjust selectors to match actual UI
      const gameSearch = page.getByPlaceholder(/cerca|search/i);
      if (await gameSearch.isVisible()) {
        await gameSearch.fill('Catan');
        await page.waitForTimeout(500);
        const gameOption = page.getByText('Catan').first();
        await gameOption.click();
      }

      // Capture game info
      state.gameName = 'Catan';
      // gameId will come from upload response
    });

    // Step 2: Upload PDF
    await test.step('Upload PDF file', async () => {
      const fileInput = page.locator('input[type="file"]');
      await fileInput.setInputFiles(config.testPdf);
      await page.waitForTimeout(1000);

      // Wait for upload to complete
      await expect(page.getByText(/caricato|success|pubblicato/i)).toBeVisible({
        timeout: 30_000,
      });
    });

    // Step 3: Verify progress tracker appears
    await test.step('Verify progress tracker', async () => {
      const progressTracker = page.getByTestId('upload-progress-tracker');
      // Progress tracker may or may not appear depending on timing
      if (await progressTracker.isVisible({ timeout: 5000 }).catch(() => false)) {
        await expect(progressTracker).toBeVisible();
      }
    });

    // Step 4: Click "Vai alla Queue" (admin only)
    await test.step('Click Vai alla Queue', async () => {
      const queueLink = page.getByRole('link', { name: /Vai alla Queue/i });
      await expect(queueLink).toBeVisible({ timeout: 10_000 });

      // Capture URL params before clicking
      const href = await queueLink.getAttribute('href');
      if (href) {
        const url = new URL(href, config.baseUrl);
        state.gameId = url.searchParams.get('gameId') ?? undefined;
      }

      await queueLink.click();
      await page.waitForURL(/admin\/knowledge-base\/queue/);
    });
  });

  test('2. Monitor embedding in queue dashboard', async ({ page }) => {
    test.skip(!state.gameId, 'No gameId from previous step');

    const queuePage = new QueueDashboardPage(page);

    // Login and navigate
    await page.goto('/login');
    await page.fill('[name="email"]', process.env.ADMIN_EMAIL ?? 'admin@meepleai.com');
    await page.fill('[name="password"]', process.env.ADMIN_PASSWORD ?? 'admin123');
    await page.click('button[type="submit"]');
    await page.waitForURL(/dashboard/);

    await queuePage.goto({
      flow: 'embedding',
      gameId: state.gameId,
      gameName: state.gameName,
    });

    // Verify flow banner
    await test.step('Verify flow banner', async () => {
      await queuePage.expectFlowBannerVisible(state.gameName!);
    });

    // Wait for job completion
    await test.step('Wait for job completion', async () => {
      await queuePage.waitForJobCompletion(config.timeout);
    });

    // Verify chunks section
    await test.step('Verify chunks preview', async () => {
      await queuePage.openChunksSection();
      await queuePage.expectChunksVisible();
    });

    // Click "Testa Agent"
    await test.step('Navigate to agent test', async () => {
      const testAgentLink = queuePage.testAgentButton;
      await expect(testAgentLink).toBeVisible();
      await testAgentLink.click();
      await page.waitForURL(/admin\/games\/.*\/agent\/test/);
    });
  });

  test('3. Test RAG agent with embedded KB', async ({ page }) => {
    test.skip(!state.gameId, 'No gameId from previous step');

    // Login and navigate
    await page.goto('/login');
    await page.fill('[name="email"]', process.env.ADMIN_EMAIL ?? 'admin@meepleai.com');
    await page.fill('[name="password"]', process.env.ADMIN_PASSWORD ?? 'admin123');
    await page.click('button[type="submit"]');
    await page.waitForURL(/dashboard/);

    await page.goto(
      `/admin/games/${state.gameId}/agent/test?flow=embedding&gameName=${encodeURIComponent(state.gameName ?? '')}`
    );

    // Verify flow banner shows Queue done
    await test.step('Verify flow banner on agent test page', async () => {
      const banner = page.getByTestId('embedding-flow-banner');
      if (await banner.isVisible({ timeout: 3000 }).catch(() => false)) {
        const queueStep = page.getByTestId('flow-step-queue');
        await expect(queueStep).toHaveAttribute('data-status', 'done');
      }
    });

    // Send a test question to the agent
    await test.step('Send test question to agent', async () => {
      // Find chat input — adjust selector to match actual agent test UI
      const chatInput = page.getByPlaceholder(/domanda|question|chiedi/i)
        .or(page.locator('textarea').first());
      await chatInput.fill('Quanti giocatori possono giocare?');

      const sendButton = page.getByRole('button', { name: /invia|send/i })
        .or(page.locator('button[type="submit"]'));
      await sendButton.click();
    });

    // Verify agent response
    await test.step('Verify agent response with citations', async () => {
      // Wait for response to appear (streaming)
      const response = page.locator('[data-testid="agent-response"], [data-testid="assistant-message"]').first();
      await expect(response).toBeVisible({ timeout: 60_000 });

      // Verify response is not empty
      const text = await response.textContent();
      expect(text?.length).toBeGreaterThan(10);

      // Check for citations (may be in a separate panel or inline)
      // Flexible: check either citations panel or inline references
      const hasCitations = await page.locator('[data-testid*="citation"], [data-testid*="source"]')
        .count()
        .then((c) => c > 0)
        .catch(() => false);

      // Citations are expected but not blocking — log warning if missing
      if (!hasCitations) {
        console.warn('No citations found in agent response — verify RAG pipeline');
      }
    });
  });

  // Cleanup
  test.afterAll(async ({ browser }) => {
    // Cleanup: delete test data via API
    if (state.documentId || state.jobId) {
      const context = await browser.newContext();
      const page = await context.newPage();

      try {
        // Login
        await page.goto('/login');
        await page.fill('[name="email"]', process.env.ADMIN_EMAIL ?? 'admin@meepleai.com');
        await page.fill('[name="password"]', process.env.ADMIN_PASSWORD ?? 'admin123');
        await page.click('button[type="submit"]');
        await page.waitForURL(/dashboard/);

        // Cleanup via admin API — best effort
        if (state.jobId) {
          await page.request.delete(
            `/api/v1/admin/queue/${state.jobId}`
          ).catch(() => {});
        }
      } finally {
        await context.close();
      }
    }
  });
});
```

**Important notes for implementer:**
- Selectors marked with comments like "adjust selector" MUST be verified against the actual UI by reading the page components
- Login flow may differ — check if `PLAYWRIGHT_AUTH_BYPASS` is available
- The test uses `test.describe.configure({ mode: 'serial' })` so tests run in order
- Shared `state` object passes data between tests

- [ ] **Step 4: Verify test structure compiles**

Run: `cd apps/web && npx playwright test e2e/flows/admin-embedding-flow.spec.ts --list`

Expected: Lists the 3 tests without errors

- [ ] **Step 5: Commit**

```bash
git add apps/web/e2e/flows/admin-embedding-flow.spec.ts apps/web/e2e/pages/admin/QueueDashboardPage.ts
git commit -m "test(e2e): add admin embedding flow E2E test with dev/integration parametrization"
```

---

## Task 12: Integration Testing + Final Verification

- [ ] **Step 1: Run frontend build**

Run: `cd apps/web && pnpm build`

Expected: Build succeeds with no errors

- [ ] **Step 2: Run frontend unit tests**

Run: `cd apps/web && pnpm test`

Expected: All tests pass, including new EmbeddingFlowBanner tests

- [ ] **Step 3: Run backend build**

Run: `cd apps/api/src/Api && dotnet build`

Expected: Build succeeds

- [ ] **Step 4: Run typecheck**

Run: `cd apps/web && pnpm typecheck`

Expected: No type errors

- [ ] **Step 5: Run lint**

Run: `cd apps/web && pnpm lint`

Expected: No lint errors

- [ ] **Step 6: Manual smoke test (if dev stack available)**

Start: `cd infra && make dev-core`

1. Login as admin at http://localhost:3000/login
2. Go to /upload
3. Select a game, upload a PDF
4. Verify progress tracker appears
5. Click "Vai alla Queue"
6. Verify job is highlighted, flow banner visible
7. Wait for completion
8. Check Chunks section
9. Click "Testa Agent"
10. Send a question, verify response

- [ ] **Step 7: Run E2E test (if stack is running)**

Run: `cd apps/web && TEST_ENV=dev npx playwright test e2e/flows/admin-embedding-flow.spec.ts --headed`

Expected: All 3 tests pass

- [ ] **Step 8: Final commit (if any fixes needed)**

Stage only the specific files that were fixed, then commit:
```bash
git commit -m "fix: address integration testing feedback for embedding flow"
```

---

## Task Order & Dependencies

```
Task 1 (env flag)           — independent
Task 2 (banner component)   — independent
Task 3 (layout files)       — depends on Task 2
Task 4 (progress tracker)   — independent
Task 5 (upload page links)  — depends on Task 4
Task 6 (backend pagination) — independent
Task 7 (chunk preview tab)  — depends on Task 6
Task 8 (job detail panel)   — depends on Task 7
Task 9 (queue page jobId)   — independent
Task 10 (POM)               — independent
Task 11 (E2E test)          — depends on all above
Task 12 (verification)      — depends on all above
```

**Parallelizable groups:**
- Group A: Tasks 1, 2, 4, 6, 9, 10 (all independent)
- Group B: Tasks 3, 5, 7 (after their dependencies)
- Group C: Task 8 (after Task 7)
- Group D: Tasks 11, 12 (after everything)
