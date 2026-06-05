'use client';

import { useState } from 'react';

import { Check, ChevronDown, ChevronRight, Loader2, X } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/primitives/button';

import { SeedPreviewPanel } from './SeedPreviewPanel';
import { useApproveSeed, useCatalogSeeds, useRejectSeed } from '../hooks/use-catalog-seeds';

import type { CatalogSeedDraftDto, CatalogSeedStatus } from '../lib/catalog-seed-types';

/**
 * Issue #1903 M8.3 — main queue list view.
 *
 * Combines the QueuePendingPanel + FailedItemsPanel concerns from #1835 into a
 * single filterable list. Approve and Reject (with reason prompt) call the
 * mediator-backed mutations and rely on the `useApproveSeed` / `useRejectSeed`
 * invalidation hook to refresh the list automatically.
 */
const FILTERS: { label: string; value: CatalogSeedStatus | 'All' }[] = [
  { label: 'All', value: 'All' },
  { label: 'Pending', value: 'Pending' },
  { label: 'Fetched', value: 'Fetched' },
  { label: 'FetchFailed', value: 'FetchFailed' },
  { label: 'Approved', value: 'Approved' },
  { label: 'Rejected', value: 'Rejected' },
];

const PAGE_SIZE = 50;

function StatusDot({ status }: { status: string }) {
  const tone =
    status === 'Approved'
      ? 'bg-entity-collection'
      : status === 'Fetched'
        ? 'bg-entity-game'
        : status === 'Pending'
          ? 'bg-entity-toolkit'
          : status === 'FetchFailed' || status === 'Rejected'
            ? 'bg-entity-event'
            : 'bg-muted-foreground';
  return <span className={`h-2 w-2 rounded-full ${tone}`} aria-hidden />;
}

interface SeedRowProps {
  draft: CatalogSeedDraftDto;
  expanded: boolean;
  onToggle: () => void;
}

function SeedRow({ draft, expanded, onToggle }: SeedRowProps) {
  const approve = useApproveSeed();
  const reject = useRejectSeed();

  const canApprove = draft.status === 'Fetched';
  const canReject = draft.status !== 'Approved' && draft.status !== 'Rejected';

  const handleApprove = async () => {
    try {
      await approve.mutateAsync(draft.id);
      toast.success(`Draft ${draft.id} approved`);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Approve failed');
    }
  };

  const handleReject = async () => {
    const reason = typeof window !== 'undefined' ? window.prompt('Reject reason?') : null;
    if (!reason || reason.trim().length === 0) return;
    try {
      await reject.mutateAsync({ id: draft.id, reason: reason.trim() });
      toast.success(`Draft ${draft.id} rejected`);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Reject failed');
    }
  };

  return (
    <li
      className="border-b border-border last:border-b-0"
      data-testid={`seed-row-${draft.id}`}
      data-status={draft.status}
    >
      <div className="flex items-center gap-2.5 px-3.5 py-2">
        <button
          type="button"
          onClick={onToggle}
          aria-label={expanded ? `Collapse ${draft.id}` : `Expand ${draft.id}`}
          className="text-muted-foreground hover:text-foreground"
        >
          {expanded ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
        </button>
        <StatusDot status={draft.status} />
        <span className="font-mono text-[11px] text-foreground">
          {draft.bggId ? `BGG:${draft.bggId}` : (draft.wikidataQid ?? draft.searchTermInput ?? '—')}
        </span>
        <span className="font-mono text-[10px] uppercase tracking-[0.04em] text-muted-foreground">
          {draft.status}
        </span>
        <div className="ml-auto flex items-center gap-2">
          {canApprove && (
            <Button
              size="sm"
              variant="outline"
              onClick={handleApprove}
              disabled={approve.isPending}
              aria-label={`Approve ${draft.id}`}
            >
              {approve.isPending ? (
                <Loader2 className="h-3 w-3 animate-spin" />
              ) : (
                <Check className="h-3 w-3" />
              )}
              Approve
            </Button>
          )}
          {canReject && (
            <Button
              size="sm"
              variant="outline"
              onClick={handleReject}
              disabled={reject.isPending}
              aria-label={`Reject ${draft.id}`}
            >
              {reject.isPending ? (
                <Loader2 className="h-3 w-3 animate-spin" />
              ) : (
                <X className="h-3 w-3" />
              )}
              Reject
            </Button>
          )}
        </div>
      </div>
      {expanded && (
        <div className="border-t border-border bg-muted/20 p-3">
          <SeedPreviewPanel draft={draft} />
        </div>
      )}
    </li>
  );
}

export function SeedQueueList() {
  const [filter, setFilter] = useState<(typeof FILTERS)[number]['value']>('All');
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const { data, isLoading, isError, error, refetch } = useCatalogSeeds({
    status: filter === 'All' ? undefined : filter,
    skip: 0,
    take: PAGE_SIZE,
  });

  return (
    <section
      aria-label="Catalog seed queue list"
      className="overflow-hidden rounded-xl border border-border bg-card"
    >
      <header className="flex flex-wrap items-center gap-2.5 border-b border-border bg-muted/30 px-3.5 py-2.5">
        <h3 className="font-quicksand text-[13px] font-extrabold text-foreground">📋 Queue</h3>
        <span className="font-mono text-[11px] text-muted-foreground">
          {data ? `${data.total} total` : '…'}
        </span>
        <div
          className="ml-auto flex flex-wrap items-center gap-1"
          role="group"
          aria-label="Filter by status"
        >
          {FILTERS.map(f => (
            <button
              key={f.value}
              type="button"
              onClick={() => setFilter(f.value)}
              data-state={filter === f.value ? 'active' : 'inactive'}
              className="rounded border border-border bg-background px-2 py-0.5 font-mono text-[10px] uppercase tracking-[0.04em] hover:bg-muted data-[state=active]:bg-entity-toolkit/[0.12] data-[state=active]:text-entity-toolkit"
            >
              {f.label}
            </button>
          ))}
        </div>
      </header>

      {isError && (
        <div role="alert" className="px-3.5 py-4 text-sm text-entity-event">
          <p>{error instanceof Error ? error.message : 'Failed to load queue'}</p>
          <Button variant="outline" size="sm" className="mt-2" onClick={() => refetch()}>
            Riprova
          </Button>
        </div>
      )}

      {isLoading && <p className="px-3.5 py-4 text-sm text-muted-foreground">Loading queue…</p>}

      {!isLoading && !isError && data && data.items.length === 0 && (
        <p className="px-3.5 py-6 text-sm text-muted-foreground">
          No seed drafts match this filter.
        </p>
      )}

      {!isLoading && !isError && data && data.items.length > 0 && (
        <ul className="divide-y divide-border">
          {data.items.map(draft => (
            <SeedRow
              key={draft.id}
              draft={draft}
              expanded={expandedId === draft.id}
              onToggle={() => setExpandedId(prev => (prev === draft.id ? null : draft.id))}
            />
          ))}
        </ul>
      )}
    </section>
  );
}
