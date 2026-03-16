# KB Admin Flow Improvements — Design Spec

**Date**: 2026-03-15
**Scope**: 5 improvements to admin KB/PDF management flow
**Branch**: `feature/kb-admin-flow-improvements` from `frontend-dev`

## Context

Spec-panel review of the admin KB/PDF flow identified issues across reliability, UX, and test coverage. All improvements are incremental changes to existing components — no new architecture required.

**Note**: P1b (bumpPriority UI) was removed — `queue-item-actions.tsx` already implements a bump priority dropdown with "Bump to High" and "Bump to Urgent" options, mutation, toasts, and query invalidation. Future enhancement: add "Low" and "Normal" options to the existing component if needed.

## Implementation Order

| # | Priority | Item | Files | Effort |
|---|----------|------|-------|--------|
| 1 | P3 | Verify processingState vs processingStatus mapping | `recent-pdfs-widget.tsx`, `adminClient.ts`, `admin-knowledge-base.schemas.ts` | S |
| 2 | P0a | Toast error on enqueue failure + redirect (both upload paths) | `upload-zone.tsx`, `documents/page.tsx` | S |
| 3 | P0b | Optimized polling (Intersection Observer + adaptive interval) | `usePdfProcessingStatus.ts` | M |
| 4 | P1a | Priority badge in queue preview | New `priority-badge.tsx`, `queue-preview-widget.tsx`, `queue/page.tsx` | S |
| 5 | P2 | Component-level tests for full flow | 4 new test files | M |

## 1. P3 — Verify ProcessingState vs ProcessingStatus Mapping

### Problem

The `PdfListItem` schema (in `admin-knowledge-base.schemas.ts`) has two separate string fields: `processingState` (used as query filter parameter in `getAllPdfs()`) and `processingStatus` (display field). `RecentPdfsWidget` filters with `state: 'Ready'` which maps to the `processingState` field. We need to verify that `'Ready'` is the correct backend enum value for "fully processed and indexed PDF" and document the relationship between these two fields.

### Intervention

1. Check `admin-knowledge-base.schemas.ts` for the `PdfListItem` schema — confirm both `processingState` and `processingStatus` fields exist
2. Verify in `adminClient.ts` that the `state` query parameter maps to `processingState`
3. Verify `'Ready'` is a valid backend enum value for `processingState` meaning "fully processed"
4. If mismatch found → fix the filter in the widget
5. Add inline comment documenting the `processingState` (filter) vs `processingStatus` (display) distinction

### Files

- `apps/web/src/components/admin/knowledge-base/hub/recent-pdfs-widget.tsx`
- `apps/web/src/lib/api/clients/adminClient.ts`
- `apps/web/src/lib/api/schemas/admin-knowledge-base.schemas.ts`

### Risk

Low. If filter is correct, no changes. If wrong, one-liner fix.

## 2. P0a — Toast Error on Enqueue Failure + Redirect

### Problem

If `enqueuePdf()` fails after a successful upload, it's a silent failure — only console.log. The admin doesn't know the PDF wasn't queued for processing.

### Intervention

In `upload-zone.tsx`, there are **two independent upload paths** that both have empty catch blocks for enqueue:
- `uploadSingleFile()` (files < 10MB) — uses `result.documentId`
- `uploadChunkedFile()` (files > 10MB) — uses `completeResult.documentId`

**Both must be fixed.** Extract a shared handler to avoid duplication:

```typescript
function handleEnqueueError(documentId: string, error: unknown) {
  toast({
    variant: "destructive",
    title: "PDF caricato ma non accodato",
    description: error instanceof Error ? error.message : "Errore durante l'accodamento",
  });
  setTimeout(() => {
    router.push(`/admin/knowledge-base/documents?highlight=${documentId}`);
  }, 2000);
}
```

1. Call `handleEnqueueError(documentId, err)` in both catch blocks
2. In the documents page, the orphaned PDF will have status `Pending` (uploaded but not queued). Verify that existing "Reindex" button or similar action can enqueue it manually.

### Files

- `apps/web/src/components/admin/knowledge-base/upload-zone.tsx` — main change
- `apps/web/src/app/admin/(dashboard)/knowledge-base/documents/page.tsx` — highlight logic if needed

### Behavior Matrix

| Upload | Enqueue | Result |
|--------|---------|--------|
| OK | OK | Current flow (no change) |
| OK | FAIL | Toast + redirect to documents |
| FAIL | — | Current error handling (no change) |

### Risk

Low. Success path unchanged. Only the enqueue error path is modified.

## 3. P0b — Optimized Polling

### Problem

`usePdfProcessingStatus` polls per-gameId every 3s. N visible cards = N API calls every 3 seconds. Performance degradation risk with many cards.

### Intervention

#### 3a. Intersection Observer

New wrapper hook `usePdfProcessingStatusVisible(gameId, elementRef)`:
- Accepts an optional `ref` of the card DOM element
- When card is not visible in viewport → `enabled: false` on useQuery (suspends polling)
- When card scrolls back into view → polling resumes

#### 3b. Adaptive Interval

In `usePdfProcessingStatus.ts`:
- Start: 3s interval
- After 10 cycles with no state change: 5s
- After 20 cycles with no state change: 10s
- Reset to 3s when state actually changes

Implementation detail: The existing hook already uses `refetchInterval` as a function that checks terminal status. Extend it as follows:

1. Add `prevStatusRef = useRef<string | null>(null)` to track previous status
2. Add `cycleCountRef = useRef(0)` to count unchanged cycles
3. Inside `refetchInterval` callback:
   - Compare `query.state.data?.status` to `prevStatusRef.current`
   - If equal → increment `cycleCountRef`
   - If different → reset `cycleCountRef` to 0, update `prevStatusRef`
   - Return interval based on `cycleCountRef`: `<10 → 3000`, `<20 → 5000`, `≥20 → 10000`
   - If terminal status → return `false` (existing behavior preserved)

#### 3c. Deduplication

React Query already deduplicates queries with the same `queryKey`. Verify that the key is `['pdf-status', gameId]`. If two components mount the same gameId, only one fetch is executed.

### Files

- `apps/web/src/hooks/queries/usePdfProcessingStatus.ts` — adaptive interval + dedup verification
- New file: `apps/web/src/hooks/queries/usePdfProcessingStatusVisible.ts` — Intersection Observer wrapper

### Backward Compatibility

The original hook remains unchanged in its public API. The new wrapper is opt-in. Existing consumers don't break.

### Risk

Medium-low. Polling behavior unchanged for visible cards in the first 30s. Only cards outside viewport or after long idle see differences.

## 4. P1a — Priority Badge in Queue Preview

### Problem

Admin cannot distinguish urgent from normal jobs in the queue widget.

### Intervention

#### New Component: `PriorityBadge`

Location: `apps/web/src/components/admin/knowledge-base/priority-badge.tsx` (kebab-case per project convention)

Props: `{ priority: number; showNormal?: boolean }`

Rendering rules:
| Priority | Icon | Label | Style | Show by default |
|----------|------|-------|-------|-----------------|
| ≥ 30 | 🔥 | Urgente | `bg-red-50 text-red-700 border-red-200` | Yes |
| ≥ 20 | ⬆ | Alta | `bg-orange-50 text-orange-700 border-orange-200` | Yes |
| ≥ 10 | — | Normale | — | No (reduces visual noise) |
| < 10 | ⬇ | Bassa | `bg-sky-50 text-sky-700 border-sky-200` | Yes |

When `showNormal={true}`, renders a muted badge for Normal priority too.

#### Integration

- **Queue preview widget** (`queue-preview-widget.tsx`): show `PriorityBadge` next to job filename, only if priority ≠ Normal
- **Full queue page** (`queue/page.tsx`): show `PriorityBadge` inline for every job (with `showNormal={true}`)

### Backend Dependency

The queue job DTO must include a `priority` field. If not present in the current response, document as a backend gap and render badge only when field is available.

### Files

- New: `apps/web/src/components/admin/knowledge-base/priority-badge.tsx`
- Modify: `apps/web/src/components/admin/knowledge-base/hub/queue-preview-widget.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/page.tsx`

### Risk

Low. Pure presentational component, no state logic.

## 5. P2 — Component-Level Tests

### Problem

No tests cover the upload → processing → user badge flow.

### Test Files

All in `apps/web/__tests__/components/admin/knowledge-base/`

#### 5a. `upload-zone.test.tsx`

Tests the enqueue error flow:
- Mock `uploadPdf` → success, mock `enqueuePdf` → error
- Verify: destructive toast appears
- Verify: redirect to documents page occurs

#### 5b. `usePdfProcessingStatus.test.ts`

**Dependency**: This test requires Item 3b (adaptive interval) to be implemented first. If 3b is deferred, scope the test to cover only static 3s polling and terminal state stopping.

Tests optimized polling:
- Verify adaptive interval (3s → 5s → 10s after N cycles without change)
- Verify polling stops on terminal states (`indexed`, `failed`)
- Verify queryKey deduplication

#### 5c. `KbStatusBadge.test.tsx`

Tests the `KbStatusBadge` component (canonical export, not the deprecated `DocumentStatusBadge` alias). Note: an existing `StatusBadge.test.tsx` may already exist — augment or replace as needed.

Tests 4 status transitions:
- `processing` → blue animated badge, text "In elaborazione"
- `indexed` → green badge, text "Indicizzata"
- `failed` → red badge, text "Errore"
- `none` → gray badge, text "Non indicizzata"

#### 5d. `PriorityBadge.test.tsx`

Tests 4 priority levels:
- priority ≥ 30 → 🔥 icon, red style
- priority ≥ 20 → ⬆ icon, orange style
- priority ≥ 10 → does not render (Normal hidden by default)
- priority < 10 → ⬇ icon, sky style
- With `showNormal={true}` → renders Normal

### Framework

Vitest + React Testing Library (already used in project)

### Risk

Zero. Isolated tests, no production code changes.

## Summary of All File Changes

### New Files
| File | Purpose |
|------|---------|
| `components/admin/knowledge-base/priority-badge.tsx` | Priority level badge component |
| `hooks/queries/usePdfProcessingStatusVisible.ts` | Intersection Observer polling wrapper |
| `__tests__/.../upload-zone.test.tsx` | Upload enqueue error test |
| `__tests__/.../usePdfProcessingStatus.test.ts` | Polling optimization test |
| `__tests__/.../KbStatusBadge.test.tsx` | Status badge test |
| `__tests__/.../PriorityBadge.test.tsx` | Priority badge test |

### Modified Files
| File | Change |
|------|--------|
| `upload-zone.tsx` | Toast + redirect on enqueue failure (both upload paths) |
| `usePdfProcessingStatus.ts` | Adaptive interval logic with prevStatusRef + cycleCountRef |
| `recent-pdfs-widget.tsx` | Verify/fix processingState filter + add comment |
| `queue-preview-widget.tsx` | Add PriorityBadge |
| `queue/page.tsx` | Add PriorityBadge inline |
| `documents/page.tsx` | Highlight logic for orphaned PDF (if needed) |

### Unchanged
| File | Reason |
|------|--------|
| `adminClient.ts` | Read-only verification |
| `queue-api.ts` | API already exists, no changes needed |
| `queue-item-actions.tsx` | bumpPriority UI already implemented (High/Urgent options) |
| `DocumentStatusBadge.tsx` | Already correct, only tested |
| `meeple-card.tsx` | No changes needed |
