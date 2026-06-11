/**
 * Admin Wikidata Cover Enrichment — Dead-Letter Visibility Page.
 * Issue #1823 Wave 3 M13.
 *
 * Minimal MVP scope:
 *   - paginated list of dead-letter attempts (server endpoint already paginates)
 *   - optional reason filter
 *   - per-row retry button (POST {gameId} with forceRefresh=true via M12)
 *
 * Out-of-scope for this iteration (tracked in Wave 3 follow-up):
 *   - bulk retry / acknowledge
 *   - per-row drawer with full attempt history (joins WikidataCoverEnrichmentAttempt
 *     timeline)
 *   - real-time refresh via SSE on scheduler-emitted events
 */

'use client';

import { useCallback, useEffect, useState } from 'react';

import {
  listDeadLetters,
  retryDeadLetter,
  type WikidataDeadLetterAttemptDto,
} from '@/lib/api/admin-wikidata-dead-letters';

const PAGE_SIZE = 50;

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

export default function WikidataDeadLettersPage() {
  const [items, setItems] = useState<WikidataDeadLetterAttemptDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(0);
  const [reasonFilter, setReasonFilter] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [retryStatus, setRetryStatus] = useState<Record<string, RetryStatus>>({});

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await listDeadLetters({
        skip: page * PAGE_SIZE,
        take: PAGE_SIZE,
        reason: reasonFilter || undefined,
      });
      setItems(response.items);
      setTotalCount(response.totalCount);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setLoading(false);
    }
  }, [page, reasonFilter]);

  useEffect(() => {
    void load();
  }, [load]);

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

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

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

        <div className="ml-auto text-sm text-muted-foreground">
          {totalCount} dead-letter{totalCount === 1 ? '' : 's'} matching filter
        </div>
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
              return (
                <tr key={row.id} className="border-b">
                  <td className="p-2 font-medium">{row.gameTitle}</td>
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
    </main>
  );
}
