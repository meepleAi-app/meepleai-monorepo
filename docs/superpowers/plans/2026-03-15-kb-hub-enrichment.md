# KB Hub Enrichment Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enrich the admin KB hub page with live data widgets (recent PDFs, queue preview) and add priority selection to the upload flow for "add to head of queue" capability.

**Architecture:** Convert the KB hub from a static navigation page to a live dashboard using existing API hooks (`adminClient.getAllPdfs`, `useQueueList`, `useQueueStats`) and existing component patterns (`ProcessingQueue` widget). The upload zone gets a priority dropdown before the auto-enqueue step.

**Tech Stack:** Next.js 16 (App Router), React 19, TanStack React Query, Tailwind 4, shadcn/ui, lucide-react

---

## File Structure

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `apps/web/src/components/admin/knowledge-base/hub/kb-hub-client.tsx` | Client wrapper for hub with live data |
| Create | `apps/web/src/components/admin/knowledge-base/hub/recent-pdfs-widget.tsx` | Recent completed PDFs widget |
| Create | `apps/web/src/components/admin/knowledge-base/hub/queue-preview-widget.tsx` | Queue status preview widget |
| Modify | `apps/web/src/app/admin/(dashboard)/knowledge-base/page.tsx` | Convert to use client hub component |
| Modify | `apps/web/src/components/admin/knowledge-base/upload-zone.tsx` | Add priority selector before enqueue |
| Create | `apps/web/__tests__/components/admin/knowledge-base/hub/recent-pdfs-widget.test.tsx` | Tests for recent PDFs widget |
| Create | `apps/web/__tests__/components/admin/knowledge-base/hub/queue-preview-widget.test.tsx` | Tests for queue preview widget |
| Create | `apps/web/__tests__/components/admin/knowledge-base/upload-zone-priority.test.tsx` | Tests for priority selector |

---

## Chunk 1: Priority Selector in Upload Zone

### Task 1: Add Priority Selector to UploadZone

**Files:**
- Modify: `apps/web/src/components/admin/knowledge-base/upload-zone.tsx`
- Test: `apps/web/__tests__/components/admin/knowledge-base/upload-zone-priority.test.tsx`

**Context:** The `enqueuePdf(documentId, priority)` function already accepts a priority number (0=Low, 10=Normal, 20=High, 30=Urgent). The upload zone currently hardcodes `enqueuePdf(result.documentId)` which defaults to `priority=0`. We add a toggle so the admin can choose "Urgent" (30) to insert at queue head.

- [ ] **Step 1: Write the failing test for priority selector rendering**

```tsx
// apps/web/__tests__/components/admin/knowledge-base/upload-zone-priority.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

// Mock dependencies
vi.mock('@/lib/api/context', () => ({
  useApiClient: () => ({
    pdf: {
      uploadPdf: vi.fn(),
      initChunkedUpload: vi.fn(),
      uploadChunk: vi.fn(),
      completeChunkedUpload: vi.fn(),
    },
  }),
}));

vi.mock('@/lib/hooks/use-game-search', () => ({
  useGameSearch: () => ({ data: [], isLoading: false }),
}));

vi.mock('@/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api', () => ({
  enqueuePdf: vi.fn().mockResolvedValue({ jobId: 'test-job' }),
}));

import { UploadZone } from '@/components/admin/knowledge-base/upload-zone';

describe('UploadZone priority selector', () => {
  it('renders priority toggle with Normal as default', () => {
    render(<UploadZone />);
    expect(screen.getByTestId('priority-toggle')).toBeInTheDocument();
    expect(screen.getByText('Normal')).toBeInTheDocument();
  });

  it('toggles to Urgent priority when clicked', async () => {
    const user = userEvent.setup();
    render(<UploadZone />);

    const toggle = screen.getByTestId('priority-toggle');
    await user.click(toggle);

    expect(screen.getByText('Urgent')).toBeInTheDocument();
    expect(screen.getByText(/in testa alla coda/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run __tests__/components/admin/knowledge-base/upload-zone-priority.test.tsx`
Expected: FAIL — `priority-toggle` not found

- [ ] **Step 3: Implement priority state and toggle in UploadZone**

In `apps/web/src/components/admin/knowledge-base/upload-zone.tsx`:

Add import at top:
```tsx
import { ZapIcon } from 'lucide-react';
```

Add state after `isDragOver` state (line 51):
```tsx
const [urgentPriority, setUrgentPriority] = useState(false);
```

Add the priority toggle UI — insert **after the Game Selector div** (after line 289, before the Drop Zone div):
```tsx
{/* Priority Toggle */}
<div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-3 border border-slate-200/50 dark:border-zinc-700/50 flex items-center justify-between">
  <div className="flex items-center gap-2">
    <span className="text-sm font-medium text-slate-700 dark:text-zinc-300">
      Processing Priority
    </span>
    {urgentPriority && (
      <span className="text-xs text-amber-600 dark:text-amber-400">
        — in testa alla coda
      </span>
    )}
  </div>
  <button
    type="button"
    data-testid="priority-toggle"
    onClick={() => setUrgentPriority(prev => !prev)}
    className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-sm font-medium transition-all ${
      urgentPriority
        ? 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300 ring-1 ring-amber-300 dark:ring-amber-700'
        : 'bg-slate-100 text-slate-700 dark:bg-zinc-700 dark:text-zinc-300'
    }`}
  >
    {urgentPriority && <ZapIcon className="w-3.5 h-3.5" />}
    {urgentPriority ? 'Urgent' : 'Normal'}
  </button>
</div>
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run __tests__/components/admin/knowledge-base/upload-zone-priority.test.tsx`
Expected: PASS

- [ ] **Step 5: Wire priority to enqueuePdf calls**

In `upload-zone.tsx`, change the two `enqueuePdf` calls:

Line 86 (single upload):
```tsx
// Before:
await enqueuePdf(result.documentId);
// After:
await enqueuePdf(result.documentId, urgentPriority ? 30 : 10);
```

Line 142 (chunked upload):
```tsx
// Before:
await enqueuePdf(completeResult.documentId);
// After:
await enqueuePdf(completeResult.documentId, urgentPriority ? 30 : 10);
```

- [ ] **Step 6: Write test for priority being passed to enqueuePdf**

Add to the test file:
```tsx
import { enqueuePdf } from '@/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api';

describe('UploadZone enqueue priority', () => {
  it('calls enqueuePdf with priority 30 when Urgent is selected', async () => {
    const user = userEvent.setup();
    const mockEnqueue = vi.mocked(enqueuePdf);
    mockEnqueue.mockResolvedValue({ jobId: 'j1' });

    render(<UploadZone />);

    // Select game first (simulate pre-selected)
    // ... game selection mock needed

    // Toggle to urgent
    await user.click(screen.getByTestId('priority-toggle'));

    // Verify state changed
    expect(screen.getByText('Urgent')).toBeInTheDocument();
  });
});
```

- [ ] **Step 7: Run all upload-zone tests**

Run: `cd apps/web && pnpm vitest run __tests__/components/admin/knowledge-base/`
Expected: All PASS

- [ ] **Step 8: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/upload-zone.tsx apps/web/__tests__/components/admin/knowledge-base/upload-zone-priority.test.tsx
git commit -m "feat(admin-kb): add priority toggle to upload zone for head-of-queue insertion"
```

---

## Chunk 2: Recent PDFs Widget

### Task 2: Create Recent PDFs Widget Component

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/hub/recent-pdfs-widget.tsx`
- Test: `apps/web/__tests__/components/admin/knowledge-base/hub/recent-pdfs-widget.test.tsx`

**Context:** Uses `adminClient.getAllPdfs({ state: 'Ready', sortBy: 'processedAt', sortOrder: 'desc', pageSize: 5 })` to fetch last 5 completed PDFs. The `PdfListItem` type has: `id`, `fileName`, `gameTitle`, `processingState`, `fileSizeBytes`, `chunkCount`, `processedAt`.

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/__tests__/components/admin/knowledge-base/hub/recent-pdfs-widget.test.tsx
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const mockGetAllPdfs = vi.fn();

vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: vi.fn().mockImplementation(() => ({
    get: mockGetAllPdfs,
  })),
}));

import { RecentPdfsWidget } from '@/components/admin/knowledge-base/hub/recent-pdfs-widget';

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('RecentPdfsWidget', () => {
  beforeEach(() => {
    mockGetAllPdfs.mockResolvedValue({
      items: [
        {
          id: '1',
          fileName: 'catan-rules.pdf',
          gameTitle: 'Catan',
          processingState: 'Ready',
          fileSizeBytes: 2048000,
          chunkCount: 42,
          processedAt: '2026-03-15T10:00:00Z',
          uploadedAt: '2026-03-15T09:00:00Z',
          processingStatus: 'Completed',
          progressPercentage: 100,
          pageCount: 12,
          processingError: null,
          errorCategory: null,
          retryCount: 0,
          gameId: 'g1',
        },
      ],
      total: 1,
      page: 1,
      pageSize: 5,
    });
  });

  it('renders recent PDFs with file name and game title', async () => {
    render(<RecentPdfsWidget />, { wrapper });
    expect(await screen.findByText('catan-rules.pdf')).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('shows chunk count', async () => {
    render(<RecentPdfsWidget />, { wrapper });
    expect(await screen.findByText(/42/)).toBeInTheDocument();
  });

  it('shows empty state when no PDFs', async () => {
    mockGetAllPdfs.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 5 });
    render(<RecentPdfsWidget />, { wrapper });
    expect(await screen.findByText(/nessun documento/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run __tests__/components/admin/knowledge-base/hub/recent-pdfs-widget.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement the RecentPdfsWidget component**

```tsx
// apps/web/src/components/admin/knowledge-base/hub/recent-pdfs-widget.tsx
'use client';

import { useQuery } from '@tanstack/react-query';
import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import {
  FileTextIcon,
  CheckCircleIcon,
  BoxIcon,
  ArrowRightIcon,
} from 'lucide-react';
import Link from 'next/link';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

function formatFileSize(bytes: number): string {
  if (bytes === 0) return '0 B';
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  return `${(bytes / Math.pow(1024, i)).toFixed(1)} ${sizes[i]}`;
}

export function RecentPdfsWidget() {
  const { data, isLoading } = useQuery({
    queryKey: ['admin', 'hub', 'recent-pdfs'],
    queryFn: () =>
      adminClient.getAllPdfs({
        state: 'Ready',
        sortBy: 'processedAt',
        sortOrder: 'desc',
        pageSize: 5,
      }),
    staleTime: 30_000,
    refetchInterval: 60_000,
  });

  const pdfs = data?.items ?? [];

  return (
    <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center justify-between text-base">
          <div className="flex items-center gap-2">
            <div className="h-8 w-8 rounded-lg bg-gradient-to-br from-green-500 to-emerald-600 flex items-center justify-center">
              <CheckCircleIcon className="h-4 w-4 text-white" />
            </div>
            <span>Ultimi PDF Elaborati</span>
          </div>
          <Link
            href="/admin/knowledge-base/documents"
            className="text-xs text-amber-600 hover:text-amber-700 dark:text-amber-400 flex items-center gap-1"
          >
            Tutti <ArrowRightIcon className="h-3 w-3" />
          </Link>
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-2">
        {isLoading && (
          <div className="space-y-2">
            {Array.from({ length: 3 }).map((_, i) => (
              <Skeleton key={i} className="h-12 w-full rounded-lg" />
            ))}
          </div>
        )}

        {!isLoading && pdfs.length === 0 && (
          <p className="text-sm text-muted-foreground text-center py-4">
            Nessun documento elaborato
          </p>
        )}

        {pdfs.map((pdf) => (
          <div
            key={pdf.id}
            className="flex items-center gap-3 p-2.5 rounded-lg bg-slate-50/50 dark:bg-zinc-900/50 border border-slate-200/30 dark:border-zinc-700/30"
          >
            <FileTextIcon className="h-4 w-4 text-green-600 dark:text-green-400 shrink-0" />
            <div className="min-w-0 flex-1">
              <p className="text-sm font-medium text-slate-900 dark:text-zinc-100 truncate">
                {pdf.fileName}
              </p>
              <div className="flex items-center gap-2 text-xs text-muted-foreground">
                {pdf.gameTitle && <span>{pdf.gameTitle}</span>}
                <span>·</span>
                <span>{formatFileSize(pdf.fileSizeBytes)}</span>
                <span>·</span>
                <span className="flex items-center gap-0.5">
                  <BoxIcon className="h-3 w-3" />
                  {pdf.chunkCount} chunks
                </span>
              </div>
            </div>
            {pdf.processedAt && (
              <span className="text-xs text-muted-foreground shrink-0">
                {formatDistanceToNow(new Date(pdf.processedAt), { addSuffix: true, locale: it })}
              </span>
            )}
          </div>
        ))}
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run __tests__/components/admin/knowledge-base/hub/recent-pdfs-widget.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/hub/recent-pdfs-widget.tsx apps/web/__tests__/components/admin/knowledge-base/hub/recent-pdfs-widget.test.tsx
git commit -m "feat(admin-kb): add recent processed PDFs widget for hub dashboard"
```

---

## Chunk 3: Queue Preview Widget

### Task 3: Create Queue Preview Widget Component

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/hub/queue-preview-widget.tsx`
- Test: `apps/web/__tests__/components/admin/knowledge-base/hub/queue-preview-widget.test.tsx`

**Context:** Shows active (Processing + Queued) jobs from existing `useQueueList` hook + `useQueueStats` for counts. Reuses status config pattern from `processing-queue.tsx`. Links to full queue dashboard.

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/__tests__/components/admin/knowledge-base/hub/queue-preview-widget.test.tsx
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/app/admin/(dashboard)/knowledge-base/queue/hooks/use-queue-sse', () => ({
  useQueueSSE: () => ({ connectionState: 'connected' }),
}));

const mockFetchQueue = vi.fn();
vi.mock('@/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api', () => ({
  useQueueList: (filters: unknown, sse: boolean) => ({
    data: mockFetchQueue(),
    isLoading: false,
    error: null,
  }),
  useQueueStats: () => [
    { data: { total: 3 } },  // Queued
    { data: { total: 1 } },  // Processing
    { data: { total: 45 } }, // Completed
    { data: { total: 2 } },  // Failed
  ],
}));

import { QueuePreviewWidget } from '@/components/admin/knowledge-base/hub/queue-preview-widget';

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('QueuePreviewWidget', () => {
  beforeEach(() => {
    mockFetchQueue.mockReturnValue({
      jobs: [
        { id: 'j1', pdfFileName: 'risk-rules.pdf', status: 'Processing', currentStep: 'Extracting', createdAt: '2026-03-15T10:00:00Z', priority: 10, errorMessage: null, retryCount: 0, canRetry: false, pdfDocumentId: 'p1', userId: 'u1', startedAt: null, completedAt: null, maxRetries: 3 },
        { id: 'j2', pdfFileName: 'ticket-rules.pdf', status: 'Queued', currentStep: null, createdAt: '2026-03-15T10:01:00Z', priority: 30, errorMessage: null, retryCount: 0, canRetry: false, pdfDocumentId: 'p2', userId: 'u1', startedAt: null, completedAt: null, maxRetries: 3 },
      ],
      total: 4,
      page: 1,
      pageSize: 5,
      totalPages: 1,
    });
  });

  it('renders active jobs with status', async () => {
    render(<QueuePreviewWidget />, { wrapper });
    expect(screen.getByText('risk-rules.pdf')).toBeInTheDocument();
    expect(screen.getByText('ticket-rules.pdf')).toBeInTheDocument();
  });

  it('shows stats counters', () => {
    render(<QueuePreviewWidget />, { wrapper });
    expect(screen.getByText('3')).toBeInTheDocument(); // Queued
    expect(screen.getByText('1')).toBeInTheDocument(); // Processing
  });

  it('shows link to full queue', () => {
    render(<QueuePreviewWidget />, { wrapper });
    expect(screen.getByRole('link', { name: /coda completa/i })).toHaveAttribute('href', '/admin/knowledge-base/queue');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run __tests__/components/admin/knowledge-base/hub/queue-preview-widget.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement the QueuePreviewWidget component**

```tsx
// apps/web/src/components/admin/knowledge-base/hub/queue-preview-widget.tsx
'use client';

import {
  ListOrderedIcon,
  LoaderIcon,
  ClockIcon,
  CheckCircleIcon,
  XCircleIcon,
  ArrowRightIcon,
  FileIcon,
} from 'lucide-react';
import Link from 'next/link';

import { useQueueSSE } from '@/app/admin/(dashboard)/knowledge-base/queue/hooks/use-queue-sse';
import {
  useQueueList,
  useQueueStats,
  type JobStatus,
} from '@/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';

const statusStyles: Record<JobStatus, { badge: string; label: string }> = {
  Queued: { badge: 'bg-slate-100 text-slate-800 dark:bg-slate-900/30 dark:text-slate-300', label: 'In coda' },
  Processing: { badge: 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300', label: 'In elaborazione' },
  Completed: { badge: 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300', label: 'Completato' },
  Failed: { badge: 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300', label: 'Fallito' },
  Cancelled: { badge: 'bg-gray-100 text-gray-800 dark:bg-gray-900/30 dark:text-gray-300', label: 'Annullato' },
};

export function QueuePreviewWidget() {
  const { connectionState } = useQueueSSE(true);
  const sseConnected = connectionState === 'connected';

  const { data, isLoading } = useQueueList(
    { pageSize: 5 },
    sseConnected
  );

  const statsQueries = useQueueStats();
  const queuedCount = statsQueries[0]?.data?.total ?? 0;
  const processingCount = statsQueries[1]?.data?.total ?? 0;
  const completedCount = statsQueries[2]?.data?.total ?? 0;
  const failedCount = statsQueries[3]?.data?.total ?? 0;

  const activeJobs = (data?.jobs ?? []).filter(
    (j) => j.status === 'Processing' || j.status === 'Queued'
  );

  return (
    <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center justify-between text-base">
          <div className="flex items-center gap-2">
            <div className="h-8 w-8 rounded-lg bg-gradient-to-br from-cyan-500 to-teal-600 flex items-center justify-center">
              <ListOrderedIcon className="h-4 w-4 text-white" />
            </div>
            <span>Coda Elaborazione</span>
          </div>
          <Link
            href="/admin/knowledge-base/queue"
            className="text-xs text-amber-600 hover:text-amber-700 dark:text-amber-400 flex items-center gap-1"
          >
            Coda completa <ArrowRightIcon className="h-3 w-3" />
          </Link>
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {/* Stats Counters */}
        <div className="grid grid-cols-4 gap-2">
          {[
            { label: 'In coda', count: queuedCount, icon: ClockIcon, color: 'text-slate-600 dark:text-slate-400' },
            { label: 'Attivi', count: processingCount, icon: LoaderIcon, color: 'text-blue-600 dark:text-blue-400' },
            { label: 'Completati', count: completedCount, icon: CheckCircleIcon, color: 'text-green-600 dark:text-green-400' },
            { label: 'Falliti', count: failedCount, icon: XCircleIcon, color: 'text-red-600 dark:text-red-400' },
          ].map(({ label, count, icon: Icon, color }) => (
            <div key={label} className="text-center p-2 rounded-lg bg-slate-50/50 dark:bg-zinc-900/50">
              <Icon className={`h-3.5 w-3.5 mx-auto mb-0.5 ${color}`} />
              <p className="text-lg font-bold text-slate-900 dark:text-zinc-100">{count}</p>
              <p className="text-[10px] text-muted-foreground">{label}</p>
            </div>
          ))}
        </div>

        {/* Active Jobs */}
        {isLoading && (
          <div className="space-y-2">
            {Array.from({ length: 2 }).map((_, i) => (
              <Skeleton key={i} className="h-10 w-full rounded-lg" />
            ))}
          </div>
        )}

        {!isLoading && activeJobs.length === 0 && (
          <p className="text-sm text-muted-foreground text-center py-2">
            Nessun job attivo
          </p>
        )}

        {activeJobs.map((job) => {
          const style = statusStyles[job.status] ?? statusStyles.Queued;
          return (
            <div
              key={job.id}
              className="flex items-center gap-3 p-2 rounded-lg bg-slate-50/50 dark:bg-zinc-900/50 border border-slate-200/30 dark:border-zinc-700/30"
            >
              <FileIcon className="h-4 w-4 text-slate-500 dark:text-zinc-400 shrink-0" />
              <div className="min-w-0 flex-1">
                <p className="text-sm font-medium text-slate-900 dark:text-zinc-100 truncate">
                  {job.pdfFileName}
                </p>
                {job.currentStep && (
                  <p className="text-xs text-blue-600 dark:text-blue-400">
                    Step: {job.currentStep}
                  </p>
                )}
              </div>
              <Badge variant="outline" className={style.badge}>
                {job.status === 'Processing' && (
                  <LoaderIcon className="w-3 h-3 mr-1 animate-spin" />
                )}
                {style.label}
              </Badge>
            </div>
          );
        })}
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run __tests__/components/admin/knowledge-base/hub/queue-preview-widget.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/hub/queue-preview-widget.tsx apps/web/__tests__/components/admin/knowledge-base/hub/queue-preview-widget.test.tsx
git commit -m "feat(admin-kb): add queue preview widget for hub dashboard"
```

---

## Chunk 4: Integrate Widgets into KB Hub Page

### Task 4: Convert KB Hub to Live Dashboard

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/hub/kb-hub-client.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/page.tsx`

**Context:** The hub page is currently a Server Component with static card links. We need to add a client section below the header that renders the two live widgets, while keeping the existing section cards and quick links intact.

- [ ] **Step 1: Create the KbHubClient wrapper component**

```tsx
// apps/web/src/components/admin/knowledge-base/hub/kb-hub-client.tsx
'use client';

import { QueuePreviewWidget } from './queue-preview-widget';
import { RecentPdfsWidget } from './recent-pdfs-widget';

export function KbHubClient() {
  return (
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
      <QueuePreviewWidget />
      <RecentPdfsWidget />
    </div>
  );
}
```

- [ ] **Step 2: Update the KB hub page to include KbHubClient**

In `apps/web/src/app/admin/(dashboard)/knowledge-base/page.tsx`:

Add imports:
```tsx
import { Suspense } from 'react';
import { KbHubClient } from '@/components/admin/knowledge-base/hub/kb-hub-client';
```

Insert after the `{/* Header */}` div (after line 97), before `{/* Section Cards */}`:
```tsx
{/* Live Dashboard Widgets */}
<Suspense
  fallback={
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
      <div className="h-[280px] bg-white/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse" />
      <div className="h-[280px] bg-white/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse" />
    </div>
  }
>
  <KbHubClient />
</Suspense>
```

- [ ] **Step 3: Verify the page renders correctly**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds without errors

- [ ] **Step 4: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: No type errors

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/hub/kb-hub-client.tsx apps/web/src/app/admin/(dashboard)/knowledge-base/page.tsx
git commit -m "feat(admin-kb): integrate live widgets into KB hub dashboard"
```

---

## Chunk 5: Final Verification

### Task 5: Full Test Suite and Lint

- [ ] **Step 1: Run all KB-related tests**

Run: `cd apps/web && pnpm vitest run __tests__/components/admin/knowledge-base/`
Expected: All PASS

- [ ] **Step 2: Run lint**

Run: `cd apps/web && pnpm lint`
Expected: No errors

- [ ] **Step 3: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: No type errors

- [ ] **Step 4: Final commit (if any fixes needed)**

```bash
git commit -m "fix(admin-kb): address lint/type issues in KB hub enrichment"
```

---

## Summary of Changes

| Change | Impact | User Flow |
|--------|--------|-----------|
| **Priority toggle in upload** | Admin can choose Urgent(30) to insert PDF at head of queue | Upload page: toggle → upload → enqueued at top |
| **Recent PDFs widget** | Hub shows last 5 completed PDFs with game, size, chunks | Hub page: see recent completions at a glance |
| **Queue preview widget** | Hub shows queue stats (4 counters) + active jobs | Hub page: see queue health without navigating away |
| **Hub page enrichment** | Static nav → live dashboard with widgets above nav cards | Single page shows overview + navigation |
