/**
 * Admin Wikidata Cover Enrichment — Dead-Letter Visibility Page.
 * Issue #1823 Wave 3 M13 + Phase E F2 (bulk-retry) + F3 (per-row timeline drawer).
 *
 * In-scope:
 *   - paginated list of dead-letter attempts (server endpoint already paginates)
 *   - optional reason filter
 *   - per-row retry button (POST {gameId} with forceRefresh=true via M12)
 *   - F2: checkbox column + bulk retry toolbar (max 50 per batch)
 *   - F3: click on game title → drawer with WikidataCoverEnrichmentAttempt timeline
 *
 * Out-of-scope for this iteration:
 *   - real-time refresh via SSE on scheduler-emitted events (Phase E F4 candidate)
 */

'use client';

import { useCallback, useEffect, useState } from 'react';

import {
  BULK_ACKNOWLEDGE_MAX_BATCH,
  BULK_RETRY_MAX_BATCH,
  bulkAcknowledgeDeadLetters,
  bulkRetryDeadLetters,
  listDeadLetters,
  retryDeadLetter,
  type AdminBulkAcknowledgeResult,
  type AdminBulkRetryWikidataCoverResult,
  type WikidataDeadLetterAttemptDto,
} from '@/lib/api/admin-wikidata-dead-letters';

import { AcknowledgeSelectedModal } from './AcknowledgeSelectedModal';
import { AttemptTimelineDrawer } from './AttemptTimelineDrawer';
import { useWikidataEnrichmentEvents } from './useWikidataEnrichmentEvents';

const PAGE_SIZE = 50;

// Note: 'circuit-open' (FailReasonCircuitOpen on the BE) is intentionally
// omitted — the retry policy always schedules a retry for breaker trips,
// it never produces a DeadLetter row, so filtering by it would always
// return zero results. Surfaced via the meepleai.wikidata.* metric tags
// instead.
const REASON_FILTERS: { value: string; label: string }[] = [
  { value: '', label: 'Any reason' },
  { value: 'r2-upload-error', label: 'R2 upload error' },
  { value: 'image-processing-error', label: 'Image processing error' },
  { value: 'license-not-whitelisted', label: 'License not whitelisted' },
  { value: 'image-not-available-p18', label: 'No P18 claim' },
  { value: 'image-bytes-not-available', label: 'Image bytes 404' },
  { value: 'qid-missing', label: 'QID missing' },
];

interface RetryStatus {
  state: 'idle' | 'running' | 'done' | 'error';
  outcome?: string;
  error?: string;
}

interface DrawerState {
  gameId: string;
  gameTitle: string;
}

export default function WikidataDeadLettersPage() {
  const [items, setItems] = useState<WikidataDeadLetterAttemptDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(0);
  const [reasonFilter, setReasonFilter] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [retryStatus, setRetryStatus] = useState<Record<string, RetryStatus>>({});
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [bulkState, setBulkState] = useState<
    | { state: 'idle' }
    | { state: 'running' }
    | { state: 'done'; result: AdminBulkRetryWikidataCoverResult }
    | { state: 'error'; error: string }
  >({ state: 'idle' });
  // Phase F F5 — bulk acknowledge (Issue #2254)
  const [includeAcknowledged, setIncludeAcknowledged] = useState(false);
  const [ackModalOpen, setAckModalOpen] = useState(false);
  const [ackState, setAckState] = useState<
    | { state: 'idle' }
    | { state: 'running' }
    | { state: 'done'; result: AdminBulkAcknowledgeResult }
    | { state: 'error'; error: string }
  >({ state: 'idle' });
  const [drawer, setDrawer] = useState<DrawerState | null>(null);

  // Phase E F4 — live attempt-recorded SSE feed. Each new event triggers a
  // page reload (cheap: same paginated endpoint already used by Refresh).
  const sse = useWikidataEnrichmentEvents();

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await listDeadLetters({
        skip: page * PAGE_SIZE,
        take: PAGE_SIZE,
        reason: reasonFilter || undefined,
        includeAcknowledged,
      });
      setItems(response.items);
      setTotalCount(response.totalCount);
      // Reset selection on every page/filter change — the previously selected
      // rows may no longer be visible, and bulk-retrying invisible rows would
      // be a surprising UX violation.
      setSelectedIds(new Set());
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setLoading(false);
    }
  }, [page, reasonFilter, includeAcknowledged]);

  useEffect(() => {
    void load();
  }, [load]);

  // F4: refresh the list when the BE broadcasts a new attempt event.
  // load() is stable across renders thanks to useCallback, so the deps array
  // intentionally pins to lastEvent.attemptId — replaying the same event
  // never re-loads, but a new event always does.
  const lastEventAttemptId = sse.lastEvent?.attemptId ?? null;
  useEffect(() => {
    if (!lastEventAttemptId) return;
    void load();
  }, [lastEventAttemptId, load]);

  const handleRetry = async (row: WikidataDeadLetterAttemptDto) => {
    setRetryStatus(prev => ({ ...prev, [row.id]: { state: 'running' } }));
    try {
      const result = await retryDeadLetter(row.sharedGameId);
      setRetryStatus(prev => ({
        ...prev,
        [row.id]: { state: 'done', outcome: result.outcome },
      }));
    } catch (err) {
      setRetryStatus(prev => ({
        ...prev,
        [row.id]: {
          state: 'error',
          error: err instanceof Error ? err.message : 'Unknown error',
        },
      }));
    }
  };

  const toggleSelect = (id: string) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else if (next.size < BULK_RETRY_MAX_BATCH) {
        next.add(id);
      }
      return next;
    });
  };

  const togglePageSelection = () => {
    setSelectedIds(prev => {
      const allSelected = items.every(i => prev.has(i.id));
      if (allSelected) return new Set();
      const next = new Set(prev);
      for (const item of items) {
        if (next.size >= BULK_RETRY_MAX_BATCH) break;
        next.add(item.id);
      }
      return next;
    });
  };

  const handleBulkRetry = async () => {
    const ids = Array.from(selectedIds);
    if (ids.length === 0) return;
    setBulkState({ state: 'running' });
    try {
      const result = await bulkRetryDeadLetters(ids);
      setBulkState({ state: 'done', result });
      // After a bulk run, reload to reflect new attempt rows.
      void load();
    } catch (err) {
      setBulkState({
        state: 'error',
        error: err instanceof Error ? err.message : 'Unknown error',
      });
    }
  };

  // Phase F F5 — handler invoked by the AcknowledgeSelectedModal Confirm action.
  // Note is forwarded to the BE log-only (DEC-F-4), not persisted on the row.
  const handleAcknowledgeConfirm = useCallback(
    async (note: string | null) => {
      setAckState({ state: 'running' });
      try {
        const result = await bulkAcknowledgeDeadLetters(Array.from(selectedIds), note);
        setAckState({ state: 'done', result });
        setSelectedIds(new Set());
        setAckModalOpen(false);
        await load();
      } catch (err) {
        setAckState({
          state: 'error',
          error: err instanceof Error ? err.message : 'Unknown error',
        });
      }
    },
    [selectedIds, load]
  );

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));
  const selectionAtCap = selectedIds.size >= BULK_RETRY_MAX_BATCH;
  const allOnPageSelected = items.length > 0 && items.every(i => selectedIds.has(i.id));

  return (
    <main className="space-y-4 p-6">
      <header className="space-y-1">
        <h1 className="text-2xl font-semibold">Wikidata enrichment — dead-letters</h1>
        <p className="text-sm text-muted-foreground">
          Attempts that exhausted the DEC-3j retry budget or failed with a non-retriable reason
          (corrupted image, license not whitelisted, etc.). 7-day retention.
        </p>
      </header>

      <section className="flex flex-wrap items-center gap-3">
        <label htmlFor="reason-filter" className="text-sm font-medium">
          Reason
        </label>
        <select
          id="reason-filter"
          className="rounded border bg-card px-2 py-1 text-sm"
          value={reasonFilter}
          onChange={e => {
            setReasonFilter(e.target.value);
            setPage(0);
          }}
        >
          {REASON_FILTERS.map(f => (
            <option key={f.value} value={f.value}>
              {f.label}
            </option>
          ))}
        </select>

        <button
          type="button"
          className="rounded border px-3 py-1 text-sm hover:bg-muted"
          onClick={() => void load()}
          disabled={loading}
        >
          Refresh
        </button>

        {/* Phase F F5 — toggle for acknowledged rows (default hidden). */}
        <label className="flex items-center gap-2 text-sm text-foreground">
          <button
            type="button"
            role="switch"
            aria-checked={includeAcknowledged}
            aria-label="Show acknowledged"
            onClick={() => {
              setIncludeAcknowledged(v => !v);
              setPage(0);
            }}
            className={`relative inline-flex h-5 w-9 items-center rounded-full transition-colors ${
              includeAcknowledged ? 'bg-primary' : 'bg-muted'
            }`}
          >
            <span
              className={`inline-block h-4 w-4 rounded-full bg-background shadow transition-transform ${
                includeAcknowledged ? 'translate-x-4' : 'translate-x-0.5'
              }`}
            />
          </button>
          Show acknowledged
        </label>

        <div className="ml-auto flex items-center gap-3 text-sm text-muted-foreground">
          <span
            className="flex items-center gap-1"
            aria-live="polite"
            title={`Live event stream: ${sse.state}`}
          >
            <span
              className={`inline-block size-2 rounded-full ${
                sse.state === 'open'
                  ? 'bg-emerald-500'
                  : sse.state === 'connecting'
                    ? 'bg-amber-500 motion-safe:animate-pulse'
                    : 'bg-destructive'
              }`}
              aria-hidden="true"
            />
            <span className="text-xs">{sse.state}</span>
          </span>
          <span>
            {totalCount} dead-letter{totalCount === 1 ? '' : 's'} matching filter
          </span>
        </div>
      </section>

      {/* Phase E F2 — bulk-retry toolbar. Sticky so it stays visible while
          scrolling a long table. */}
      <section
        aria-label="Bulk actions"
        className="sticky top-0 z-10 flex flex-wrap items-center gap-3 rounded border bg-card p-3 shadow-sm"
      >
        <span className="text-sm font-medium">
          {selectedIds.size} selected
          {selectionAtCap && (
            <span className="ml-1 text-xs text-amber-700">(max {BULK_RETRY_MAX_BATCH})</span>
          )}
        </span>

        <button
          type="button"
          className="rounded bg-primary px-3 py-1 text-sm text-primary-foreground hover:opacity-90 disabled:opacity-50"
          onClick={() => void handleBulkRetry()}
          disabled={selectedIds.size === 0 || bulkState.state === 'running'}
        >
          {bulkState.state === 'running' ? 'Retrying…' : 'Retry selected'}
        </button>

        {/* Phase F F5 — bulk acknowledge (Issue #2254). Opens the confirmation
            modal; the modal owns the optional note and forwards to the BE. */}
        <button
          type="button"
          className="rounded bg-secondary px-3 py-1 text-sm font-medium text-secondary-foreground hover:bg-secondary/90 disabled:opacity-50"
          onClick={() => setAckModalOpen(true)}
          disabled={
            selectedIds.size === 0 ||
            selectedIds.size > BULK_ACKNOWLEDGE_MAX_BATCH ||
            ackState.state === 'running'
          }
        >
          {ackState.state === 'running'
            ? 'Acknowledging…'
            : `Acknowledge selected (${selectedIds.size})`}
        </button>

        <button
          type="button"
          className="rounded border px-3 py-1 text-sm hover:bg-muted disabled:opacity-50"
          onClick={() => setSelectedIds(new Set())}
          disabled={selectedIds.size === 0 || bulkState.state === 'running'}
        >
          Clear selection
        </button>

        {bulkState.state === 'done' && (
          <span className="text-xs text-muted-foreground">
            ✔ {bulkState.result.triggeredCount} triggered · {bulkState.result.notFoundCount}{' '}
            not-found · {bulkState.result.failedCount} failed
          </span>
        )}
        {bulkState.state === 'error' && (
          <span className="text-xs text-destructive" role="alert">
            ✗ {bulkState.error}
          </span>
        )}
        {ackState.state === 'done' && (
          <span className="text-xs text-muted-foreground">
            ✔ {ackState.result.ackedCount} acked · {ackState.result.idempotentNoOpCount}{' '}
            already-acked · {ackState.result.notFoundCount} not-found
          </span>
        )}
        {ackState.state === 'error' && (
          <span className="text-xs text-destructive" role="alert">
            ✗ {ackState.error}
          </span>
        )}
      </section>

      {error && (
        <div
          role="alert"
          className="rounded border border-destructive bg-destructive/10 p-3 text-sm"
        >
          {error}
        </div>
      )}

      {loading && items.length === 0 ? (
        <div className="text-sm text-muted-foreground">Loading…</div>
      ) : items.length === 0 ? (
        <div className="rounded border bg-card p-6 text-center text-sm text-muted-foreground">
          No dead-letters match the current filter.
        </div>
      ) : (
        <table className="w-full border-collapse text-sm">
          <thead className="bg-muted text-left">
            <tr>
              <th className="border-b p-2">
                <input
                  type="checkbox"
                  aria-label="Select all rows on this page"
                  checked={allOnPageSelected}
                  onChange={togglePageSelection}
                />
              </th>
              <th className="border-b p-2">Game</th>
              <th className="border-b p-2">Reason</th>
              <th className="border-b p-2">Details</th>
              <th className="border-b p-2">Dead-lettered</th>
              <th className="border-b p-2">Retries</th>
              <th className="border-b p-2">Action</th>
            </tr>
          </thead>
          <tbody>
            {items.map(row => {
              const status = retryStatus[row.id]?.state ?? 'idle';
              const checked = selectedIds.has(row.id);
              const isAcked = row.acknowledgedAt !== null;
              return (
                <tr key={row.id} className={`border-b ${isAcked ? 'opacity-60' : ''}`}>
                  <td className="p-2">
                    <input
                      type="checkbox"
                      aria-label={`Select ${row.gameTitle}`}
                      checked={checked}
                      onChange={() => toggleSelect(row.id)}
                      disabled={!checked && selectionAtCap}
                    />
                  </td>
                  <td className="p-2 font-medium">
                    <button
                      type="button"
                      className="text-left underline-offset-2 hover:underline"
                      onClick={() =>
                        setDrawer({ gameId: row.sharedGameId, gameTitle: row.gameTitle })
                      }
                      aria-label={`Open timeline for ${row.gameTitle}`}
                    >
                      {row.gameTitle}
                    </button>
                    {isAcked && row.acknowledgedByFullName && row.acknowledgedAt && (
                      <span className="ml-2 inline-flex rounded bg-muted px-2 py-0.5 text-xs text-muted-foreground">
                        Acked by {row.acknowledgedByFullName} on{' '}
                        {new Date(row.acknowledgedAt).toLocaleDateString()}
                      </span>
                    )}
                  </td>
                  <td className="p-2">
                    <code className="rounded bg-muted px-1 py-0.5 text-xs">{row.reason}</code>
                  </td>
                  <td className="p-2 text-xs text-muted-foreground">{row.details ?? '—'}</td>
                  <td className="p-2 text-xs text-muted-foreground">
                    {new Date(row.deadLetteredAt).toISOString().slice(0, 19).replace('T', ' ')}
                  </td>
                  <td className="p-2 text-xs">{row.retryCount}</td>
                  <td className="p-2">
                    <button
                      type="button"
                      className="rounded border px-2 py-1 text-xs hover:bg-muted disabled:opacity-50"
                      onClick={() => void handleRetry(row)}
                      disabled={status === 'running'}
                    >
                      {status === 'running'
                        ? 'Retrying…'
                        : status === 'done'
                          ? `Retry → ${retryStatus[row.id]?.outcome}`
                          : status === 'error'
                            ? 'Retry failed'
                            : 'Retry'}
                    </button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}

      <nav className="flex items-center justify-between text-sm">
        <button
          type="button"
          className="rounded border px-3 py-1 hover:bg-muted disabled:opacity-50"
          onClick={() => setPage(p => Math.max(0, p - 1))}
          disabled={page === 0 || loading}
        >
          Previous
        </button>
        <span className="text-muted-foreground">
          Page {page + 1} / {totalPages}
        </span>
        <button
          type="button"
          className="rounded border px-3 py-1 hover:bg-muted disabled:opacity-50"
          onClick={() => setPage(p => p + 1)}
          disabled={(page + 1) * PAGE_SIZE >= totalCount || loading}
        >
          Next
        </button>
      </nav>

      {drawer && (
        <AttemptTimelineDrawer
          gameId={drawer.gameId}
          gameTitle={drawer.gameTitle}
          open={drawer !== null}
          onClose={() => setDrawer(null)}
        />
      )}

      <AcknowledgeSelectedModal
        open={ackModalOpen}
        selectedCount={selectedIds.size}
        onConfirm={handleAcknowledgeConfirm}
        onCancel={() => setAckModalOpen(false)}
      />
    </main>
  );
}
